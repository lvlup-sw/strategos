// -----------------------------------------------------------------------
// <copyright file="ContextProbe.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Agents.Models;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Process-shared observation sink for the DR-6 context-assembly behavioral proof
/// (T016). The stub <see cref="StubObjectSetProvider"/> records the
/// <c>SimilarityExpression</c> it received on <see cref="LastSimilarity"/>; the
/// context-aware enrich step records the <see cref="AssembledContext"/> the
/// generated worker handler delivered on <see cref="LastAssembledContext"/>.
/// </summary>
/// <remarks>
/// Registered as a DI singleton on the host so the stub provider and the step
/// share one instance. Reset at the start of each test to isolate it from prior
/// runs on the session-scoped host.
/// </remarks>
public sealed class ContextProbe
{
    private readonly object gate = new();

    /// <summary>
    /// Gets the similarity expression captured from the stub provider's most
    /// recent <c>ExecuteSimilarityAsync</c> call, or <see langword="null"/> if it
    /// has not been invoked since the last <see cref="Reset"/>.
    /// </summary>
    public SimilarityExpression? LastSimilarity { get; private set; }

    /// <summary>
    /// Gets the number of times the stub provider's <c>ExecuteSimilarityAsync</c>
    /// was invoked since the last <see cref="Reset"/>.
    /// </summary>
    public int SimilarityCallCount { get; private set; }

    /// <summary>
    /// Gets the assembled context the generated worker handler delivered to the
    /// context-aware step, or <see langword="null"/> if the step has not received
    /// context since the last <see cref="Reset"/>.
    /// </summary>
    public AssembledContext? LastAssembledContext { get; private set; }

    /// <summary>
    /// Records the similarity expression the stub provider was asked to execute.
    /// </summary>
    /// <param name="expression">The similarity expression.</param>
    public void RecordSimilarity(SimilarityExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));

        lock (this.gate)
        {
            this.LastSimilarity = expression;
            this.SimilarityCallCount++;
        }
    }

    /// <summary>
    /// Records the assembled context delivered to the context-aware step.
    /// </summary>
    /// <param name="context">The assembled context.</param>
    public void RecordAssembledContext(AssembledContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        lock (this.gate)
        {
            this.LastAssembledContext = context;
        }
    }

    /// <summary>
    /// Clears all captured observations. Called at the start of each test.
    /// </summary>
    public void Reset()
    {
        lock (this.gate)
        {
            this.LastSimilarity = null;
            this.SimilarityCallCount = 0;
            this.LastAssembledContext = null;
        }
    }
}
