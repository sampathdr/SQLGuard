using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Json
{
    internal sealed class ResultDto
    {
        public string RuleId { get; init; } = null!;
        public string Title { get; init; } = null!;
        public string? Category { get; init; }
        public string Severity { get; init; } = null!;
        public string Status { get; init; } = null!;
        public bool Mandatory { get; init; }
        public string? Pack { get; init; }
        public string? Detail { get; init; }
        public string? ActualValue { get; init; }
        public string? Remediation { get; init; }
        public List<string> ComplianceReferences { get; init; } = [];
        public DateTimeOffset EvaluatedAt { get; init; }
        public long DurationMs { get; init; }
        public string? Error { get; init; }
    }
}
