using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Sarif
{
    internal sealed class LogicalLocation
    {
        public string? Name { get; init; }
        public string? Kind { get; init; }
        public string? FullyQualifiedName { get; init; }
    }
}
