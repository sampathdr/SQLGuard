using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Transforms a <see cref="ScanResult"/> into a specific output format.
    /// Each implementation is stateless and independently unit-testable.
    /// </summary>
    public interface IReportWriter
    {
        /// <summary>The output format this writer produces.</summary>
        ReportFormat Format { get; }

        /// <summary>File extension including the leading dot, e.g. <c>.json</c>.</summary>
        string FileExtension { get; }

        /// <summary>MIME content type, e.g. <c>application/json</c>.</summary>
        string ContentType { get; }

        /// <summary>
        /// Writes the report to <paramref name="output"/>.
        /// The caller owns the stream lifecycle; the writer must not close it.
        /// Console writer writes to stdout and ignores the stream.
        /// </summary>
        Task WriteAsync(ScanResult result, Stream output, CancellationToken ct = default);
    }
}
