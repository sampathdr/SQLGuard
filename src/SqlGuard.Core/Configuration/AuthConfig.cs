using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Configuration
{
    /// <summary>
    /// Authentication configuration.
    ///
    /// NEVER store literal passwords here. Use one of:
    ///   type: password
    ///   username_env: DB_USER        # env var NAME (not value)
    ///   password_env: DB_PASS        # env var NAME (not value)
    ///
    ///   type: connection_string
    ///   connection_string_env: SQLGUARD_CONNSTR  # env var holding the full connection string
    ///
    ///   type: integrated             # Windows auth (SQL Server)
    ///   type: managed_identity       # Azure MI / Workload Identity
    /// </summary>
    public sealed class AuthConfig
    {
        /// <summary>
        /// Authentication type.
        /// Valid values: password | integrated | peer | managed_identity | connection_string
        /// </summary>
        public string Type { get; set; } = "password";

        /// <summary>Literal username (acceptable — not a secret).</summary>
        public string? Username { get; set; }

        /// <summary>
        /// Name of the environment variable that contains the username.
        /// Takes precedence over Username if set.
        /// </summary>
        public string? UsernameEnv { get; set; }

        /// <summary>
        /// Name of the environment variable that contains the password.
        /// NEVER put the actual password here.
        /// </summary>
        public string? PasswordEnv { get; set; }

        /// <summary>
        /// Name of the environment variable that contains a complete connection string.
        /// Used with type: connection_string. The env var value may contain credentials.
        /// NEVER put the actual connection string here.
        /// </summary>
        public string? ConnectionStringEnv { get; set; }
    }
}
