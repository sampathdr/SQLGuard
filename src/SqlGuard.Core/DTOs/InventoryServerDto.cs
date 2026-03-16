using SqlGuard.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.DTOs
{
    internal sealed class InventoryServerDto
    {
        public string? Label { get; set; }
        public string Engine { get; set; } = "SqlServer";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Database { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public List<string>? Packs { get; set; }
        public AuthConfig? Auth { get; set; }
    }
}
