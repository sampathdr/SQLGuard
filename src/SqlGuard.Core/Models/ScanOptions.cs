using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    /// <summary>
    /// Runtime configuration for a scan run
    /// </summary>
    public sealed record ScanOptions
    {
        public IReadOnlyList<string> Packs { get; init; } = ["core"];
        public IReadOnlyList<string>? IncludeRuleIds { get; init; }          // null = all
        public IReadOnlyList<string>? ExcludeRuleIds { get; init; }
        public IReadOnlyList<string>? Categories { get; init; }
        public Severity MinSeverity { get; init; } = Severity.Low;
        public bool MandatoryOnly { get; init; } = false;
        public bool FailFast { get; init; } = false;
        public Severity FailOnSeverity { get; init; } = Severity.High;
        public int MaxConcurrency { get; init; } = 4;
    }
}
