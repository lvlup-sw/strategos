// -----------------------------------------------------------------------
// <copyright file="StubObjectSetProvider.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Ontology.ObjectSets;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// A stub <see cref="IObjectSetProvider"/> for the DR-6 context-assembly
/// behavioral proof (T016). It records every <c>ExecuteSimilarityAsync</c> call's
/// <c>SimilarityExpression</c> on the shared <see cref="ContextProbe"/> (so the
/// test can assert the declared <c>TopK</c>/<c>MinRelevance</c> reached the
/// provider) and returns a single canned scored item so the generated assembler
/// produces a non-empty retrieval segment.
/// </summary>
/// <remarks>
/// Only <see cref="ExecuteSimilarityAsync{T}"/> is exercised by the generated
/// assembler; the non-similarity members throw because the context-assembly path
/// never calls them.
/// </remarks>
public sealed class StubObjectSetProvider(ContextProbe probe) : IObjectSetProvider
{
    /// <summary>The canned relevance score returned for the single stub item.</summary>
    public const double StubScore = 0.95;

    private readonly ContextProbe probe = probe;

    /// <inheritdoc />
    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));

        // Capture the lowered expression so the test can assert the step's
        // declared TopK / MinRelevance / query reached the provider.
        this.probe.RecordSimilarity(expression);

        // Return one canned item. T is the collection marker type (ProductCatalog);
        // a parameterless instance suffices for the assembler's item.ToString() map.
        var item = Activator.CreateInstance<T>();
        var result = new ScoredObjectSetResult<T>(
            items: [item],
            totalCount: 1,
            inclusion: ObjectSetInclusion.Properties,
            scores: [StubScore]);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        throw new NotSupportedException("The context-assembly path does not call ExecuteAsync.");

    /// <inheritdoc />
    public IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        throw new NotSupportedException("The context-assembly path does not call StreamAsync.");

    /// <inheritdoc />
    public Task EnsureSchemaAsync<T>(CancellationToken ct = default) where T : class =>
        throw new NotSupportedException("The context-assembly path does not call EnsureSchemaAsync.");

    /// <inheritdoc />
    public Task EnsureAllSchemasAsync(CancellationToken ct = default) =>
        throw new NotSupportedException("The context-assembly path does not call EnsureAllSchemasAsync.");
}
