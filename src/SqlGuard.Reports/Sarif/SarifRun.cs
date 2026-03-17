using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Sarif
{
    internal sealed class SarifRun
    {
        public SarifTool Tool { get; init; } = null!;
        public List<SarifResult> Results { get; init; } = [];
        public List<Invocation> Invocations { get; init; } = [];
        public Dictionary<string, object?>? Properties { get; init; }
    }
}
