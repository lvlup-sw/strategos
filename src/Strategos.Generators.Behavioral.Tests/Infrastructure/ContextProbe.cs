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

    private SimilarityExpression? lastSimilarity;
    private int similarityCallCount;
    private AssembledContext? lastAssembledContext;

    /// <summary>
    /// Gets the similarity expression captured from the stub provider's most
    /// recent <c>ExecuteSimilarityAsync</c> call, or <see langword="null"/> if it
    /// has not been invoked since the last <see cref="Reset"/>.
    /// </summary>
    /// <remarks>
    /// The getter acquires the same <c>gate</c> lock the writers hold, so a test
    /// assertion (reader) never observes torn/stale state written by the stub
    /// provider (writer) on another thread — this is a DI singleton shared across
    /// both roles.
    /// </remarks>
    public SimilarityExpression? LastSimilarity
    {
        get
        {
            lock (this.gate)
            {
                return this.lastSimilarity;
            }
        }
    }

    /// <summary>
    /// Gets the number of times the stub provider's <c>ExecuteSimilarityAsync</c>
    /// was invoked since the last <see cref="Reset"/>.
    /// </summary>
    /// <remarks>
    /// Read under the same <c>gate</c> lock the writers hold so the count is
    /// synchronized with the write that incremented it.
    /// </remarks>
    public int SimilarityCallCount
    {
        get
        {
            lock (this.gate)
            {
                return this.similarityCallCount;
            }
        }
    }

    /// <summary>
    /// Gets the assembled context the generated worker handler delivered to the
    /// context-aware step, or <see langword="null"/> if the step has not received
    /// context since the last <see cref="Reset"/>.
    /// </summary>
    /// <remarks>
    /// Read under the same <c>gate</c> lock the writers hold so the reader never
    /// observes a half-published reference.
    /// </remarks>
    public AssembledContext? LastAssembledContext
    {
        get
        {
            lock (this.gate)
            {
                return this.lastAssembledContext;
            }
        }
    }

    /// <summary>
    /// Records the similarity expression the stub provider was asked to execute.
    /// </summary>
    /// <param name="expression">The similarity expression.</param>
    public void RecordSimilarity(SimilarityExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));

        lock (this.gate)
        {
            this.lastSimilarity = expression;
            this.similarityCallCount++;
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
            this.lastAssembledContext = context;
        }
    }

    /// <summary>
    /// Clears all captured observations. Called at the start of each test.
    /// </summary>
    public void Reset()
    {
        lock (this.gate)
        {
            this.lastSimilarity = null;
            this.similarityCallCount = 0;
            this.lastAssembledContext = null;
        }
    }
}
