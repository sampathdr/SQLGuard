using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Coordinates a complete single-server scan
    /// Runs the full pipeline: load packs → connect → filter → evaluate → aggregate.
    /// </summary>
    public interface IScanOrchestrator
    {
        Task<ScanResult> ScanAsync(
            DatabaseTarget target,
            ScanOptions options,
            IProgress<ScanProgressUpdate>? progress = null,
            CancellationToken ct = default);
    }
}
