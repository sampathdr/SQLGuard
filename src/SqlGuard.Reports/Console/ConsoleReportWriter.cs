using Spectre.Console;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Console
{
    /// <summary>
    /// Renders a rich, coloured scan report to the terminal using Spectre.Console.
    /// Writes directly to stdout — the <paramref name="output"/> stream is unused
    /// (the caller passes <see cref="Stream.Null"/>).
    ///
    /// Output structure:
    ///   1. Header rule
    ///   2. Scan summary panel (engine, host, version, score, timing)
    ///   3. Severity breakdown bar chart
    ///   4. Critical + High findings detail (with remediation)
    ///   5. Full results table
    ///   6. Final verdict panel
    /// </summary>
    public sealed class ConsoleReportWriter : IReportWriter
    {
        public ReportFormat Format => ReportFormat.Console;
        public string FileExtension => ".txt";
        public string ContentType => "text/plain";

        public Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default)
        {
            RenderHeader(result);
            RenderSummaryPanel(result);
            RenderSeverityBreakdown(result);
            RenderCriticalFindings(result);
            RenderResultsTable(result);
            RenderVerdict(result);
            return Task.CompletedTask;
        }

        // ── Sections ─────────────────────────────────────────────────────────────

        private static void RenderHeader(ScanResult result)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new FigletText("SqlGuard").Color(Color.Cyan1));
            AnsiConsole.Write(new Rule("[bold cyan]Database Security Scan Report[/]").RuleStyle("cyan dim"));
        }

        private static void RenderSummaryPanel(ScanResult result)
        {
            var grid = new Grid()
                .AddColumn(new GridColumn().Width(16))
                .AddColumn(new GridColumn().Width(28))
                .AddColumn(new GridColumn().Width(16))
                .AddColumn(new GridColumn().Width(28));

            grid.AddRow("[grey]Engine[/]", $"[cyan]{result.Engine}[/]",
                        "[grey]Host[/]", $"[cyan]{Markup.Escape(result.Host)}[/]");
            grid.AddRow("[grey]Database[/]", Markup.Escape(result.Database),
                        "[grey]Version[/]", Markup.Escape(result.ServerVersion ?? "unknown"));
            grid.AddRow("[grey]Packs[/]", Markup.Escape(string.Join(", ", result.PacksLoaded)),
                        "[grey]Duration[/]", $"{result.Duration.TotalSeconds:F2}s");
            grid.AddRow("[grey]Scanned[/]", result.StartedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                        "[grey]Score[/]", ScoreMarkup(result.ComplianceScore));
            grid.AddRow("[grey]Pass[/]", $"[green]{result.PassCount}[/]",
                        "[grey]Fail[/]", $"[red]{result.FailCount}  (mandatory: {result.MandatoryFailCount})[/]");
            grid.AddRow("[grey]Warn[/]", $"[yellow]{result.WarnCount}[/]",
                        "[grey]Error/Skip[/]", $"[grey]{result.ErrorCount} / {result.SkippedCount}[/]");

            AnsiConsole.Write(new Panel(grid)
                .Header("[bold]Scan Summary[/]")
                .BorderColor(Color.Cyan1)
                .Padding(1, 0));
            AnsiConsole.WriteLine();
        }

        private static void RenderSeverityBreakdown(ScanResult result)
        {
            var bySeverity = result.Results
                .Where(r => r.Status == RuleStatus.Fail)
                .GroupBy(r => r.Severity)
                .OrderByDescending(g => g.Key)
                .ToList();

            if (bySeverity.Count == 0) return;

            var chart = new BreakdownChart().Width(60);

            foreach (var group in bySeverity)
            {
                var (color, label) = group.Key switch
                {
                    Severity.Critical => (Color.Red, "Critical"),
                    Severity.High => (Color.DarkOrange, "High"),
                    Severity.Medium => (Color.Yellow, "Medium"),
                    Severity.Low => (Color.Blue, "Low"),
                    _ => (Color.Grey, "Info")
                };
                chart.AddItem(label, group.Count(), color);
            }

            AnsiConsole.Write(new Panel(chart)
                .Header("[bold]Failures by Severity[/]")
                .BorderColor(Color.Red)
                .Padding(1, 0));
            AnsiConsole.WriteLine();
        }

        private static void RenderCriticalFindings(ScanResult result)
        {
            var criticalAndHigh = result.Results
                .Where(r => r.Status == RuleStatus.Fail &&
                            r.Severity is Severity.Critical or Severity.High)
                .OrderByDescending(r => r.Severity)
                .ToList();

            if (criticalAndHigh.Count == 0) return;

            AnsiConsole.Write(new Rule("[bold red]Critical & High Findings[/]").RuleStyle("red dim"));

            foreach (var finding in criticalAndHigh)
            {
                var sevColor = finding.Severity == Severity.Critical ? "red" : "darkorange";
                var tree = new Tree($"[{sevColor}]■[/] [{sevColor}]{finding.Severity}[/]  [bold]{finding.RuleId}[/] — {Markup.Escape(finding.Title)}");

                if (!string.IsNullOrWhiteSpace(finding.Detail))
                    tree.AddNode($"[grey]Detail:[/] {Markup.Escape(finding.Detail)}");

                if (!string.IsNullOrWhiteSpace(finding.Remediation))
                    tree.AddNode($"[green]Fix:[/] {Markup.Escape(finding.Remediation.Split('\n')[0])}"); // first line only

                if (finding.ComplianceReferences.Count > 0)
                    tree.AddNode($"[grey]Refs:[/] {string.Join("  ", finding.ComplianceReferences)}");

                AnsiConsole.Write(tree);
            }
            AnsiConsole.WriteLine();
        }

        private static void RenderResultsTable(ScanResult result)
        {
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[grey]Rule ID[/]").Width(18))
                .AddColumn(new TableColumn("[grey]Sev[/]").Width(10))
                .AddColumn(new TableColumn("[grey]Status[/]").Width(11))
                .AddColumn(new TableColumn("[grey]Mand[/]").Width(5))
                .AddColumn(new TableColumn("[grey]Category[/]").Width(20))
                .AddColumn(new TableColumn("[grey]Title[/]"))
                .ShowRowSeparators();

            foreach (var r in result.Results
                .OrderByDescending(r => r.Severity)
                .ThenBy(r => r.Status)
                .ThenBy(r => r.RuleId))
            {
                var (statusMark, statusColor) = r.Status switch
                {
                    RuleStatus.Pass => ("✓ PASS", "green"),
                    RuleStatus.Fail => ("✗ FAIL", "red"),
                    RuleStatus.Warn => ("⚠ WARN", "yellow"),
                    RuleStatus.Error => ("! ERROR", "red"),
                    RuleStatus.NotApplicable => ("~ N/A", "grey"),
                    _ => ("- SKIP", "grey")
                };

                var sevColor = r.Severity switch
                {
                    Severity.Critical => "red",
                    Severity.High => "darkorange",
                    Severity.Medium => "yellow",
                    Severity.Low => "blue",
                    _ => "grey"
                };

                table.AddRow(
                    $"[dim]{r.RuleId}[/]",
                    $"[{sevColor}]{r.Severity}[/]",
                    $"[{statusColor}]{statusMark}[/]",
                    r.Mandatory ? "[red]●[/]" : "[grey]○[/]",
                    Markup.Escape(r.Category ?? ""),
                    Markup.Escape(r.Title)
                );
            }

            AnsiConsole.Write(new Panel(table)
                .Header("[bold]All Results[/]")
                .BorderColor(Color.Grey)
                .Padding(0, 0));
            AnsiConsole.WriteLine();
        }

        private static void RenderVerdict(ScanResult result)
        {
            if (result.MandatoryFailCount == 0)
            {
                AnsiConsole.Write(new Panel(
                    $"[bold green]✓  ALL MANDATORY CHECKS PASSED[/]  " +
                    $"[grey]Compliance Score: {result.ComplianceScore}%[/]")
                    .BorderColor(Color.Green));
            }
            else
            {
                AnsiConsole.Write(new Panel(
                    $"[bold red]✗  {result.MandatoryFailCount} MANDATORY CHECK(S) FAILED[/]  " +
                    $"[grey]Compliance Score: {result.ComplianceScore}%[/]")
                    .BorderColor(Color.Red));
            }
            AnsiConsole.WriteLine();
        }

        private static string ScoreMarkup(double score) => score switch
        {
            >= 90 => $"[green bold]{score}%[/]",
            >= 70 => $"[yellow bold]{score}%[/]",
            _ => $"[red bold]{score}%[/]"
        };
    }
}
