using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Sarif
{
    internal sealed class ReportingDescriptor
    {
        public string Id { get; init; } = null!;
        public string? Name { get; init; }
        public MultiformatMessageString? ShortDescription { get; init; }
        public MultiformatMessageString? FullDescription { get; init; }
        public ReportingConfiguration? DefaultConfiguration { get; init; }
        public string? HelpUri { get; init; }
        public Dictionary<string, object?>? Properties { get; init; }
    }
}
