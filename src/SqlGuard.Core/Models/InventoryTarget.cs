using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    public sealed record InventoryTarget
    {
        public required string Label { get; init; }
        public required DatabaseTarget Target { get; init; }
        public ScanOptions? ScanOptions { get; init; }   // per-server overrides
    }
}
