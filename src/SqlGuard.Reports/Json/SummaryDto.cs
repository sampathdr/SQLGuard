using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Json
{
    internal sealed class SummaryDto
    {
        public double ComplianceScore { get; init; }
        public int TotalChecked { get; init; }
        public int Pass { get; init; }
        public int Fail { get; init; }
        public int MandatoryFail { get; init; }
        public int Warn { get; init; }
        public int Error { get; init; }
        public int Skipped { get; init; }
        public string HighestSeverity { get; init; } = null!;
        public bool Passed { get; init; }
    }
}
