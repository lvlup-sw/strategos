using System.Collections.Immutable;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Contracts;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Contracts;

/// <summary>#170 contract-to-graph round-trip.</summary>
public sealed class ContractOntologyCatalogTests
{
    [Test]
    public async Task GeneratedCatalog_PreservesActionIntentAndContractProvenance()
    {
        var position = ContractOntologyCatalog.ObjectTypes.Single();
        var action = position.Actions.Single();

        await Assert.That(position.Source).IsEqualTo(DescriptorSource.HandAuthoredContract);
        await Assert.That(position.SymbolKey).IsEqualTo("typespec://Trading/Position");
        await Assert.That(action.Name).IsEqualTo("inspectPosition");
        await Assert.That(action.RequiredAuthority).IsEqualTo("position.reader");
        await Assert.That(action.AllowedClients).IsEquivalentTo(["mcp", "web"]);
        await Assert.That(action.RequiresConfirmation).IsFalse();
        await Assert.That(action.IsReadOnly).IsTrue();
        await Assert.That(action.Idempotent).IsTrue();
        await Assert.That(action.Preconditions.Single().Kind)
            .IsEqualTo(PreconditionKind.RelationHolds);
    }

    [Test]
    public async Task GeneratedCatalog_MergesWithIngestedStructureWithoutAont205()
    {
        var ingested = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = "Trading",
            SymbolKey = "typespec://Trading/Position",
            LanguageId = "typespec",
            Source = DescriptorSource.Ingested,
            SourceId = "typespec-schema",
            Properties =
            [
                new PropertyDescriptor("Status", typeof(string))
                {
                    Source = DescriptorSource.Ingested,
                },
            ],
        };
        var source = new TestOntologySource
        {
            SourceId = "typespec-schema",
            Deltas = ImmutableArray.Create<OntologyDelta>(new OntologyDelta.AddObjectType(ingested)
            {
                SourceId = "typespec-schema",
                Timestamp = new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero),
            }),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<ContractTradingOntology>()
            .AddSources([source])
            .Build();

        var position = graph.ObjectTypes.Single(item => item.Name == "Position");
        await Assert.That(position.Actions.Single().Name).IsEqualTo("inspectPosition");
        await Assert.That(position.Properties.Single().Name).IsEqualTo("Status");
        await Assert.That(graph.Warnings.Any(message => message.Contains("AONT205", StringComparison.Ordinal)))
            .IsFalse();
    }

    private sealed class ContractTradingOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            builder.AuthorityAxis("access", "none", "read");
            builder.Authority("position.reader").At("access", "read");
            foreach (var descriptor in ContractOntologyCatalog.ObjectTypes)
            {
                builder.ObjectTypeFromDescriptor(descriptor);
            }
        }
    }
}
