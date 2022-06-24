using System.Text.RegularExpressions;
using System.Xml.Linq;

class Program
{
    private static Regex sWhitespace = new Regex(@"\s+");

    public static void Main(string[] args)
    {
        XDocument document = XDocument.Load("file.dtsx");

        XNamespace dtsNs = "www.microsoft.com/SqlServer/Dts";
        XNamespace sqlTaskNs = "www.microsoft.com/sqlserver/dts/tasks/sqltask";

        //data flow tasks are in components
        var componentData = document
            .Descendants("component")
            .Select(x => new
            {
                RefId = x.Attribute("refId").Value
                ,
                Name = x.Attribute("name").Value
                ,
                ComponentId = x.Attribute("componentClassID").Value
                ,
                PropertyValue = x.Descendants("property")
                    .Where(z =>
                    z.Attribute("name").Value == "OpenRowset"
                    ||
                    z.Attribute("name").Value == "SqlCommand"
                    ||
                    z.Attribute("name").Value == "TableOrViewName")
                    .Select(z => ReplaceWhitespace(z.Value))
                    .FirstOrDefault(z => !string.IsNullOrEmpty(z))
            })
            .ToList();

        int counter = 0;

        foreach (var item in componentData)
        {
            counter++;
            Console.WriteLine($"{counter}: {item} ");
        }

        // SQL task data
        // TODO: deal with disabled exceptions
        var sqlStatements = document
            .Descendants(dtsNs + "Executable")
            //.Where(x => string.IsNullOrEmpty(x.Attribute(dtsNs + "Disabled").ToString()))
            .Where(x => x.Attribute(dtsNs + "CreationName").Value == "Microsoft.ExecuteSQLTask")
            .Select(x => new
            {
                RefId = x.Attribute(dtsNs + "refId").Value
                ,
                Name = x.Attribute(dtsNs + "ObjectName").Value
                ,
                PropertyValue = x.Descendants(sqlTaskNs + "SqlTaskData")
                                 .Select(x => ReplaceWhitespace(x.Attribute(sqlTaskNs + "SqlStatementSource").Value))
                                 .FirstOrDefault()
            });

        foreach (var item in sqlStatements)
        {
            counter++;
            Console.WriteLine($"{counter}: {item}");
        }

        //static string RemoveLineBreaks(string input)
        //{
        //    if (!string.IsNullOrEmpty(input))
        //    {
        //        input = Regex.Replace(input, @"\r\n?|\n", " ");
        //    }

        //    return input;
        //}

        
    }
    private static string ReplaceWhitespace(string input)
    {
        return sWhitespace.Replace(input, " ");
    }
}