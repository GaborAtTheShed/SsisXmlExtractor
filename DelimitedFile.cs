﻿using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SsisXmlExtractor
{
    [DelimitedRecord("|")]
    public class DelimitedFile
    {
        public string? FileName { get; set; }
        public string? RefId { get; set; }
        public string? TaskName { get; set; }
        public string? TaskType { get; set; }
        public string? Sql { get; set; }
        public string? TableNames { get; set; }
    }
}
