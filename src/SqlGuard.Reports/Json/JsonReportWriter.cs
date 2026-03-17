using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlGuard.Reports.Json
{
    /// <summary>
    /// Writes a structured JSON report suitable for automation, dashboards,
    /// and security platform integrations.
    ///
    /// Schema is intentionally stable and follows the documented JSON contract.
    /// Breaking changes require a major version bump.
    /// </summary>
    public sealed class JsonReportWriter : IReportWriter
    {
        public ReportFormat Format => ReportFormat.Json;
        public string FileExtension => ".json";
        public string ContentType => "application/json";

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public async Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default)
        {
            var dto = Map(result);
            await JsonSerializer.SerializeAsync(output, dto, Options, ct);
        }

        // ── DTO mapping (separates wire format from domain model) ─────────────────

        private static ScanReportDto Map(ScanResult result) => new()
        {
            SchemaVersion = "1",
            ScanId = result.ScanId,
            GeneratedAt = DateTimeOffset.UtcNow,
            Server = new ServerInfoDto
            {
                Label = result.Label,
                Engine = result.Engine,
                Host = result.Host,
                Database = result.Database,
                Version = result.ServerVersion,
            },
            Scan = new ScanMetaDto
            {
                StartedAt = result.StartedAt,
                CompletedAt = result.CompletedAt,
                DurationMs = (long)result.Duration.TotalMilliseconds,
                PacksLoaded = result.PacksLoaded.ToList()
            },
            Summary = new SummaryDto
            {
                ComplianceScore = result.ComplianceScore,
                TotalChecked = result.TotalChecked,
                Pass = result.PassCount,
                Fail = result.FailCount,
                MandatoryFail = result.MandatoryFailCount,
                Warn = result.WarnCount,
                Error = result.ErrorCount,
                Skipped = result.SkippedCount,
                HighestSeverity = result.HighestSeverity.ToString().ToLowerInvariant(),
                Passed = result.MandatoryFailCount == 0
            },
            Results = result.Results
                .OrderByDescending(r => r.Severity)
                .ThenBy(r => r.RuleId)
                .Select(MapResult)
                .ToList()
        };

        private static ResultDto MapResult(EvaluationResult r) => new()
        {
            RuleId = r.RuleId,
            Title = r.Title,
            Category = r.Category,
            Severity = r.Severity.ToString().ToLowerInvariant(),
            Status = r.Status.ToString().ToLowerInvariant(),
            Mandatory = r.Mandatory,
            Pack = r.Pack,
            Detail = r.Detail,
            ActualValue = r.ActualValue?.ToString(),
            Remediation = r.Remediation,
            ComplianceReferences = r.ComplianceReferences.ToList(),
            EvaluatedAt = r.EvaluatedAt,
            DurationMs = (long)r.Duration.TotalMilliseconds,
            Error = r.Error?.Message
        };
    }
}
