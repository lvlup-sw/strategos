using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Merge;

/// <summary>
/// #163: contract-authored intent is first-class. A
/// <see cref="DescriptorSource.HandAuthoredContract"/> action survives
/// graph merge with a mechanically ingested structural contribution;
/// ingested intent on the same type still fails AONT205.
/// </summary>
public class HandAuthoredContractMergeTests
{
    private const string DomainName = "Trading";

    private const string IngestSourceId = "marten-typescript";

    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public sealed class ContractPositionOntology : DomainOntology
    {
        public override string DomainName => HandAuthoredContractMergeTests.DomainName;

        protected override void Define(IOntologyBuilder builder)
        {
            var contract = new ObjectTypeDescriptor
            {
                Name = "Position",
                DomainName = DomainName,
                SymbolKey = "contract://trading/Position",
                Source = DescriptorSource.HandAuthoredContract,
                Actions = new List<ActionDescriptor>
                {
                    new("Trade", "Contract-authored trade"),
                },
            };

            if (builder is OntologyBuilder concrete)
            {
                concrete.ObjectTypeFromDescriptor(contract);
            }
        }
    }

    [Test]
    public async Task Merge_ContractAuthoredAction_SurvivesIngestedStructuralContribution()
    {
        var ingested = new ObjectTypeDescriptor
        {
            Name = "Position",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./pos.ts#Position",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
            Properties = new List<PropertyDescriptor>
            {
                new("Symbol", typeof(string)) { Source = DescriptorSource.Ingested },
            },
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(ingested)
                {
                    SourceId = IngestSourceId,
                    Timestamp = Timestamp,
                }),
        };

        var graph = new OntologyGraphBuilder()
            .AddDomain<ContractPositionOntology>()
            .AddSources(new IOntologySource[] { source })
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "Position");
        await Assert.That(position.Actions.Count).IsEqualTo(1);
        await Assert.That(position.Actions[0].Name).IsEqualTo("Trade");
        await Assert.That(position.Properties.Any(p => p.Name == "Symbol")).IsTrue();
        await Assert.That(position.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task Merge_IngestedActions_StillFailAONT205()
    {
        var ingested = new ObjectTypeDescriptor
        {
            Name = "OrphanIntent",
            DomainName = DomainName,
            SymbolKey = "scip-typescript ./orphan.ts#OrphanIntent",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = IngestSourceId,
            Actions = new List<ActionDescriptor>
            {
                new("Trade", "Mechanical trade"),
            },
        };

        var source = new TestOntologySource
        {
            SourceId = IngestSourceId,
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(ingested)
                {
                    SourceId = IngestSourceId,
                    Timestamp = Timestamp,
                }),
        };

        OntologyCompositionException? caught = null;
        try
        {
            new OntologyGraphBuilder()
                .AddSources(new IOntologySource[] { source })
                .Build();
        }
        catch (OntologyCompositionException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.Message).Contains("Actions");
    }
}
