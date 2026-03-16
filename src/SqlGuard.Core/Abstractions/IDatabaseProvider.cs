using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Entry point for a specific database engine.
    /// Factory for a single database engine.
    /// </summary>
    public interface IDatabaseProvider
    {
        /// <summary>Database engine this provider supports.</summary>
        DatabaseEngine Engine { get; }

        /// <summary>Human-readable supported version range, e.g. "SQL Server 2016–2022".</summary>
        string SupportedVersionRange { get; }

        /// <summary>Tests that the target is reachable and credentials are valid.</summary>
        Task<bool> TestConnectionAsync(DatabaseTarget target, CancellationToken ct = default);

        /// <summary>
        /// Opens a connection and returns a collector.
        /// The returned object must also implement <see cref="IAsyncDisposable"/>.
        /// </summary>
        Task<IMetadataCollector> CreateCollectorAsync(DatabaseTarget target, CancellationToken ct = default);
    }
}
