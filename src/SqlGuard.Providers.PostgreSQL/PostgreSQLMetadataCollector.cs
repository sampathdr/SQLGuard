using Npgsql;
using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Providers.PostgreSQL
{
    internal sealed class PostgreSQLMetadataCollector : IMetadataCollector, IAsyncDisposable
    {
        private readonly NpgsqlConnection _conn;
        private string? _serverVersion;

        public PostgreSQLMetadataCollector(NpgsqlConnection conn) => _conn = conn;

        public DatabaseEngine Engine => DatabaseEngine.PostgreSQL;

        public string ServerVersion
        {
            get
            {
                if (_serverVersion is not null) return _serverVersion;
                _serverVersion = _conn.ServerVersion ?? "0.0.0";
                return _serverVersion;
            }
        }

        public int MajorVersion
        {
            get
            {
                if (int.TryParse(ServerVersion.Split('.')[0], out int major)) return major;
                return 0;
            }
        }

        public async Task<T?> QueryScalarAsync<T>(string sql, CancellationToken ct = default)
        {
            await using var cmd = new NpgsqlCommand(sql, _conn) { CommandTimeout = 30 };
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is null or DBNull) return default;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
            string sql, CancellationToken ct = default)
        {
            await using var cmd = new NpgsqlCommand(sql, _conn) { CommandTimeout = 30 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            var rows = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }

        public ValueTask DisposeAsync() => _conn.DisposeAsync();
    }
}
