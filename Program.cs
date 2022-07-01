using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileHelpers;
using SsisXmlExtractor;

class Program
{
    private static Regex sWhitespace = new Regex(@"\s+");
    private const string OUTPUT_FILENAME = "ssis_sql_output";
    private const string SQL_OBJECTS_FILENAME = "sql_objects.txt";

    public static void Main(string[] args)
    {
        Console.ResetColor();

        var currentDirectory = Directory.GetCurrentDirectory();

        var sqlObjectsFile = Directory.GetFiles(currentDirectory, SQL_OBJECTS_FILENAME);
        var dtsxFilePaths = Directory.GetFiles(currentDirectory, "*.dtsx");
        List<SqlObject> listOfSqlObjects = new();

        if (sqlObjectsFile.Length == 0)
        {
            Console.WriteLine($"Can't find {SQL_OBJECTS_FILENAME}, continue without matching objects? Press Y to continue, otherwise any key to exit.");
            var answer = Console.ReadKey().Key;
            if (answer != ConsoleKey.Y)
            {
                Environment.Exit(0);
            }
        }
        else
        {
            try
            {
                var readEngine = new FileHelperEngine<SqlObject>();
                listOfSqlObjects = readEngine.ReadFile(SQL_OBJECTS_FILENAME).ToList();
                Console.WriteLine($"{sqlObjectsFile[0]} has been loaded.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"There was a problem loading {SQL_OBJECTS_FILENAME}: {ex.Message}, {ex.StackTrace}");
            }
        }

        if (dtsxFilePaths.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No dtsx files can be found at {currentDirectory}");

            Environment.Exit(0);
        }

        XNamespace dtsNs = "www.microsoft.com/SqlServer/Dts";
        XNamespace sqlTaskNs = "www.microsoft.com/sqlserver/dts/tasks/sqltask";
        
        List<OutputFileRow> listForExport = new ();

        foreach (var file in dtsxFilePaths)
        {
            var fileName = new FileInfo(file).Name;
            
            try
            {
                Console.WriteLine($"Processing: {fileName}");
                XDocument document = XDocument.Load(file);

                // data flow tasks are in components
                var componentData = document
                    .Descendants("component")
                    .Select(x => new
                    {
                        RefId = x.Attribute("refId").Value
                      ,
                        Name = x.Attribute("name").Value
                      ,
                        DataFlowTaskName = x.Ancestors(dtsNs + "Executable").Select(y => y.Attribute(dtsNs + "ObjectName").Value).FirstOrDefault()
                      ,
                        ComponentId = x.Attribute("componentClassID").Value
                      ,
                        Disabled = x.Ancestors(dtsNs + "Executable")
                            .Select(y => y.Attribute(dtsNs + "Disabled") == null ? "False" : y.Attribute(dtsNs + "Disabled").Value).FirstOrDefault()
                      ,
                        PropertyValue = x.Descendants("property")

                            .Where(z => z.Attribute("name").Value == "OpenRowset"
                                || z.Attribute("name").Value == "SqlCommand"
                                || z.Attribute("name").Value == "TableOrViewName")
                            .Select(z => ReplaceWhiteSpaceAndOtherChars(z.Value))
                            .FirstOrDefault(z => !string.IsNullOrEmpty(z))
                    })
                    .ToList();

                foreach (var record in componentData)
                {
                    listForExport.Add(new OutputFileRow
                    {
                        FileName = fileName,
                        RefId = record.RefId,
                        DataFlowTaskName = record.DataFlowTaskName,
                        TaskName = record.Name,
                        TaskType = record.ComponentId,
                        TaskOrParentDisabled = record.Disabled,
                        Sql = record.PropertyValue,
                        ExtractedTableNames = string.Join(", ", ExtractTableNames(record.PropertyValue)),
                        MatchFound = ""
                    });
                }

                var sqlStatements = document
                    .Descendants(dtsNs + "Executable")
                    .Where(x => x.Attribute(dtsNs + "CreationName").Value == "Microsoft.ExecuteSQLTask")
                    .Select(x => new
                    {
                        RefId = x.Attribute(dtsNs + "refId").Value
                      ,
                        Name = x.Attribute(dtsNs + "ObjectName").Value
                      ,
                        ExecutableType = x.Attribute(dtsNs + "ExecutableType").Value
                      , 
                        Disabled = (x.Attribute(dtsNs + "Disabled") == null ? "False" : x.Attribute(dtsNs + "Disabled").Value)
                      ,
                        PropertyValue = x.Descendants(sqlTaskNs + "SqlTaskData")
                                            .Select(x => ReplaceWhiteSpaceAndOtherChars(x.Attribute(sqlTaskNs + "SqlStatementSource").Value))
                                            .FirstOrDefault()
                    })
                    .ToList();

                foreach (var record in sqlStatements)
                {
                    listForExport.Add(new OutputFileRow
                    {
                        FileName = fileName,
                        RefId = record.RefId,
                        DataFlowTaskName = "",
                        TaskName = record.Name,
                        TaskType = record.ExecutableType,
                        TaskOrParentDisabled = record.Disabled,
                        Sql = record.PropertyValue,
                        ExtractedTableNames = string.Join(", ", ExtractTableNames(record.PropertyValue)),
                        MatchFound = ""
                    }); ;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error while processing {fileName}: {ex.Message}, {ex.StackTrace}");
                Console.WriteLine("To continue processing the rest of the files, press any key.");
                Console.ResetColor();
                
                Console.ReadKey();
            }
        }
        
        listForExport.OrderBy(l => l.RefId).ThenBy(l => l.TaskName);

        var matchedSqlObjects = MatchSqlObjectsInList(listForExport, listOfSqlObjects);

        var fileSuffixWithExtension = $"_{DateTime.Now:yyyy-MM-ddTHHmmss}.txt";
        var outputFileName = OUTPUT_FILENAME + fileSuffixWithExtension;
        var matchedSqlObjectsFileName = "matched_sql_objects" + fileSuffixWithExtension;

        var outputFileEngine = new FileHelperEngine<OutputFileRow>();
        outputFileEngine.HeaderText = outputFileEngine.GetFileHeader();
        outputFileEngine.WriteFile(outputFileName, listForExport);

        var sqlObjectEngine = new FileHelperEngine<MatchedSqlObject>();
        sqlObjectEngine.HeaderText = sqlObjectEngine.GetFileHeader();
        sqlObjectEngine.WriteFile(matchedSqlObjectsFileName, matchedSqlObjects);
    }

    private static List<MatchedSqlObject> MatchSqlObjectsInList(List<OutputFileRow> listForExport, List<SqlObject> listOfSqlObjects)
    {
        Console.WriteLine("Matching SSIS objects to SQL objects...");
        List<MatchedSqlObject> matchedObjectsOutput = new ();
        List<MatchedSqlObject> matchedObjectsTemp = new ();

        foreach (var ssisSql in listForExport)
        {
            matchedObjectsTemp.Clear();

            if (ssisSql.Sql != null)
            {
                var firstMatchFromSqlScript = listOfSqlObjects
                    .Where(l => ssisSql.Sql.Contains(l.SqlObjectName, StringComparison.InvariantCultureIgnoreCase))
                    .OrderByDescending(l => l.SqlObjectName.Length)
                    .FirstOrDefault();

                if (firstMatchFromSqlScript != null)
                {
                    matchedObjectsTemp.Add(new MatchedSqlObject
                    {
                        SqlObjectName = firstMatchFromSqlScript.SqlObjectName,
                        SqlObjectLocation = firstMatchFromSqlScript.SqlObjectLocation,
                        SqlObjectType = firstMatchFromSqlScript.SqlObjectType,
                        PackageName = ssisSql.FileName,
                        RefId = ssisSql.RefId,
                        TaskOrParentDisabled = ssisSql.TaskOrParentDisabled
                    });
                }

                var extractedTableNames = ExtractTableNames(ssisSql.Sql);

                if (extractedTableNames != null && extractedTableNames.Count > 1)
                {
                    foreach (var tableName in extractedTableNames)
                    {
                        var matchFromExtractedTableNames = listOfSqlObjects
                            .Where(l => tableName.Contains(l.SqlObjectName, StringComparison.InvariantCultureIgnoreCase))
                            .OrderByDescending(l => l.SqlObjectName.Length)
                            .FirstOrDefault();

                        if (matchFromExtractedTableNames != null)
                        {
                            if (!matchedObjectsTemp.Any(m => m.SqlObjectName == matchFromExtractedTableNames.SqlObjectName))
                            {
                                matchedObjectsTemp.Add(new MatchedSqlObject
                                {
                                    SqlObjectName = matchFromExtractedTableNames.SqlObjectName,
                                    SqlObjectLocation = matchFromExtractedTableNames.SqlObjectLocation,
                                    SqlObjectType = matchFromExtractedTableNames.SqlObjectType,
                                    PackageName = ssisSql.FileName,
                                    RefId = ssisSql.RefId,
                                    TaskOrParentDisabled = ssisSql.TaskOrParentDisabled
                                });
                            }
                        }
                    }
                }

                if (matchedObjectsTemp != null && matchedObjectsTemp.Count > 0)
                {
                    
                    foreach (var matchedSqlObject in matchedObjectsTemp)
                    {
                        matchedObjectsOutput.Add(matchedSqlObject);
                    }
                }

                ssisSql.MatchFound = matchedObjectsTemp.Count.ToString();
            }
            else
            {
                ssisSql.MatchFound = "n/a";
            }
        }

        Console.WriteLine("Done!");
        return matchedObjectsOutput;
    }

    private static string ReplaceWhiteSpaceAndOtherChars(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        input = input.Replace("[", "");
        input = input.Replace("]", "");
        input = input.Replace("\"", "");

        return sWhitespace.Replace(input, " ");
    }

    private static List<string> ExtractTableNames(string? query)
    {
        List<string> tables = new List<string>();
        
        if (string.IsNullOrEmpty(query))
        {
            return tables;
        }

        string pattern = @"(from|join|into)\s+([`]\w+.+\w+\s*[`]|(\[)\w+.+\w+\s*(\])|\w+\s*\.+\s*\w*|\w+\b)";

        foreach (Match m in Regex.Matches(query, pattern, RegexOptions.IgnoreCase))
        {
            string name = m.Groups[2].Value;
            tables.Add(name);
        }

        return tables;
    }
}