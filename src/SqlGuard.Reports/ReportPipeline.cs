using Microsoft.Extensions.Logging;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports
{
    /// <summary>
    /// Coordinates all registered <see cref="IReportWriter"/> instances.
    ///
    /// Responsibilities:
    ///   • Ensures output directory exists before writing
    ///   • Console format → stdout (no file created)
    ///   • All other formats → {outputDirectory}/{fileBaseName}{extension}
    ///   • File writers run in parallel after the console is rendered
    ///   • Never throws on a single writer failure; logs and continues
    /// </summary>
    public sealed class ReportPipeline : IReportPipeline
    {
        private readonly IEnumerable<IReportWriter> _writers;
        private readonly ILogger<ReportPipeline> _logger;

        public ReportPipeline(IEnumerable<IReportWriter> writers, ILogger<ReportPipeline> logger)
        {
            _writers = writers;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task RunAsync(
            ScanResult result,
            IReadOnlyList<ReportFormat> formats,
            string? outputDirectory,
            string fileBaseName = "sqlguard-report",
            CancellationToken ct = default)
        {
            // ── 1. Console first (synchronous, immediate feedback) ────────────────
            if (formats.Contains(ReportFormat.Console))
            {
                var consoleWriter = _writers.FirstOrDefault(w => w.Format == ReportFormat.Console);
                if (consoleWriter is not null)
                    await consoleWriter.WriteAsync(result, Stream.Null, ct);  // console writes to stdout directly
            }

            // ── 2. File-based writers in parallel ─────────────────────────────────
            var fileFormats = formats.Where(f => f != ReportFormat.Console).ToList();
            if (fileFormats.Count == 0) return;

            var outDir = string.IsNullOrWhiteSpace(outputDirectory) ? "." : outputDirectory;
            EnsureDirectory(outDir);

            var tasks = fileFormats.Select(format => WriteFileAsync(result, format, outDir, fileBaseName, ct));
            await Task.WhenAll(tasks);
        }

        private async Task WriteFileAsync(
            ScanResult result, ReportFormat format,
            string outDir, string fileBaseName, CancellationToken ct)
        {
            var writer = _writers.FirstOrDefault(w => w.Format == format);
            if (writer is null)
            {
                _logger.LogWarning("No writer registered for format {Format}. Skipping.", format);
                return;
            }

            var fileName = $"{fileBaseName}{writer.FileExtension}";
            var filePath = Path.Combine(outDir, fileName);

            try
            {
                await using var stream = new FileStream(
                    filePath, FileMode.Create, FileAccess.Write,
                    FileShare.None, bufferSize: 65536, useAsync: true);

                await writer.WriteAsync(result, stream, ct);
                await stream.FlushAsync(ct);

                _logger.LogInformation("Report written: {FilePath} ({Format})", filePath, format);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to write {Format} report to {FilePath}", format, filePath);
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
