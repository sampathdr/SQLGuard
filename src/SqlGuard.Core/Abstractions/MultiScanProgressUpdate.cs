using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Progress event emitted during a multi-server inventory scan.
    /// </summary>
    public sealed record MultiScanProgressUpdate
    {
        public required string ServerLabel { get; init; }
        public required int ServerCurrent { get; init; }
        public required int ServerTotal { get; init; }
        public ScanProgressUpdate? RuleUpdate { get; init; }
    }
}
