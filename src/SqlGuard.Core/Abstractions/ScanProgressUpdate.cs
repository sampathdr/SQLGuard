using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Progress event emitted for each rule during a single-server scan.
    /// </summary>
    public sealed record ScanProgressUpdate
    {
        public required string RuleId { get; init; }
        public required string RuleTitle { get; init; }
        public required int Current { get; init; }
        public required int Total { get; init; }
        public RuleStatus? Status { get; init; }
    }
}
