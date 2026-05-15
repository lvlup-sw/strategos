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

    // Task 17 (Build_SourceThrowsDuringLoadAsync_ExceptionMessageContainsSourceId)
    // is appended to this file at task time per the implementer instructions.
}
