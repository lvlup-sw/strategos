using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests;

/// <summary>
/// DR-3 + DR-10 (Tasks 13, 17): integration coverage for the
/// <see cref="OntologyGraphBuilder.Build"/> source-drain. Two sources
/// contributing different fields both reach the composed graph; a source
/// that throws during <c>LoadAsync</c> surfaces its <c>SourceId</c> in
/// the propagated <see cref="OntologyCompositionException"/>.
/// </summary>
public class OntologyGraphBuilderSourcesTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Build_TwoSourcesContributingDifferentFields_BothAppearInGraph()
    {
        var positionDescriptor = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./pos.ts#Position",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
        };

        var orderDescriptor = new ObjectTypeDescriptor
        {
            Name = "Order",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = "scip-typescript ./ord.ts#Order",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript-orders",
        };

        var sourceA = new TestOntologySource
        {
            SourceId = "marten-typescript",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(positionDescriptor)
                {
                    SourceId = "marten-typescript",
                    Timestamp = Timestamp,
                }),
        };

        var sourceB = new TestOntologySource
        {
            SourceId = "marten-typescript-orders",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(orderDescriptor)
                {
                    SourceId = "marten-typescript-orders",
                    Timestamp = Timestamp,
                }),
        };

        var graphBuilder = new OntologyGraphBuilder()
            .AddSources(new IOntologySource[] { sourceA, sourceB });

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypes.Count).IsEqualTo(2);
        await Assert.That(graph.ObjectTypes.Any(ot => ot.Name == "Position" && ot.Source == DescriptorSource.Ingested))
            .IsTrue();
        await Assert.That(graph.ObjectTypes.Any(ot => ot.Name == "Order" && ot.Source == DescriptorSource.Ingested))
            .IsTrue();
    }

    // ----- Task 17: source-error propagation -----

    [Test]
    public async Task Build_SourceThrowsDuringLoadAsync_ExceptionMessageContainsSourceId()
    {
        // DR-10 failure mode 1: source raises during LoadAsync ⇒
        // OntologyGraphBuilder.Build() propagates the exception as an
        // OntologyCompositionException whose message contains the
        // offending source's SourceId so logs/incident reports attribute
        // the failure to the right ingester.
        var throwingSource = new ThrowingSource("flaky-typescript");

        var graphBuilder = new OntologyGraphBuilder()
            .AddSources(new IOntologySource[] { throwingSource });

        OntologyCompositionException? caught = null;
        try
        {
            graphBuilder.Build();
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("flaky-typescript");
    }

    [Test]
    public async Task Build_SourceThrowsAfterFirstDelta_ExceptionContainsSourceId()
    {
        // Variant: the source yields one delta and then raises; the wrap
        // still catches the post-yield exception and surfaces SourceId.
        var source = new PartialDrainThrowingSource("partial-typescript", Timestamp);

        var graphBuilder = new OntologyGraphBuilder()
            .AddSources(new IOntologySource[] { source });

        OntologyCompositionException? caught = null;
        try
        {
            graphBuilder.Build();
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("partial-typescript");
        // Inner exception preserves the original failure for diagnostics.
        await Assert.That(caught.InnerException).IsNotNull();
    }

    private sealed class ThrowingSource(string sourceId) : IOntologySource
    {
        public string SourceId { get; } = sourceId;

        public async IAsyncEnumerable<OntologyDelta> LoadAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            throw new InvalidOperationException("synthetic source failure");
#pragma warning disable CS0162 // Unreachable code detected
            yield break;
#pragma warning restore CS0162
        }

        public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class PartialDrainThrowingSource(string sourceId, DateTimeOffset timestamp)
        : IOntologySource
    {
        public string SourceId { get; } = sourceId;

        public async IAsyncEnumerable<OntologyDelta> LoadAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();

            yield return new OntologyDelta.AddObjectType(
                new ObjectTypeDescriptor
                {
                    Name = "Half",
                    DomainName = "Trading",
                    SymbolKey = "scip ./half#Half",
                    Source = DescriptorSource.Ingested,
                    SourceId = SourceId,
                })
            {
                SourceId = SourceId,
                Timestamp = timestamp,
            };

            throw new InvalidOperationException("synthetic mid-stream failure");
        }

        public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }
    }
}
