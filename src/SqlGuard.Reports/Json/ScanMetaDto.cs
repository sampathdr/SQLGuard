using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Json
{
    internal sealed class ScanMetaDto
    {
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; init; }
        public long DurationMs { get; init; }
        public List<string> PacksLoaded { get; init; } = [];
    }
}
