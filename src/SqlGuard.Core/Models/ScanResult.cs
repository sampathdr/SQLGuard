using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    /// <summary>
    /// Aggregate of a full scan run
    /// </summary>
    public sealed class ScanResult
    {
        public Guid ScanId { get; } = Guid.NewGuid();
        public required string Engine { get; init; }
        public required string Host { get; init; }
        public required string Database { get; init; }
        public string? Label { get; init; }
        public string? ServerVersion { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; set; }
        public TimeSpan Duration => CompletedAt - StartedAt;
        public IReadOnlyList<string> PacksLoaded { get; init; } = [];
        public IReadOnlyList<EvaluationResult> Results { get; init; } = [];

        // Summary
        public int PassCount => Results.Count(r => r.Status == RuleStatus.Pass);
        public int FailCount => Results.Count(r => r.Status == RuleStatus.Fail);
        public int WarnCount => Results.Count(r => r.Status == RuleStatus.Warn);
        public int ErrorCount => Results.Count(r => r.Status == RuleStatus.Error);
        public int SkippedCount => Results.Count(r => r.Status is RuleStatus.Skipped or RuleStatus.NotApplicable);
        public int MandatoryFailCount => Results.Count(r => r.Status == RuleStatus.Fail && r.Mandatory);
        public int TotalChecked => Results.Count(r => r.Status is not (RuleStatus.Skipped or RuleStatus.NotApplicable));

        public Severity HighestSeverity =>
            Results.Where(r => r.Status == RuleStatus.Fail)
                   .Select(r => r.Severity)
                   .DefaultIfEmpty(Severity.Info)
                   .Max();

        public double ComplianceScore =>
            TotalChecked == 0 ? 100.0
            : Math.Round((double)PassCount / TotalChecked * 100, 1);

        /// <summary>CI/CD gate — true means the build should pass.</summary>
        public bool Passed(Severity failThreshold = Severity.High) =>
            !Results.Any(r => r.Status == RuleStatus.Fail && r.Mandatory && r.Severity >= failThreshold);
    }
}
