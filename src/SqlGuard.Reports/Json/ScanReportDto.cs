using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Json
{
    internal sealed class ScanReportDto
    {
        public string SchemaVersion { get; init; } = "1";
        public Guid ScanId { get; init; }
        public DateTimeOffset GeneratedAt { get; init; }
        public ServerInfoDto Server { get; init; } = null!;
        public ScanMetaDto Scan { get; init; } = null!;
        public SummaryDto Summary { get; init; } = null!;
        public List<ResultDto> Results { get; init; } = [];
    }
}
