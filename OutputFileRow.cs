using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsisXmlExtractor
{
    [DelimitedRecord("\t")]
    public class OutputFileRow
    {
        public string? FileName { get; set; }
        public string? RefId { get; set; }
        public string? DataFlowTaskName { get; set; }
        public string? TaskName { get; set; }
        public string? TaskType { get; set; }
        public string? TaskOrParentDisabled { get; set; }
        public string? Sql { get; set; }
        public string? MatchedSqlObjectName { get; set; }
    }
}
