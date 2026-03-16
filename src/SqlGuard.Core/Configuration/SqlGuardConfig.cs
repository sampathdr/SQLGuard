using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Configuration
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // SqlGuardConfig  —  root model for sqlguard.yml
    //
    // SECURITY RULES enforced by this model:
    //   • Passwords NEVER appear as literal strings — only as env var references
    //   • Connection strings with credentials NEVER stored in config files
    //   • Config files should be committed to version control (no secrets)
    //   • All sensitive values use ${ENV_VAR} interpolation pattern
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Root configuration model. Maps 1:1 to sqlguard.yml.
    /// Designed to be safe for version control — no literal secrets.
    /// </summary>
    public sealed class SqlGuardConfig
    {
        /// <summary>Version of the config file schema (for future migrations).</summary>
        public string SchemaVersion { get; set; } = "1";

        /// <summary>Default server to scan when --server is omitted.</summary>
        public ServerConfig? Server { get; set; }

        /// <summary>Multiple named server profiles (use --profile to select).</summary>
        public Dictionary<string, ServerConfig> Profiles { get; set; } = [];

        /// <summary>Default scan settings.</summary>
        public ScanConfig Scan { get; set; } = new();

        /// <summary>Report output settings.</summary>
        public ReportConfig Report { get; set; } = new();
    }
}
