using SqlGuard.Core.Abstractions;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SqlGuard.Core.Engine
{
    /// <summary>
    /// Loads rule packs from:
    ///   1. A named built-in pack (embedded resources in any registered assembly)
    ///   2. A file system directory path
    ///   3. An explicit .yaml file path
    ///
    /// Pack naming convention: "sqlserver-core", "postgresql-cis", etc.
    /// </summary>
    public sealed class YamlRulePackLoader : IRulePackLoader
    {
        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private readonly List<Assembly> _rulePackAssemblies = [];

        public void RegisterAssembly(Assembly assembly) => _rulePackAssemblies.Add(assembly);

        public async Task<IReadOnlyList<RuleDefinition>> LoadAsync(string packNameOrPath, CancellationToken ct = default)
        {
            var yamlFiles = await ResolveYamlFilesAsync(packNameOrPath, ct);
            var rules = new List<RuleDefinition>();

            foreach (var (content, source) in yamlFiles)
            {
                try
                {
                    var fileRules = ParseRuleFile(content, packNameOrPath);
                    rules.AddRange(fileRules);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to parse rule file '{source}': {ex.Message}", ex);
                }
            }

            return rules;
        }

        public async Task<RulePackManifest?> LoadManifestAsync(string packNameOrPath, CancellationToken ct = default)
        {
            var manifestYaml = await TryLoadManifestYamlAsync(packNameOrPath, ct);
            if (manifestYaml is null) return null;

            return Deserializer.Deserialize<RulePackManifest>(manifestYaml);
        }

        private async Task<List<(string Content, string Source)>> ResolveYamlFilesAsync(
            string packNameOrPath, CancellationToken ct)
        {
            var results = new List<(string, string)>();

            // ── File system directory ────────────────────────────────────────────
            if (Directory.Exists(packNameOrPath))
            {
                foreach (var file in Directory.GetFiles(packNameOrPath, "*.yaml", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file).Equals("manifest.yaml", StringComparison.OrdinalIgnoreCase)) continue;
                    results.Add((await File.ReadAllTextAsync(file, ct), file));
                }
                return results;
            }

            // ── Explicit .yaml file ──────────────────────────────────────────────
            if (File.Exists(packNameOrPath) && packNameOrPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            {
                results.Add((await File.ReadAllTextAsync(packNameOrPath, ct), packNameOrPath));
                return results;
            }

            // ── Embedded resource in registered assemblies ───────────────────────
            foreach (var assembly in _rulePackAssemblies)
            {
                var prefix = $"SqlGuard.RulePacks.{packNameOrPath.Replace('-', '.')}";
                var names = assembly.GetManifestResourceNames()
                    .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                             && n.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                             && !n.EndsWith("manifest.yaml", StringComparison.OrdinalIgnoreCase));

                foreach (var name in names)
                {
                    using var stream = assembly.GetManifestResourceStream(name)!;
                    using var reader = new StreamReader(stream);
                    results.Add((await reader.ReadToEndAsync(ct), name));
                }
            }

            if (results.Count == 0)
                throw new FileNotFoundException(
                    $"Rule pack '{packNameOrPath}' not found. Expected a directory path, a .yaml file path, " +
                    $"or a named built-in pack (sqlserver-core, postgresql-cis, etc.).");

            return results;
        }

        private static IReadOnlyList<RuleDefinition> ParseRuleFile(string yaml, string packName)
        {
            // Support both a single rule document and a list of rules
            yaml = yaml.TrimStart();

            if (yaml.StartsWith("- ") || yaml.StartsWith("rules:"))
            {
                var wrapper = Deserializer.Deserialize<RuleFileWrapper>(yaml);
                return (wrapper?.Rules ?? [])
                    .Select(r => r with { Pack = r.Pack ?? packName })
                    .ToList();
            }

            var single = Deserializer.Deserialize<RuleDefinition>(yaml);
            return [single with { Pack = single.Pack ?? packName }];
        }

        private async Task<string?> TryLoadManifestYamlAsync(string packNameOrPath, CancellationToken ct)
        {
            var manifestPath = Path.Combine(packNameOrPath, "manifest.yaml");
            if (File.Exists(manifestPath))
                return await File.ReadAllTextAsync(manifestPath, ct);

            foreach (var assembly in _rulePackAssemblies)
            {
                var name = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.Contains(packNameOrPath.Replace('-', '.'), StringComparison.OrdinalIgnoreCase)
                                      && n.EndsWith("manifest.yaml", StringComparison.OrdinalIgnoreCase));
                if (name is null) continue;
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync(ct);
            }
            return null;
        }

        private sealed class RuleFileWrapper
        {
            public List<RuleDefinition>? Rules { get; set; }
        }
    }
}
