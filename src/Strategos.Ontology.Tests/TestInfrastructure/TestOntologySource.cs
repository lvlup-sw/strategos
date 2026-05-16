using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Strategos.Ontology;

namespace Strategos.Ontology.Tests.TestInfrastructure;

/// <summary>
/// In-memory <see cref="IOntologySource"/> fixture for unit/merge/diagnostic
/// tests. <c>LoadAsync</c> yields the configured <see cref="Deltas"/> in
/// order; <c>SubscribeAsync</c> completes immediately.
/// </summary>
/// <remarks>
/// DR-9 (Task 20). The fixture matches production source wiring (same
/// <see cref="IOntologySource"/> interface) so test code paths and the
/// real ingester drain identical surfaces. Cancellation is honored
/// between yields.
/// </remarks>
public sealed class TestOntologySource : IOntologySource
{
    public required string SourceId { get; init; }

    public required ImmutableArray<OntologyDelta> Deltas { get; init; }

    public async IAsyncEnumerable<OntologyDelta> LoadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Honor pre-cancelled tokens before the first await — otherwise
        // a pre-cancelled call would complete normally (no deltas) instead
        // of failing fast, which masks bad callers in tests.
        ct.ThrowIfCancellationRequested();

        // Yield once so the method body runs asynchronously even when the
        // delta list is empty — keeps the contract consistent with the
        // production source wiring.
        await Task.Yield();

        foreach (var delta in Deltas)
        {
            ct.ThrowIfCancellationRequested();
            yield return delta;
        }
    }

    public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
        yield break;
    }
}
