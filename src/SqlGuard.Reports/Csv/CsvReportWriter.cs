using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Csv
{
    /// <summary>
    /// Writes a CSV report suitable for Excel, Power BI, or any data analysis tool.
    ///
    /// Output structure:
    ///   Row 1: Headers
    ///   Row 2+: One row per evaluation result
    ///   Final rows: Summary statistics block (separated by blank line)
    ///
    /// All fields are RFC 4180 compliant — values containing commas, quotes,
    /// or newlines are properly quoted and escaped.
    /// </summary>
    public sealed class CsvReportWriter : IReportWriter
    {
        public ReportFormat Format => ReportFormat.Csv;
        public string FileExtension => ".csv";
        public string ContentType => "text/csv";

        private static readonly string[] ResultHeaders =
        [
            "scan_id", "engine", "host", "database", "server_version",
        "rule_id", "title", "category", "severity", "status",
        "mandatory", "pack", "detail", "actual_value",
        "compliance_references", "duration_ms", "evaluated_at", "remediation"
        ];

        public async Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default)
        {
            // Build entire CSV in memory first — avoids WriteLineAsync(string, CancellationToken)
            // ambiguity with ReadOnlyMemory<char> overload introduced in .NET 6+.
            var sb = new StringBuilder(4096);

            // ── Header row ────────────────────────────────────────────────────────
            sb.AppendLine(CsvRow(ResultHeaders));

            // ── Result rows ───────────────────────────────────────────────────────
            foreach (var r in result.Results.OrderByDescending(r => r.Severity).ThenBy(r => r.RuleId))
            {
                var row = new object?[]
                {
                result.ScanId,
                result.Engine,
                result.Host,
                result.Database,
                result.ServerVersion,
                r.RuleId,
                r.Title,
                r.Category,
                r.Severity,
                r.Status,
                r.Mandatory,
                r.Pack,
                r.Detail,
                r.ActualValue?.ToString(),
                string.Join("; ", r.ComplianceReferences),
                (long)r.Duration.TotalMilliseconds,
                r.EvaluatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                r.Remediation?.Replace('\n', ' ')
                };
                sb.AppendLine(CsvRow(row.Select(v => v?.ToString())));
            }

            // ── Summary block ─────────────────────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine("# Summary");
            sb.AppendLine(CsvRow(["metric", "value"]));
            sb.AppendLine(CsvRow(["scan_id", result.ScanId.ToString()]));
            sb.AppendLine(CsvRow(["engine", result.Engine]));
            sb.AppendLine(CsvRow(["host", result.Host]));
            sb.AppendLine(CsvRow(["database", result.Database]));
            sb.AppendLine(CsvRow(["server_version", result.ServerVersion ?? ""]));
            sb.AppendLine(CsvRow(["compliance_score", $"{result.ComplianceScore:F1}%"]));
            sb.AppendLine(CsvRow(["pass", result.PassCount.ToString()]));
            sb.AppendLine(CsvRow(["fail", result.FailCount.ToString()]));
            sb.AppendLine(CsvRow(["mandatory_fail", result.MandatoryFailCount.ToString()]));
            sb.AppendLine(CsvRow(["warn", result.WarnCount.ToString()]));
            sb.AppendLine(CsvRow(["error", result.ErrorCount.ToString()]));
            sb.AppendLine(CsvRow(["duration_seconds", $"{result.Duration.TotalSeconds:F2}"]));
            sb.AppendLine(CsvRow(["packs_loaded", string.Join("; ", result.PacksLoaded)]));
            sb.AppendLine(CsvRow(["started_at", result.StartedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")]));

            // ── Write — UTF-8 BOM so Excel opens without import wizard ────────────
            await using var writer = new StreamWriter(
                output,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                leaveOpen: true);
            await writer.WriteAsync(sb.ToString());
        }

        // ── RFC 4180 CSV serialisation ────────────────────────────────────────────

        private static string CsvRow(IEnumerable<string?> values) =>
            string.Join(",", values.Select(EscapeField));

        private static string EscapeField(string? value)
        {
            if (value is null) return string.Empty;

            // Must quote if contains comma, double-quote, newline, or leading/trailing whitespace
            bool needsQuoting = value.Contains(',') || value.Contains('"') ||
                                value.Contains('\n') || value.Contains('\r') ||
                                value != value.Trim();

            if (!needsQuoting) return value;

            // Escape internal double-quotes by doubling them
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
