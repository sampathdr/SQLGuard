using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Reports.Sarif
{
    internal sealed class Location 
    { 
        public List<LogicalLocation>? LogicalLocations { get; init; } 
    }
}
