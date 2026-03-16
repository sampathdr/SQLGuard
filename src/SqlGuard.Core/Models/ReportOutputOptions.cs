using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    public sealed record ReportOutputOptions
    {
        /// <summary>One or more output formats to generate from a single scan.</summary>
        public IReadOnlyList<ReportFormat> Formats { get; init; } = [ReportFormat.Console];

        /// <summary>
        /// Directory to write file-based reports into.
        /// Null = current working directory.
        /// </summary>
        public string? OutputDirectory { get; init; }

        /// <summary>
        /// Base filename for reports (without extension).
        /// Defaults to "sqlguard-report".
        /// </summary>
        public string FileBaseName { get; init; } = "sqlguard-report";
    }
}
