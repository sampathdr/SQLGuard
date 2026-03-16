using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    public sealed record DatabaseTarget
    {
        public required DatabaseEngine Engine { get; init; }
        public required string Host { get; init; }
        public int Port { get; init; }               // 0 = engine default
        public required string Database { get; init; }
        public string? Username { get; init; }               // null = Windows/peer auth
        public string? Password { get; init; }               // consider vault integration
        public string? ConnectionStringOverride { get; init; }               // full override if needed
        public int TimeoutSeconds { get; init; } = 30;
        public string? Label { get; init; }               // friendly name for reports
    }
}
