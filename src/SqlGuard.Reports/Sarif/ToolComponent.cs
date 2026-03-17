using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Sarif
{
    internal sealed class ToolComponent
    {
        public string Name { get; init; } = null!;
        public string? InformationUri { get; init; }
        public string? SemanticVersion { get; init; }
        public List<ReportingDescriptor> Rules { get; init; } = [];
    }
}
