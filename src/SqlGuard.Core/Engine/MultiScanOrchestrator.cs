using Microsoft.Extensions.Logging;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Engine
{
    /// <summary>
    /// Scans multiple database targets from an inventory file sequentially.
    /// Each server gets its own <see cref="ScanResult"/>; failures on one server
    /// do not abort subsequent servers.
    /// </summary>
    public sealed class MultiScanOrchestrator : IMultiScanOrchestrator
    {
        private readonly IScanOrchestrator _singleScanner;
        private readonly ILogger<MultiScanOrchestrator> _logger;

        public MultiScanOrchestrator(IScanOrchestrator singleScanner, ILogger<MultiScanOrchestrator> logger)
        {
            _singleScanner = singleScanner;
            _logger = logger;
        }

        public async Task<MultiScanResult> ScanAllAsync(
            IReadOnlyList<InventoryTarget> targets,
            ScanOptions defaultOptions,
            IProgress<MultiScanProgressUpdate>? progress = null,
            CancellationToken ct = default)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var results = new List<ScanResult>(targets.Count);

            _logger.LogInformation("Starting multi-server scan: {Count} target(s)", targets.Count);

            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var inventoryTarget = targets[i];
                _logger.LogInformation("Scanning [{Current}/{Total}] {Label}",
                    i + 1, targets.Count, inventoryTarget.Label);

                // Per-server scan options override default; fall back to default
                var scanOptions = inventoryTarget.ScanOptions ?? defaultOptions;

                // Forward rule-level progress with server context
                IProgress<ScanProgressUpdate>? ruleProgress = progress is null ? null
                    : new Progress<ScanProgressUpdate>(update =>
                        progress.Report(new MultiScanProgressUpdate
                        {
                            ServerLabel = inventoryTarget.Label,
                            ServerCurrent = i + 1,
                            ServerTotal = targets.Count,
                            RuleUpdate = update
                        }));

                try
                {
                    var result = await _singleScanner.ScanAsync(
                        inventoryTarget.Target, scanOptions, ruleProgress, ct);
                    results.Add(result);

                    _logger.LogInformation(
                        "[{Label}] Score: {Score}%  Fail: {Fail}  Pass: {Pass}",
                        inventoryTarget.Label, result.ComplianceScore,
                        result.FailCount, result.PassCount);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Surface as a ScanResult with a single Error result — don't abort the batch
                    _logger.LogError(ex, "Scan failed for {Label}: {Message}", inventoryTarget.Label, ex.Message);
                    results.Add(new ScanResult
                    {
                        Engine = inventoryTarget.Target.Engine.ToString(),
                        Host = inventoryTarget.Target.Host,
                        Database = inventoryTarget.Target.Database,
                        Label = inventoryTarget.Label,
                        StartedAt = DateTimeOffset.UtcNow,
                        CompletedAt = DateTimeOffset.UtcNow,
                        Results = [new EvaluationResult
                    {
                        RuleId    = "SCAN-ERROR",
                        Title     = "Scan execution failed",
                        Status    = RuleStatus.Error,
                        Severity  = Severity.Critical,
                        Mandatory = true,
                        Detail    = ex.Message,
                        Error     = ex
                    }]
                    });
                }
            }

            _logger.LogInformation(
                "Multi-scan complete. Servers: {Total}  Failed: {Failed}  Total mandatory fails: {Fails}",
                results.Count,
                results.Count(r => r.MandatoryFailCount > 0),
                results.Sum(r => r.MandatoryFailCount));

            return new MultiScanResult
            {
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
                Results = results
            };
        }
    }
}
