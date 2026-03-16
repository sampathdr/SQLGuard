using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    /// <summary>
    /// Parsed from YAML, fully declarative
    /// </summary>
    public sealed record RuleDefinition
    {
        public required string Id { get; init; }               // e.g. "MSSQL-CORE-001"
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required string Category { get; init; }               // e.g. "ServerSecurity"
        public required Severity Severity { get; init; }
        public required DatabaseEngine Engine { get; init; }
        public required string Query { get; init; }               // T-SQL / PL/pgSQL
        public required RuleOperator Operator { get; init; }
        public object? Expected { get; init; }               // fixed expected value
        public IReadOnlyList<object>? ValidValues { get; init; }            // multiple acceptable values
        public IReadOnlyDictionary<string, object>? VersionValidValues { get; init; } // keyed by version prefix
        public bool Mandatory { get; init; } = true;       // false → WARN instead of FAIL
        public string? DynamicExpectedFunction { get; init; }               // C# expression for runtime eval
        public string? Remediation { get; init; }
        public string? Notes { get; init; }
        public IReadOnlyList<string> ComplianceReferences { get; init; } = [];
        public IReadOnlyList<string> Tags { get; init; } = [];
        public string? MinVersion { get; init; }               // skip rule below this version
        public string? MaxVersion { get; init; }               // skip rule above this version
        public bool Enabled { get; init; } = true;
        public string? Pack { get; init; }               // which rule pack this belongs to
    }
}
