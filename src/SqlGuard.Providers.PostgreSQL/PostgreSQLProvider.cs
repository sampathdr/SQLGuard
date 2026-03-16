using Npgsql;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Providers.PostgreSQL
{
    public sealed class PostgreSQLProvider : IDatabaseProvider
    {
        public DatabaseEngine Engine => DatabaseEngine.PostgreSQL;
        public string SupportedVersionRange => "PostgreSQL 13 – 17";

        public async Task<bool> TestConnectionAsync(DatabaseTarget target, CancellationToken ct = default)
        {
            try
            {
                await using var conn = new NpgsqlConnection(BuildConnectionString(target));
                await conn.OpenAsync(ct);
                return true;
            }
            catch { return false; }
        }

        public async Task<IMetadataCollector> CreateCollectorAsync(DatabaseTarget target, CancellationToken ct = default)
        {
            var conn = new NpgsqlConnection(BuildConnectionString(target));
            await conn.OpenAsync(ct);
            return new PostgreSQLMetadataCollector(conn);
        }

        internal static string BuildConnectionString(DatabaseTarget target)
        {
            if (!string.IsNullOrWhiteSpace(target.ConnectionStringOverride))
                return target.ConnectionStringOverride;

            var b = new NpgsqlConnectionStringBuilder
            {
                Host = target.Host,
                Port = target.Port > 0 ? target.Port : 5432,
                Database = target.Database,
                Username = target.Username,
                Password = target.Password,
                Timeout = target.TimeoutSeconds,
                ApplicationName = "SqlGuard",
                SslMode = SslMode.Prefer
            };
            return b.ConnectionString;
        }
    }
}
