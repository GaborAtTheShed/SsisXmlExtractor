using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsisXmlExtractor
{
    [DelimitedRecord("\t")]
    public class SqlObject
    {
        public string? SqlObjectName { get; set; }
        public string? SqlObjectType { get; set; }
        public string? SqlObjectLocation { get; set; }
    }
}
