using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Dispatches one ScanResult to multiple IReportWriter instances
    /// Renders all configured formats from a single scan result.
    /// Console is rendered synchronously first; file writers run in parallel.
    /// A failure in one writer is logged but never cancels the others.
    /// </summary>
    public interface IReportPipeline
    {
        Task RunAsync(
            ScanResult result,
            IReadOnlyList<ReportFormat> formats,
            string? outputDirectory,
            string fileBaseName = "sqlguard-report",
            CancellationToken ct = default);
    }
}
