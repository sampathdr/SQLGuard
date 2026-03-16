using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Resolves a <c>dynamic_expected_function</c> string (from a rule YAML) to a
    /// runtime expected value. Register implementations via DI to extend the engine.
    /// </summary>
    public interface IDynamicExpectedResolver
    {
        /// <summary>Returns true if this resolver handles <paramref name="functionExpression"/>.</summary>
        bool CanResolve(string functionExpression);

        /// <summary>Computes and returns the expected value at scan time.</summary>
        Task<object?> ResolveAsync(
            string functionExpression,
            IMetadataCollector collector,
            CancellationToken ct = default);
    }
}
