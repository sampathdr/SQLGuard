using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlGuard.Reports.Sarif
{
    /// <summary>
    /// Writes a SARIF 2.1.0 report for integration with GitHub Advanced Security,
    /// Azure DevOps, and any other SARIF-compatible security dashboard.
    ///
    /// SARIF spec: https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html
    ///
    /// GitHub Advanced Security usage:
    ///   - Upload as a code-scanning artifact
    ///   - Results appear in the Security → Code scanning tab
    ///   - Failed rules show as open alerts; passing rules are not reported
    /// </summary>
    public sealed class SarifReportWriter : IReportWriter
    {
        public ReportFormat Format => ReportFormat.Sarif;
        public string FileExtension => ".sarif";
        public string ContentType => "application/sarif+json";

        private const string SarifVersion = "2.1.0";
        private const string SarifSchemaUri = "https://json.schemastore.org/sarif-2.1.0.json";
        private const string ToolName = "SqlGuard";
        private const string ToolInfoUri = "https://github.com/your-org/sqlguard";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default)
        {
            var sarif = BuildSarifLog(result);
            await JsonSerializer.SerializeAsync(output, sarif, JsonOptions, ct);
        }

        private static SarifLog BuildSarifLog(ScanResult result)
        {
            // Map each unique rule to a SARIF ReportingDescriptor
            var ruleDescriptors = result.Results
                .GroupBy(r => r.RuleId)
                .Select(g => BuildRuleDescriptor(g.First()))
                .ToList();

            // Map each failing/warning result to a SARIF Result
            var sarifResults = result.Results
                .Where(r => r.Status is RuleStatus.Fail or RuleStatus.Warn or RuleStatus.Error)
                .Select(r => BuildSarifResult(r, result))
                .ToList();

            return new SarifLog
            {
                Schema = SarifSchemaUri,
                Version = SarifVersion,
                Runs =
                [
                    new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new ToolComponent
                        {
                            Name            = ToolName,
                            InformationUri  = ToolInfoUri,
                            SemanticVersion = GetToolVersion(),
                            Rules           = ruleDescriptors
                        }
                    },
                    Results       = sarifResults,
                    Invocations   = [BuildInvocation(result)],
                    Properties    = new Dictionary<string, object?>
                    {
                        ["sqlguard/scanId"]          = result.ScanId.ToString(),
                        ["sqlguard/engine"]          = result.Engine,
                        ["sqlguard/host"]            = result.Host,
                        ["sqlguard/database"]        = result.Database,
                        ["sqlguard/complianceScore"] = result.ComplianceScore,
                        ["sqlguard/serverVersion"]   = result.ServerVersion
                    }
                }
                ]
            };
        }

        private static ReportingDescriptor BuildRuleDescriptor(EvaluationResult r) => new()
        {
            Id = r.RuleId,
            Name = SanitizeName(r.Title),
            ShortDescription = new MultiformatMessageString { Text = r.Title },
            FullDescription = new MultiformatMessageString { Text = r.Detail ?? r.Title },
            DefaultConfiguration = new ReportingConfiguration
            {
                Level = SarifLevel(r.Severity)
            },
            Properties = new Dictionary<string, object?>
            {
                ["category"] = r.Category,
                ["mandatory"] = r.Mandatory,
                ["pack"] = r.Pack,
                ["compliance"] = r.ComplianceReferences
            },
            HelpUri = $"{ToolInfoUri}/blob/main/docs/rules/{r.RuleId}.md"
        };

        private static SarifResult BuildSarifResult(EvaluationResult r, ScanResult scan) => new()
        {
            RuleId = r.RuleId,
            Level = r.Status == RuleStatus.Warn ? "warning" : SarifLevel(r.Severity),
            Message = new Message { Text = BuildMessage(r) },
            Locations =
            [
                new Location
            {
                LogicalLocations =
                [
                    new LogicalLocation
                    {
                        Name           = $"{scan.Host}/{scan.Database}",
                        Kind           = "database",
                        FullyQualifiedName = $"{scan.Engine}://{scan.Host}/{scan.Database}"
                    }
                ]
            }
            ],
            Properties = new Dictionary<string, object?>
            {
                ["mandatory"] = r.Mandatory,
                ["actualValue"] = r.ActualValue?.ToString(),
                ["category"] = r.Category
            }
        };

        private static Invocation BuildInvocation(ScanResult result) => new()
        {
            ExecutionSuccessful = true,
            StartTimeUtc = result.StartedAt.UtcDateTime,
            EndTimeUtc = result.CompletedAt.UtcDateTime
        };

        private static string BuildMessage(EvaluationResult r)
        {
            var parts = new List<string> { r.Title };
            if (!string.IsNullOrWhiteSpace(r.Detail)) parts.Add(r.Detail);
            if (!string.IsNullOrWhiteSpace(r.Remediation)) parts.Add($"Remediation: {r.Remediation}");
            if (r.ComplianceReferences.Count > 0)
                parts.Add($"References: {string.Join(", ", r.ComplianceReferences)}");
            return string.Join(" | ", parts);
        }

        private static string SarifLevel(Severity severity) => severity switch
        {
            Severity.Critical or Severity.High => "error",
            Severity.Medium => "warning",
            _ => "note"
        };

        private static string SanitizeName(string title) =>
            new string(title.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());

        private static string GetToolVersion()
        {
            var asm = typeof(SarifReportWriter).Assembly;
            return asm.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }
}
