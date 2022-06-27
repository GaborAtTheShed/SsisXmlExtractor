using System.Text.RegularExpressions;
using System.Xml.Linq;
using FileHelpers;
using SsisXmlExtractor;

class Program
{
    private static Regex sWhitespace = new Regex(@"\s+");
    private const string OUTPUT_FILENAME = "ssis_sql_output";

    public static void Main(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var filePaths = Directory.GetFiles(currentDirectory, "*.dtsx");

        if (filePaths.Length == 0)
        {
            throw new Exception($"No dtsx files can be found at {currentDirectory}");
        }

        XNamespace dtsNs = "www.microsoft.com/SqlServer/Dts";
        XNamespace sqlTaskNs = "www.microsoft.com/sqlserver/dts/tasks/sqltask";
        
        List<DelimitedFile> listForExport = new ();

        foreach (var file in filePaths)
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
                        //.Select(y => y.Attribute(dtsNs + "ExecutableType").Value).FirstOrDefault()
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
                    listForExport.Add(new DelimitedFile
                    {
                        FileName = fileName,
                        RefId = record.RefId,
                        DataFlowTaskName = record.DataFlowTaskName,
                        TaskName = record.Name,
                        TaskType = record.ComponentId,
                        TaskOrParentDisabled = record.Disabled,
                        Sql = record.PropertyValue,
                        MatchedSqlObjectName = ""
                    });
                }

                var sqlStatements = document
                    .Descendants(dtsNs + "Executable")
                    //.Where(x => x.Attribute(dtsNs + "Disabled") || x.Attribute(dtsNs + "Disabled").Value == "True")
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
                    listForExport.Add(new DelimitedFile
                    {
                        FileName = fileName,
                        RefId = record.RefId,
                        DataFlowTaskName = "",
                        TaskName = record.Name,
                        TaskType = record.ExecutableType,
                        TaskOrParentDisabled = record.Disabled,
                        Sql = record.PropertyValue,
                        MatchedSqlObjectName = ""
                    }); ;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error while processing {fileName}: {ex.Message}, {ex.StackTrace}");
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("To continue processing the rest of the files, press any key.");
                Console.ResetColor();
                
                Console.ReadKey();
            }
        }
        
        listForExport.OrderBy(l => l.RefId).ThenBy(l => l.TaskName);

        var fileNameWithDate = OUTPUT_FILENAME + $"_{DateTime.Now:yyyy-MM-ddTHHmmss}.txt";

        var fileHelperEngine = new FileHelperEngine<DelimitedFile>();
        fileHelperEngine.HeaderText = fileHelperEngine.GetFileHeader();
        fileHelperEngine.WriteFile(fileNameWithDate, listForExport);
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

    //public static string? GetTables(string? query)
    //{
    //    if (string.IsNullOrEmpty(query))
    //    {
    //        return query;
    //    }

    //    List<string> tables = new List<string>();

    //    string pattern = @"(from|join|into)\s+([`]\w+.+\w+\s*[`]|(\[)\w+.+\w+\s*(\])|\w+\s*\.+\s*\w*|\w+\b)";

    //    foreach (Match m in Regex.Matches(query, pattern, RegexOptions.IgnoreCase))
    //    {
    //        string name = m.Groups[2].Value;
    //        tables.Add(name);
    //    }

    //    return string.Join(", ", tables);
    //}
}