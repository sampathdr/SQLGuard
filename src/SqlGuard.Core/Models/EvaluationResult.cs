using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    /// <summary>
    /// Result of one rule against one database
    /// </summary>
    public sealed record EvaluationResult
    {
        public required string RuleId { get; init; }
        public required string Title { get; init; }
        public required RuleStatus Status { get; init; }
        public required Severity Severity { get; init; }
        public required bool Mandatory { get; init; }
        public object? ActualValue { get; init; }
        public object? ExpectedValue { get; init; }
        public string? Detail { get; init; }
        public string? Remediation { get; init; }
        public IReadOnlyList<string> ComplianceReferences { get; init; } = [];
        public string? Pack { get; init; }
        public string? Category { get; init; }
        public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
        public TimeSpan Duration { get; init; }
        public Exception? Error { get; init; }
    }
}
