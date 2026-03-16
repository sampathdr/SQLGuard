using Microsoft.Extensions.Logging;
using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SqlGuard.Core.Configuration
{
    /// <summary>
    /// Loads and validates sqlguard.yml configuration files.
    ///
    /// Security contract:
    ///   • Resolves ${ENV_VAR} placeholders — the config file itself never contains secrets
    ///   • Validates that no literal passwords appear in the parsed config
    ///   • Logs a redacted view (never logs actual credential values)
    ///   • Supports config file schema validation before attempting a connection
    /// </summary>
    public sealed class ConfigLoader
    {
        // Matches ${VAR_NAME} or $VAR_NAME patterns
        private static readonly Regex EnvVarPattern =
            new(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|\$(?<name>[A-Za-z_][A-Za-z0-9_]*)",
                RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private readonly ILogger<ConfigLoader> _logger;

        public ConfigLoader(ILogger<ConfigLoader> logger) => _logger = logger;

        /// <summary>
        /// Loads configuration from a YAML file.
        /// Resolves all ${ENV_VAR} references from the process environment.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown for invalid config or missing required env vars.</exception>
        public SqlGuardConfig LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new ConfigurationException($"Config file not found: {filePath}");

            _logger.LogDebug("Loading config from {FilePath}", filePath);

            var rawYaml = File.ReadAllText(filePath);

            // Resolve env vars in the raw YAML before deserializing
            var resolvedYaml = ResolveEnvironmentVariables(rawYaml, filePath);

            try
            {
                var config = Deserializer.Deserialize<SqlGuardConfig>(resolvedYaml)
                    ?? throw new ConfigurationException("Config file is empty or invalid.");

                Validate(config, filePath);
                _logger.LogDebug("Config loaded successfully from {FilePath}", filePath);
                return config;
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                throw new ConfigurationException(
                    $"YAML parse error in '{filePath}' at line {ex.Start.Line}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves a ServerConfig into a DatabaseTarget, reading credentials
        /// from environment variables specified in AuthConfig.
        /// </summary>
        /// <exception cref="ConfigurationException">Thrown if required env vars are missing.</exception>
        public DatabaseTarget ResolveTarget(ServerConfig server)
        {
            if (!Enum.TryParse<DatabaseEngine>(server.Engine, ignoreCase: true, out var engine))
                throw new ConfigurationException(
                    $"Unknown database engine: '{server.Engine}'. Valid values: {string.Join(", ", Enum.GetNames<DatabaseEngine>())}");

            var auth = server.Auth;

            return new DatabaseTarget
            {
                Engine = engine,
                Host = server.Host,
                Port = server.Port,
                Database = server.Database,
                Label = server.Label,
                TimeoutSeconds = server.TimeoutSeconds,
                Username = ResolveCredential(auth.UsernameEnv, auth.Username, "username", isSecret: false),
                Password = ResolveCredential(auth.PasswordEnv, null, "password", isSecret: true),
                ConnectionStringOverride = ResolveConnectionString(auth)
            };
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private string ResolveEnvironmentVariables(string yaml, string filePath)
        {
            var missingVars = new List<string>();

            var resolved = EnvVarPattern.Replace(yaml, match =>
            {
                var varName = match.Groups["name"].Value;
                var value = Environment.GetEnvironmentVariable(varName);

                if (value is null)
                {
                    // Only flag as missing if it looks like a real reference (not coincidental $ usage)
                    if (match.Value.StartsWith("${"))
                        missingVars.Add(varName);
                    return match.Value; // leave unresolved — might be optional
                }

                _logger.LogDebug("Resolved env var {VarName} in config", varName);
                return value;
            });

            if (missingVars.Count > 0)
            {
                _logger.LogWarning(
                    "Config file '{FilePath}' references env vars that are not set: {Vars}. " +
                    "If these are credentials, set them before scanning.",
                    filePath, string.Join(", ", missingVars));
            }

            return resolved;
        }

        private string? ResolveCredential(string? envVarName, string? literalFallback,
            string fieldName, bool isSecret)
        {
            if (!string.IsNullOrWhiteSpace(envVarName))
            {
                var value = Environment.GetEnvironmentVariable(envVarName);
                if (value is null)
                    _logger.LogWarning("Environment variable '{EnvVar}' ({Field}) is not set.", envVarName, fieldName);
                return value;
            }

            if (!string.IsNullOrWhiteSpace(literalFallback))
            {
                if (isSecret)
                    _logger.LogWarning(
                        "A literal {Field} was found in config. " +
                        "Use {Field}_env pointing to an environment variable instead.", fieldName, fieldName);
                return literalFallback;
            }

            return null;
        }

        private string? ResolveConnectionString(AuthConfig auth)
        {
            if (string.IsNullOrWhiteSpace(auth.ConnectionStringEnv)) return null;

            var cs = Environment.GetEnvironmentVariable(auth.ConnectionStringEnv);
            if (cs is null)
                _logger.LogWarning(
                    "Env var '{EnvVar}' for connection_string is not set.", auth.ConnectionStringEnv);
            return cs;
        }

        private static void Validate(SqlGuardConfig config, string filePath)
        {
            var errors = new List<string>();

            // Check default server
            if (config.Server is not null)
                ValidateServer(config.Server, "server", errors);

            // Check all profiles
            foreach (var (name, profile) in config.Profiles)
                ValidateServer(profile, $"profiles.{name}", errors);

            if (errors.Count > 0)
                throw new ConfigurationException(
                    $"Config file '{filePath}' has {errors.Count} error(s):\n" +
                    string.Join("\n", errors.Select(e => $"  • {e}")));
        }

        private static void ValidateServer(ServerConfig server, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(server.Host))
                errors.Add($"{path}.host is required.");

            if (string.IsNullOrWhiteSpace(server.Database))
                errors.Add($"{path}.database is required.");

            if (!Enum.TryParse<DatabaseEngine>(server.Engine, ignoreCase: true, out _))
                errors.Add($"{path}.engine '{server.Engine}' is not valid. Use: SqlServer, PostgreSQL, MySQL, Oracle.");

            // Security: warn if a literal password-like field is present in auth
            // (post-resolution — if it survived env var replacement, it was literal)
            var auth = server.Auth;
            if (!string.IsNullOrWhiteSpace(auth.PasswordEnv) &&
                auth.PasswordEnv.Length > 3 &&
                !auth.PasswordEnv.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c)))
            {
                errors.Add(
                    $"{path}.auth.password_env should be an environment variable NAME (uppercase), " +
                    $"not a password value. Got: '{auth.PasswordEnv[..Math.Min(3, auth.PasswordEnv.Length)]}...'");
            }
        }
    }
}
