using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SqlGuard.Reports.Sarif
{
    // ── SARIF 2.1.0 schema DTOs ───────────────────────────────────────────────────
    // Minimal subset required for GitHub Advanced Security.

    internal sealed class SarifLog
    {
        [JsonPropertyName("$schema")] public string Schema { get; init; } = null!;
        public string Version { get; init; } = null!;
        public List<SarifRun> Runs { get; init; } = [];
    }
}
