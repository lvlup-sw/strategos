using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.TestInfrastructure;

/// <summary>
/// DR-9 (Task 20): contract tests for the <see cref="TestOntologySource"/>
/// fixture used across merge/diagnostic/integration tests. Verifies the
/// fixture matches production source wiring (same <see cref="IOntologySource"/>
/// interface) and that <c>LoadAsync</c> yields configured deltas in order
/// while <c>SubscribeAsync</c> completes immediately.
/// </summary>
public class TestOntologySourceTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task TestOntologySource_LoadAsync_YieldsConfiguredDeltas()
    {
        var d1 = new OntologyDelta.AddObjectType(
            new ObjectTypeDescriptor("A", typeof(string), "X"))
        {
            SourceId = "test",
            Timestamp = Timestamp,
        };
        var d2 = new OntologyDelta.RemoveObjectType("X", "B")
        {
            SourceId = "test",
            Timestamp = Timestamp,
        };
        var d3 = new OntologyDelta.RenameProperty("X", "C", "Old", "New")
        {
            SourceId = "test",
            Timestamp = Timestamp,
        };

        var source = new TestOntologySource
        {
            SourceId = "test",
            Deltas = ImmutableArray.Create<OntologyDelta>(d1, d2, d3),
        };

        var collected = new List<OntologyDelta>();
        await foreach (var delta in source.LoadAsync(CancellationToken.None))
        {
            collected.Add(delta);
        }

        await Assert.That(collected.Count).IsEqualTo(3);
        await Assert.That(collected[0]).IsEqualTo((OntologyDelta)d1);
        await Assert.That(collected[1]).IsEqualTo((OntologyDelta)d2);
        await Assert.That(collected[2]).IsEqualTo((OntologyDelta)d3);
    }

    [Test]
    public async Task TestOntologySource_SubscribeAsync_CompletesImmediately()
    {
        var source = new TestOntologySource
        {
            SourceId = "test",
            Deltas = ImmutableArray<OntologyDelta>.Empty,
        };

        var count = 0;
        await foreach (var _ in source.SubscribeAsync(CancellationToken.None))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task TestOntologySource_ImplementsIOntologySource()
    {
        var source = new TestOntologySource
        {
            SourceId = "test",
            Deltas = ImmutableArray<OntologyDelta>.Empty,
        };

        await Assert.That((object)source).IsAssignableTo<IOntologySource>();
        await Assert.That(source.SourceId).IsEqualTo("test");
    }

    [Test]
    public async Task TestOntologySource_LoadAsync_HonorsCancellation()
    {
        var d = new OntologyDelta.RemoveObjectType("X", "B")
        {
            SourceId = "test",
            Timestamp = Timestamp,
        };
        var source = new TestOntologySource
        {
            SourceId = "test",
            Deltas = ImmutableArray.Create<OntologyDelta>(d, d),
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        OperationCanceledException? caught = null;
        try
        {
            await foreach (var _ in source.LoadAsync(cts.Token))
            {
                // expected to throw before yielding
            }
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task TestOntologySource_LoadAsync_PreCancelled_EmptyDeltas_StillThrows()
    {
        // Regression: a pre-cancelled token must fail fast even when the
        // delta list is empty. Previously the per-iteration check was the
        // only guard, so a no-deltas Load would complete normally on an
        // already-cancelled token.
        var source = new TestOntologySource
        {
            SourceId = "test",
            Deltas = ImmutableArray<OntologyDelta>.Empty,
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        OperationCanceledException? caught = null;
        try
        {
            await foreach (var _ in source.LoadAsync(cts.Token))
            {
            }
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task TestOntologySource_SubscribeAsync_PreCancelled_Throws()
    {
        var source = new TestOntologySource
        {
            SourceId = "test",
            Deltas = ImmutableArray<OntologyDelta>.Empty,
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        OperationCanceledException? caught = null;
        try
        {
            await foreach (var _ in source.SubscribeAsync(cts.Token))
            {
            }
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }
}
