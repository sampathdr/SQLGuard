using Microsoft.Data.SqlClient;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Providers.SqlServer
{
    public sealed class SqlServerProvider : IDatabaseProvider
    {
        public DatabaseEngine Engine => DatabaseEngine.SqlServer;
        public string SupportedVersionRange => "SQL Server 2016 (13.x) – 2022 (16.x) / Azure SQL";

        public async Task<bool> TestConnectionAsync(DatabaseTarget target, CancellationToken ct = default)
        {
            try
            {
                await using var conn = new SqlConnection(BuildConnectionString(target));
                await conn.OpenAsync(ct);
                return true;
            }
            catch { return false; }
        }

        public async Task<IMetadataCollector> CreateCollectorAsync(DatabaseTarget target, CancellationToken ct = default)
        {
            var conn = new SqlConnection(BuildConnectionString(target));
            await conn.OpenAsync(ct);
            return new SqlServerMetadataCollector(conn);
        }

        internal static string BuildConnectionString(DatabaseTarget target)
        {
            if (!string.IsNullOrWhiteSpace(target.ConnectionStringOverride))
                return target.ConnectionStringOverride;

            var b = new SqlConnectionStringBuilder
            {
                DataSource = target.Port > 0 ? $"{target.Host},{target.Port}" : target.Host,
                InitialCatalog = target.Database,
                ConnectTimeout = target.TimeoutSeconds,
                Encrypt = true,
                TrustServerCertificate = true,  // allow override via ConnectionStringOverride
                ApplicationName = "SqlGuard"
            };

            if (target.Username is not null)
            {
                b.UserID = target.Username;
                b.Password = target.Password;
            }
            else
            {
                b.IntegratedSecurity = true;
            }
            return b.ConnectionString;
        }
    }    
}
