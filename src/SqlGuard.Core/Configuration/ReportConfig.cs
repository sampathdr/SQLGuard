using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Configuration
{
    /// <summary>
    /// Report output configuration.
    /// </summary>
    public sealed class ReportConfig
    {
        /// <summary>
        /// Output formats to generate.
        /// Valid: console, json, html, sarif, csv, markdown
        /// </summary>
        public List<string> Formats { get; set; } = ["console"];

        /// <summary>Directory to write file reports. Default: ./reports</summary>
        public string OutputDirectory { get; set; } = "./reports";

        /// <summary>Base filename without extension. Default: sqlguard-report</summary>
        public string FileBaseName { get; set; } = "sqlguard-report";

        /// <summary>
        /// For multi-server scans: use {label} in FileBaseName for per-server files.
        /// e.g. "{label}-sqlguard-report" → "prod-db-1-sqlguard-report.html"
        /// </summary>
        public bool PerServerFiles { get; set; } = true;
    }
}
