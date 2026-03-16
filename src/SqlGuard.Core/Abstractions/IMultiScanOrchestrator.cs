using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Scans multiple servers from an inventory.
    /// Scans all targets in an inventory sequentially.
    /// A failure on one server is recorded and does not abort subsequent servers.
    /// </summary>
    public interface IMultiScanOrchestrator
    {
        Task<MultiScanResult> ScanAllAsync(
            IReadOnlyList<InventoryTarget> targets,
            ScanOptions defaultOptions,
            IProgress<MultiScanProgressUpdate>? progress = null,
            CancellationToken ct = default);
    }
}
