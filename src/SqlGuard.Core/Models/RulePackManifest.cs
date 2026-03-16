using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Models
{
    /// <summary>
    /// Metadata from a pack's manifest.yaml file.
    /// </summary>
    public sealed record RulePackManifest
    {
        public required string Id { get; init; }                 // e.g. "sqlserver-cis"
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Version { get; init; }
        public required DatabaseEngine Engine { get; init; }
        public string? Author { get; init; }
        public string? Homepage { get; init; }
        public IReadOnlyList<string> Tags { get; init; } = [];
        public IReadOnlyList<string> Dependencies { get; init; } = [];      // packs that must load first
    }
}
