using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Markdown
{
    /// <summary>
    /// Writes a Markdown report ideal for:
    ///   • GitHub PR comments (post as a bot comment in CI)
    ///   • GitHub wiki / documentation
    ///   • Audit trail in repositories
    ///
    /// The output is standard CommonMark, rendered correctly by GitHub,
    /// GitLab, Bitbucket, and most Markdown renderers.
    /// </summary>
    public sealed class MarkdownReportWriter : IReportWriter
    {
        public ReportFormat Format => ReportFormat.Markdown;
        public string FileExtension => ".md";
        public string ContentType => "text/markdown";

        public async Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default)
        {
            var sb = new StringBuilder(4096);

            WriteHeader(sb, result);
            WriteSummaryTable(sb, result);
            WriteSeverityBreakdown(sb, result);
            WriteFailedFindings(sb, result);
            WritePassedFindings(sb, result);
            WriteFooter(sb, result);

            await using var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
            await writer.WriteAsync(sb.ToString());
        }

        private static void WriteHeader(StringBuilder sb, ScanResult result)
        {
            sb.AppendLine("# 🛡️ SqlGuard Security Scan Report");
            sb.AppendLine();
            sb.AppendLine($"> **Scan ID:** `{result.ScanId}`  ");
            sb.AppendLine($"> **Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
            sb.AppendLine($"> **Engine:** {result.Engine} {result.ServerVersion}  ");
            sb.AppendLine($"> **Server:** `{result.Host}` / `{result.Database}`");
            if (!string.IsNullOrWhiteSpace(result.Label))
                sb.AppendLine($"> **Label:** {result.Label}");
            sb.AppendLine();

            // Overall badge
            var scoreEmoji = result.ComplianceScore >= 90 ? "🟢" : result.ComplianceScore >= 70 ? "🟡" : "🔴";
            var verdictLine = result.MandatoryFailCount == 0
                ? "✅ **All mandatory checks passed**"
                : $"❌ **{result.MandatoryFailCount} mandatory check(s) failed**";
            sb.AppendLine($"{verdictLine} &nbsp;|&nbsp; {scoreEmoji} Compliance Score: **{result.ComplianceScore}%**");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        private static void WriteSummaryTable(StringBuilder sb, ScanResult result)
        {
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| 🔴 Critical Fails | {CountBySeverity(result, Severity.Critical)} |");
            sb.AppendLine($"| 🟠 High Fails | {CountBySeverity(result, Severity.High)} |");
            sb.AppendLine($"| 🟡 Medium Fails | {CountBySeverity(result, Severity.Medium)} |");
            sb.AppendLine($"| 🔵 Low / Info Fails | {CountBySeverity(result, Severity.Low) + CountBySeverity(result, Severity.Info)} |");
            sb.AppendLine($"| ⚠️  Warnings | {result.WarnCount} |");
            sb.AppendLine($"| ✅ Passed | {result.PassCount} |");
            sb.AppendLine($"| ⏭️  Skipped | {result.SkippedCount} |");
            sb.AppendLine($"| 💥 Errors | {result.ErrorCount} |");
            sb.AppendLine($"| 📊 Compliance Score | **{result.ComplianceScore}%** |");
            sb.AppendLine($"| ⏱️  Duration | {result.Duration.TotalSeconds:F2}s |");
            sb.AppendLine($"| 📦 Packs | {string.Join(", ", result.PacksLoaded.Select(p => $"`{p}`"))} |");
            sb.AppendLine();
        }

        private static void WriteSeverityBreakdown(StringBuilder sb, ScanResult result)
        {
            var bySeverity = result.Results
                .GroupBy(r => r.Severity)
                .OrderByDescending(g => g.Key)
                .ToList();

            if (!bySeverity.Any()) return;

            sb.AppendLine("## Results by Severity");
            sb.AppendLine();

            foreach (var group in bySeverity)
            {
                var icon = group.Key switch
                {
                    Severity.Critical => "🔴",
                    Severity.High => "🟠",
                    Severity.Medium => "🟡",
                    Severity.Low => "🔵",
                    _ => "⚪"
                };
                var pass = group.Count(r => r.Status == RuleStatus.Pass);
                var fail = group.Count(r => r.Status == RuleStatus.Fail);
                var warn = group.Count(r => r.Status == RuleStatus.Warn);
                sb.AppendLine($"- {icon} **{group.Key}**: {fail} fail &nbsp; {warn} warn &nbsp; {pass} pass");
            }
            sb.AppendLine();
        }

        private static void WriteFailedFindings(StringBuilder sb, ScanResult result)
        {
            var failures = result.Results
                .Where(r => r.Status is RuleStatus.Fail or RuleStatus.Warn)
                .OrderByDescending(r => r.Severity)
                .ThenBy(r => r.RuleId)
                .ToList();

            if (!failures.Any()) return;

            sb.AppendLine("## ❌ Failed & Warning Checks");
            sb.AppendLine();
            sb.AppendLine("| Rule | Severity | Status | Category | Title |");
            sb.AppendLine("|------|----------|--------|----------|-------|");

            foreach (var r in failures)
            {
                var sevIcon = r.Severity switch
                {
                    Severity.Critical => "🔴 Critical",
                    Severity.High => "🟠 High",
                    Severity.Medium => "🟡 Medium",
                    Severity.Low => "🔵 Low",
                    _ => "⚪ Info"
                };
                var statusIcon = r.Status == RuleStatus.Fail ? "❌ Fail" : "⚠️ Warn";
                var mandatory = r.Mandatory ? " 🔒" : "";
                sb.AppendLine($"| `{r.RuleId}` | {sevIcon} | {statusIcon}{mandatory} | {r.Category} | {EscapeMd(r.Title)} |");
            }
            sb.AppendLine();

            // Detail blocks for mandatory failures
            var mandatoryFails = failures.Where(r => r.Status == RuleStatus.Fail && r.Mandatory).ToList();
            if (mandatoryFails.Any())
            {
                sb.AppendLine("### Mandatory Failure Details");
                sb.AppendLine();
                foreach (var r in mandatoryFails)
                {
                    sb.AppendLine($"<details>");
                    sb.AppendLine($"<summary><strong>{r.RuleId}</strong> — {EscapeMd(r.Title)}</summary>");
                    sb.AppendLine();
                    if (!string.IsNullOrWhiteSpace(r.Detail))
                    {
                        sb.AppendLine($"**Finding:** {EscapeMd(r.Detail)}");
                        sb.AppendLine();
                    }
                    if (!string.IsNullOrWhiteSpace(r.Remediation))
                    {
                        sb.AppendLine("**Remediation:**");
                        sb.AppendLine($"```sql");
                        sb.AppendLine(r.Remediation.Trim());
                        sb.AppendLine("```");
                    }
                    if (r.ComplianceReferences.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"**References:** {string.Join(" · ", r.ComplianceReferences)}");
                    }
                    sb.AppendLine();
                    sb.AppendLine("</details>");
                    sb.AppendLine();
                }
            }
        }

        private static void WritePassedFindings(StringBuilder sb, ScanResult result)
        {
            var passed = result.Results
                .Where(r => r.Status == RuleStatus.Pass)
                .OrderBy(r => r.RuleId)
                .ToList();

            if (!passed.Any()) return;

            sb.AppendLine("<details>");
            sb.AppendLine("<summary>✅ Passed Checks</summary>");
            sb.AppendLine();
            sb.AppendLine("| Rule | Severity | Category | Title |");
            sb.AppendLine("|------|----------|----------|-------|");
            foreach (var r in passed)
                sb.AppendLine($"| `{r.RuleId}` | {r.Severity} | {r.Category} | {EscapeMd(r.Title)} |");
            sb.AppendLine();
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        private static void WriteFooter(StringBuilder sb, ScanResult result)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Generated by [SqlGuard](https://github.com/your-org/sqlguard) · " +
                          $"Scan started {result.StartedAt:yyyy-MM-dd HH:mm:ss} UTC · " +
                          $"Duration {result.Duration.TotalSeconds:F2}s*");
        }

        private static int CountBySeverity(ScanResult result, Severity severity) =>
            result.Results.Count(r => r.Status == RuleStatus.Fail && r.Severity == severity);

        private static string EscapeMd(string text) =>
            text.Replace("|", "\\|").Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`");
    }
}
