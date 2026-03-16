using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Loads RuleDefinitions from YAML files or embedded resources.
    /// Resolves a pack name or path to a list of <see cref="RuleDefinition"/> objects.
    /// Supports filesystem directories, explicit .yaml files, and embedded NuGet resources.
    /// </summary>
    public interface IRulePackLoader
    {
        /// <summary>Load all rules from a directory, .yaml file, or built-in pack name.</summary>
        Task<IReadOnlyList<RuleDefinition>> LoadAsync(
            string packNameOrPath, CancellationToken ct = default);

        /// <summary>Load the pack's manifest.yaml metadata, or null if not present.</summary>
        Task<RulePackManifest?> LoadManifestAsync(
            string packNameOrPath, CancellationToken ct = default);
    }
}
