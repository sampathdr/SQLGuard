using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Sarif
{
    internal sealed class SarifResult
    {
        public string RuleId { get; init; } = null!;
        public string Level { get; init; } = "warning";
        public Message Message { get; init; } = null!;
        public List<Location> Locations { get; init; } = [];
        public Dictionary<string, object?>? Properties { get; init; }
    }
}
