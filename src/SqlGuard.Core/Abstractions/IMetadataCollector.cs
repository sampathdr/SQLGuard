using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Abstraction over a live database connection used by the EvaluationEngine.
    /// Fetches raw data from a live DB connection.
    /// Isolated per engine; Rules never talk to the DB directly.
    /// </summary>
    public interface IMetadataCollector
    {
        /// <summary>Engine this collector is connected to.</summary>
        DatabaseEngine Engine { get; }

        /// <summary>Detected server version string, e.g. "16.0.4175.2".</summary>
        string ServerVersion { get; }

        /// <summary>Major version integer for version-aware rule branching.</summary>
        int MajorVersion { get; }

        /// <summary>Execute a scalar query; returns first column of first row, or default.</summary>
        Task<T?> QueryScalarAsync<T>(string sql, CancellationToken ct = default);

        /// <summary>Execute a query; returns all rows as ordered column-to-value dictionaries.</summary>
        Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            string sql, CancellationToken ct = default);
    }
}
