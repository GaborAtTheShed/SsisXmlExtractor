using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsisXmlExtractor
{
    [DelimitedRecord("\t")]
    public class MatchedSqlObject
    {
        public string? SqlObjectName { get; set; }
        public string? SqlObjectType { get; set; }
        public string? SqlObjectLocation { get; set; }
        public string? PackageName { get; set; }
        public string? RefId { get; set; }
        public string? TaskOrParentDisabled { get; set; }
    }
}
