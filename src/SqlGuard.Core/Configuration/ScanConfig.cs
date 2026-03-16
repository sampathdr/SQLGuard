using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Configuration
{
    /// <summary>
    /// Scan behaviour defaults (overridable via CLI flags).
    /// </summary>
    public sealed class ScanConfig
    {
        /// <summary>Rule packs to load. Default: [sqlserver-core].</summary>
        public List<string> Packs { get; set; } = ["sqlserver-core"];

        /// <summary>
        /// Severity threshold for CI/CD failure.
        /// Valid: mandatory | critical | high | medium | low
        /// </summary>
        public string FailOn { get; set; } = "mandatory";

        /// <summary>Minimum severity to include in results. Default: Low.</summary>
        public string MinSeverity { get; set; } = "Low";

        /// <summary>Only run mandatory rules.</summary>
        public bool MandatoryOnly { get; set; }

        /// <summary>Rule IDs to exclude from all scans.</summary>
        public List<string> ExcludeRules { get; set; } = [];

        /// <summary>Max parallel rule evaluations per scan.</summary>
        public int MaxConcurrency { get; set; } = 4;
    }
}
