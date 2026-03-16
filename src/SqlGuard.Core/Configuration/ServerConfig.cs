using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Configuration
{
    /// <summary>
    /// Connection target for a single database server.
    /// </summary>
    public sealed class ServerConfig
    {
        /// <summary>Friendly label shown in reports.</summary>
        public string? Label { get; set; }

        /// <summary>Database engine. Valid: SqlServer, PostgreSQL, MySQL, Oracle.</summary>
        public string Engine { get; set; } = "SqlServer";

        /// <summary>Hostname or IP address.</summary>
        public string Host { get; set; } = "localhost";

        /// <summary>Port. 0 = engine default.</summary>
        public int Port { get; set; }

        /// <summary>Database / catalog name.</summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>Connection timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Authentication settings. See AuthConfig.</summary>
        public AuthConfig Auth { get; set; } = new();
    }
}
