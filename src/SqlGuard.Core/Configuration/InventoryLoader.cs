using SqlGuard.Core.DTOs;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlGuard.Core.Configuration
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // Inventory file schema — used with sqlguard scan --inventory servers.json
    //
    // Example JSON:
    // {
    //   "default_packs": ["sqlserver-core", "sqlserver-cis"],
    //   "servers": [
    //     {
    //       "label": "prod-db-1",
    //       "engine": "SqlServer",
    //       "host": "10.0.0.10",
    //       "database": "AppDb",
    //       "auth": { "type": "password", "username": "sqlguard", "password_env": "DB_PASS" }
    //     }
    //   ]
    // }
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses an inventory JSON file listing multiple database targets.
    /// All credential fields follow the same env-var-reference convention as sqlguard.yml.
    /// </summary>
    public sealed class InventoryLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private readonly ConfigLoader _configLoader;

        public InventoryLoader(ConfigLoader configLoader) => _configLoader = configLoader;

        /// <summary>Loads and resolves all targets from a JSON inventory file.</summary>
        public async Task<IReadOnlyList<InventoryTarget>> LoadAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new ConfigurationException($"Inventory file not found: {filePath}");

            await using var stream = File.OpenRead(filePath);
            var raw = await JsonSerializer.DeserializeAsync<InventoryFileDto>(stream, JsonOptions, ct)
                ?? throw new ConfigurationException("Inventory file is empty or invalid JSON.");

            var targets = new List<InventoryTarget>(raw.Servers.Count);

            foreach (var entry in raw.Servers)
            {
                var server = new ServerConfig
                {
                    Label = entry.Label ?? entry.Host,
                    Engine = entry.Engine,
                    Host = entry.Host,
                    Port = entry.Port,
                    Database = entry.Database,
                    TimeoutSeconds = entry.TimeoutSeconds,
                    Auth = entry.Auth ?? new AuthConfig()
                };

                var dbTarget = _configLoader.ResolveTarget(server);

                // Merge default packs with per-server overrides
                var packs = (entry.Packs?.Count > 0 ? entry.Packs : raw.DefaultPacks)
                            ?? ["sqlserver-core"];

                targets.Add(new InventoryTarget
                {
                    Label = server.Label ?? server.Host,
                    Target = dbTarget,
                    ScanOptions = new ScanOptions { Packs = packs }
                });
            }

            return targets;
        }
    }
}
