using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Json
{
    internal sealed class ServerInfoDto
    {
        public string? Label { get; init; }
        public string Engine { get; init; } = null!;
        public string Host { get; init; } = null!;
        public string Database { get; init; } = null!;
        public string? Version { get; init; }
    }
}
