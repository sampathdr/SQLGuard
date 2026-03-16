using SqlGuard.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SqlGuard.Core.Abstractions
{
    /// <summary>
    /// Applies a single rule to a collector's live data.
    /// Stateless engine that evaluates one <see cref="RuleDefinition"/> against data obtained from an <see cref="IMetadataCollector"/>.
    /// </summary>
    public interface IEvaluationEngine
    {
        Task<EvaluationResult> EvaluateAsync(
            RuleDefinition rule,
            IMetadataCollector collector,
            CancellationToken ct = default);
    }
}
