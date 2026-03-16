using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlGuard.Core.Evaluation
{
    /// <summary>
    /// Stateless evaluation engine. Executes a RuleDefinition against a live
    /// IMetadataCollector and returns a structured EvaluationResult.
    ///
    /// Evaluation order:
    ///   1. Version gate (MinVersion / MaxVersion) → NotApplicable if outside range
    ///   2. Collect actual value via SQL query
    ///   3. Resolve expected value:
    ///        a. DynamicExpectedFunction  (if registered resolver handles it)
    ///        b. VersionValidValues       (version-keyed dictionary)
    ///        c. ValidValues list         (multiple acceptable values)
    ///        d. Expected                 (fixed scalar)
    ///   4. Apply RuleOperator
    ///   5. Map pass/fail → RuleStatus respecting mandatory flag
    /// </summary>
    public sealed class EvaluationEngine : IEvaluationEngine
    {
        private readonly IEnumerable<IDynamicExpectedResolver> _dynamicResolvers;

        public EvaluationEngine(IEnumerable<IDynamicExpectedResolver> dynamicResolvers)
            => _dynamicResolvers = dynamicResolvers;

        public async Task<EvaluationResult> EvaluateAsync(
            RuleDefinition rule,
            IMetadataCollector collector,
            CancellationToken ct = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // ── 1. Version gate ──────────────────────────────────────────────
                if (!IsVersionInRange(rule, collector.ServerVersion))
                {
                    return Build(rule, RuleStatus.NotApplicable, null, null,
                        $"Rule not applicable for version {collector.ServerVersion}", sw.Elapsed);
                }

                // ── 2. Collect actual value ──────────────────────────────────────
                object? actual = await CollectAsync(rule, collector, ct);

                // ── 3. Resolve expected value ────────────────────────────────────
                (object? expected, IReadOnlyList<object>? validValues) =
                    await ResolveExpectedAsync(rule, collector, ct);

                // ── 4. Apply operator ────────────────────────────────────────────
                bool passed = validValues?.Count > 0
                    ? validValues.Any(v => Compare(rule.Operator, actual, v))
                    : Compare(rule.Operator, actual, expected);

                // ── 5. Map to status ─────────────────────────────────────────────
                RuleStatus status = passed
                    ? RuleStatus.Pass
                    : rule.Mandatory ? RuleStatus.Fail : RuleStatus.Warn;

                string? detail = passed
                    ? $"Actual: {Format(actual)}"
                    : $"Actual: {Format(actual)}  |  Expected: {FormatExpected(expected, validValues)}";

                return Build(rule, status, actual, expected, detail, sw.Elapsed);
            }
            catch (Exception ex)
            {
                return Build(rule, RuleStatus.Error, null, null,
                    $"Evaluation error: {ex.Message}", sw.Elapsed, ex);
            }
        }

        // ── Collect ──────────────────────────────────────────────────────────────

        private static async Task<object?> CollectAsync(
            RuleDefinition rule, IMetadataCollector collector, CancellationToken ct)
        {
            // Multi-row queries return first column of first row as scalar
            var rows = await collector.QueryAsync(rule.Query, ct);
            if (rows.Count == 0) return null;
            var first = rows[0];
            return first.Values.FirstOrDefault();
        }

        // ── Expected resolution ──────────────────────────────────────────────────

        private async Task<(object? Expected, IReadOnlyList<object>? ValidValues)> ResolveExpectedAsync(
            RuleDefinition rule, IMetadataCollector collector, CancellationToken ct)
        {
            // a. Dynamic function
            if (!string.IsNullOrWhiteSpace(rule.DynamicExpectedFunction))
            {
                var resolver = _dynamicResolvers.FirstOrDefault(r => r.CanResolve(rule.DynamicExpectedFunction));
                if (resolver is not null)
                {
                    var val = await resolver.ResolveAsync(rule.DynamicExpectedFunction, collector, ct);
                    return (val, null);
                }
            }

            // b. Version-aware valid values
            if (rule.VersionValidValues?.Count > 0)
            {
                var versionKey = rule.VersionValidValues.Keys
                    .FirstOrDefault(k => collector.ServerVersion.StartsWith(k, StringComparison.OrdinalIgnoreCase));
                if (versionKey is not null)
                {
                    var versionExpected = rule.VersionValidValues[versionKey];
                    return (versionExpected, null);
                }
            }

            // c. Multiple valid values
            if (rule.ValidValues?.Count > 0)
                return (null, rule.ValidValues);

            // d. Fixed expected
            return (rule.Expected, null);
        }

        // ── Comparison ───────────────────────────────────────────────────────────

        private static bool Compare(RuleOperator op, object? actual, object? expected)
        {
            if (actual is null && expected is null) return op == RuleOperator.Equals;
            if (actual is null || expected is null) return op == RuleOperator.NotEquals;

            string actualStr = actual.ToString()!;
            string expectedStr = expected.ToString()!;

            return op switch
            {
                RuleOperator.Equals => string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
                RuleOperator.NotEquals => !string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
                RuleOperator.GreaterThan => CompareNumeric(actual, expected) > 0,
                RuleOperator.LessThan => CompareNumeric(actual, expected) < 0,
                RuleOperator.Contains => actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
                RuleOperator.NotContains => !actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
                RuleOperator.In => expectedStr.Split(',').Any(v => string.Equals(v.Trim(), actualStr, StringComparison.OrdinalIgnoreCase)),
                RuleOperator.NotIn => !expectedStr.Split(',').Any(v => string.Equals(v.Trim(), actualStr, StringComparison.OrdinalIgnoreCase)),
                RuleOperator.Regex => Regex.IsMatch(actualStr, expectedStr, RegexOptions.IgnoreCase),
                _ => false   // Custom operator — handled by IDynamicExpectedResolver
            };
        }

        private static int CompareNumeric(object a, object b)
        {
            if (double.TryParse(a.ToString(), out double da) && double.TryParse(b.ToString(), out double db))
                return da.CompareTo(db);
            return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        // ── Version range ─────────────────────────────────────────────────────────

        private static bool IsVersionInRange(RuleDefinition rule, string serverVersion)
        {
            if (rule.MinVersion is not null && string.Compare(serverVersion, rule.MinVersion, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (rule.MaxVersion is not null && string.Compare(serverVersion, rule.MaxVersion, StringComparison.OrdinalIgnoreCase) > 0)
                return false;
            return true;
        }

        // ── Result factory ────────────────────────────────────────────────────────

        private static EvaluationResult Build(
            RuleDefinition rule, RuleStatus status,
            object? actual, object? expected,
            string? detail, TimeSpan duration,
            Exception? ex = null) => new()
            {
                RuleId = rule.Id,
                Title = rule.Title,
                Status = status,
                Severity = rule.Severity,
                Mandatory = rule.Mandatory,
                ActualValue = actual,
                ExpectedValue = expected,
                Detail = detail,
                Remediation = rule.Remediation,
                ComplianceReferences = rule.ComplianceReferences,
                Pack = rule.Pack,
                Category = rule.Category,
                Duration = duration,
                Error = ex
            };

        private static string Format(object? v) => v?.ToString() ?? "(null)";
        private static string FormatExpected(object? expected, IReadOnlyList<object>? validValues) =>
            validValues?.Count > 0
                ? $"one of [{string.Join(", ", validValues)}]"
                : Format(expected);
    }
}
