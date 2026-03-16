using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    public sealed class MultiScanResult
    {
        public Guid BatchId { get; } = Guid.NewGuid();
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset CompletedAt { get; set; }
        public IReadOnlyList<ScanResult> Results { get; init; } = [];

        public int TotalPass => Results.Sum(r => r.PassCount);
        public int TotalFail => Results.Sum(r => r.FailCount);
        public int TotalWarn => Results.Sum(r => r.WarnCount);
        public int ServersScanned => Results.Count;
        public int ServersFailed => Results.Count(r => r.MandatoryFailCount > 0);
    }
}
