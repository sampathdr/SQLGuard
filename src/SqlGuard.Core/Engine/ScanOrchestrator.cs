using Microsoft.Extensions.Logging;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Engine
{
    /// <summary>
    /// Coordinates a complete scan pipeline:
    ///   Load packs → Connect → Collect → Evaluate (parallel) → Aggregate → Return
    /// </summary>
    public sealed class ScanOrchestrator : IScanOrchestrator
    {
        private readonly IEnumerable<IDatabaseProvider> _providers;
        private readonly IRulePackLoader _packLoader;
        private readonly IEvaluationEngine _evaluationEngine;
        private readonly ILogger<ScanOrchestrator> _logger;

        public ScanOrchestrator(
            IEnumerable<IDatabaseProvider> providers,
            IRulePackLoader packLoader,
            IEvaluationEngine evaluationEngine,
            ILogger<ScanOrchestrator> logger)
        {
            _providers = providers;
            _packLoader = packLoader;
            _evaluationEngine = evaluationEngine;
            _logger = logger;
        }

        public async Task<ScanResult> ScanAsync(
            DatabaseTarget target,
            ScanOptions options,
            IProgress<ScanProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
            var startedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Starting scan: {Engine} @ {Host}/{Database}", target.Engine, target.Host, target.Database);

            // ── 1. Resolve provider ──────────────────────────────────────────────
            var provider = _providers.FirstOrDefault(p => p.Engine == target.Engine)
                ?? throw new InvalidOperationException($"No provider registered for engine '{target.Engine}'. Install the SqlGuard.Providers.{target.Engine} package.");

            // ── 2. Test connection ───────────────────────────────────────────────
            _logger.LogDebug("Testing connection to {Host}", target.Host);
            if (!await provider.TestConnectionAsync(target, ct))
                throw new InvalidOperationException($"Cannot connect to {target.Engine} at {target.Host}:{target.Port}. Check credentials and network.");

            // ── 3. Create collector ──────────────────────────────────────────────
            await using var collector = (IAsyncDisposable)await provider.CreateCollectorAsync(target, ct);
            var metaCollector = (IMetadataCollector)collector;

            _logger.LogInformation("Connected. Server version: {Version}", metaCollector.ServerVersion);

            // ── 4. Load rule packs ───────────────────────────────────────────────
            var allRules = new List<RuleDefinition>();
            var packsLoaded = new List<string>();

            foreach (var pack in options.Packs)
            {
                var rules = await _packLoader.LoadAsync(pack, ct);
                var filtered = rules.Where(r => r.Engine == target.Engine && r.Enabled).ToList();
                allRules.AddRange(filtered);
                packsLoaded.Add(pack);
                _logger.LogDebug("Loaded pack '{Pack}': {Count} rules for {Engine}", pack, filtered.Count, target.Engine);
            }

            // ── 5. Apply scan options filters ────────────────────────────────────
            var rulesToRun = ApplyFilters(allRules, options);
            _logger.LogInformation("Running {Count} rules (from {Total} loaded)", rulesToRun.Count, allRules.Count);

            // ── 6. Evaluate rules (bounded parallel) ─────────────────────────────
            var results = new EvaluationResult[rulesToRun.Count];
            int completed = 0;

            var semaphore = new SemaphoreSlim(options.MaxConcurrency);

            var tasks = rulesToRun.Select(async (rule, index) =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var result = await _evaluationEngine.EvaluateAsync(rule, metaCollector, ct);
                    results[index] = result;

                    var current = Interlocked.Increment(ref completed);
                    progress?.Report(new ScanProgressUpdate
                    {
                        RuleId = rule.Id,
                        RuleTitle = rule.Title,
                        Current = current,
                        Total = rulesToRun.Count,
                        Status = result.Status
                    });

                    _logger.LogDebug("[{Status}] {RuleId} — {Title}", result.Status, rule.Id, rule.Title);

                    if (options.FailFast && result.Status == RuleStatus.Fail && result.Severity >= options.FailOnSeverity)
                        throw new OperationCanceledException($"FailFast triggered by rule {rule.Id}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var scanResult = new ScanResult
            {
                Engine = target.Engine.ToString(),
                Host = target.Host,
                Database = target.Database,
                Label = target.Label,
                ServerVersion = metaCollector.ServerVersion,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                PacksLoaded = packsLoaded,
                Results = results.Where(r => r is not null).ToList()
            };

            _logger.LogInformation(
                "Scan complete in {Duration:F1}s — Pass:{Pass} Fail:{Fail} Warn:{Warn} Error:{Error} | Score: {Score}%",
                scanResult.Duration.TotalSeconds,
                scanResult.PassCount, scanResult.FailCount,
                scanResult.WarnCount, scanResult.ErrorCount,
                scanResult.ComplianceScore);

            return scanResult;
        }

        private static List<RuleDefinition> ApplyFilters(List<RuleDefinition> rules, ScanOptions options)
        {
            var q = rules.AsEnumerable();

            if (options.IncludeRuleIds?.Count > 0)
                q = q.Where(r => options.IncludeRuleIds.Contains(r.Id));

            if (options.ExcludeRuleIds?.Count > 0)
                q = q.Where(r => !options.ExcludeRuleIds.Contains(r.Id));

            if (options.Categories?.Count > 0)
                q = q.Where(r => options.Categories.Contains(r.Category, StringComparer.OrdinalIgnoreCase));

            if (options.MandatoryOnly)
                q = q.Where(r => r.Mandatory);

            q = q.Where(r => r.Severity >= options.MinSeverity);

            return q.OrderBy(r => r.Id).ToList();
        }
    }
}
