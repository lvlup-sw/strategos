using System.Collections.Immutable;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

namespace Strategos.Ontology.Tests.Diagnostics;

/// <summary>
/// DR-7 (Task 27): AONT205 also fires at graph-freeze as a defensive
/// invariant check on the composed graph. Task 16 already fires the
/// diagnostic at delta-apply; the freeze-time check guards the
/// post-merge state for the "two ingested sources racing intent" stress
/// case — if any descriptor survives to graph-freeze with
/// Source = Ingested AND non-empty Actions/Events/Lifecycle, the build
/// fails with AONT205 as an error.
/// </summary>
public class AONT205GraphFreezeTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Build_TwoSourcesAttemptIntentContributionPostMerge_AONT205Error()
    {
        // Synthesize the post-merge state directly: a purely ingested
        // descriptor that — despite Task 16's delta-apply check — somehow
        // reaches graph-freeze with intent fields populated. The
        // graph-freeze check must catch this defensively. We bypass the
        // delta path by hand-feeding the descriptor through a custom
        // DomainOntology that uses ObjectTypeFromDescriptor directly.
        var graphBuilder = new OntologyGraphBuilder()
            .AddDomain<DefectiveIngestedIntentOntology>();

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
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.DomainName).IsEqualTo("Trading");
        await Assert.That(aont205.TypeName).IsEqualTo("DefectivePosition");
    }

    /// <summary>
    /// Custom DomainOntology that bypasses the delta-apply invariant by
    /// calling <see cref="OntologyBuilder.ObjectTypeFromDescriptor"/>
    /// directly with an Ingested-tagged descriptor that carries Actions.
    /// Simulates a hypothetical bug where two sources race and the
    /// defensive Task 16 check is not enough.
    /// </summary>
    private sealed class DefectiveIngestedIntentOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            var defect = new ObjectTypeDescriptor
            {
                Name = "DefectivePosition",
                DomainName = "Trading",
                ClrType = null,
                SymbolKey = "scip-typescript ./defect.ts#DefectivePosition",
                LanguageId = "typescript",
                Source = DescriptorSource.Ingested,
                SourceId = "marten-typescript-defect",
                Actions = new List<ActionDescriptor>
                {
                    new("Trade", "Trade action emitted by a misbehaving ingester"),
                },
            };

            // ObjectTypeFromDescriptor is exposed on the concrete builder
            // type. Cast through the concrete type to bypass the
            // delta-level AONT205 check that ApplyDelta enforces.
            if (builder is OntologyBuilder concrete)
            {
                concrete.ObjectTypeFromDescriptor(defect);
            }
        }
    }

    [Test]
    public async Task Build_IngestedInterfaceActionMappings_AONT205Error()
    {
        var graphBuilder = new OntologyGraphBuilder()
            .AddDomain<DefectiveIngestedMappingOntology>();

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
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.Message).Contains("InterfaceActionMappings");
    }

    [Test]
    public async Task Build_IngestedExternalLinkExtensionPoints_AONT205Error()
    {
        var graphBuilder = new OntologyGraphBuilder()
            .AddDomain<DefectiveIngestedExtensionOntology>();

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
        var aont205 = caught!.Diagnostics.FirstOrDefault(d => d.Id == "AONT205");
        await Assert.That(aont205).IsNotNull();
        await Assert.That(aont205!.Message).Contains("ExternalLinkExtensionPoints");
    }

    [Test]
    public async Task Build_HandAuthoredContractWithActions_DoesNotFireAONT205()
    {
        var graph = new OntologyGraphBuilder()
            .AddDomain<ContractAuthoredIntentOntology>()
            .Build();

        var position = graph.ObjectTypes.Single(ot => ot.Name == "ContractPosition");
        await Assert.That(position.Source).IsEqualTo(DescriptorSource.HandAuthoredContract);
        await Assert.That(position.Actions.Count).IsEqualTo(1);
        await Assert.That(position.Actions[0].Name).IsEqualTo("Trade");
    }

    private sealed class DefectiveIngestedMappingOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            var defect = new ObjectTypeDescriptor
            {
                Name = "DefectiveMapping",
                DomainName = "Trading",
                SymbolKey = "scip-typescript ./defect.ts#DefectiveMapping",
                LanguageId = "typescript",
                Source = DescriptorSource.Ingested,
                SourceId = "marten-typescript-defect",
                InterfaceActionMappings = new List<InterfaceActionMapping>
                {
                    new() { InterfaceActionName = "Search", ConcreteActionName = "SearchPositions" },
                },
            };

            if (builder is OntologyBuilder concrete)
            {
                concrete.ObjectTypeFromDescriptor(defect);
            }
        }
    }

    private sealed class DefectiveIngestedExtensionOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            var defect = new ObjectTypeDescriptor
            {
                Name = "DefectiveExtension",
                DomainName = "Trading",
                SymbolKey = "scip-typescript ./defect.ts#DefectiveExtension",
                LanguageId = "typescript",
                Source = DescriptorSource.Ingested,
                SourceId = "marten-typescript-defect",
                ExternalLinkExtensionPoints = new List<ExternalLinkExtensionPoint>
                {
                    new() { Name = "KnowledgeLinks" },
                },
            };

            if (builder is OntologyBuilder concrete)
            {
                concrete.ObjectTypeFromDescriptor(defect);
            }
        }
    }

    private sealed class ContractAuthoredIntentOntology : DomainOntology
    {
        public override string DomainName => "Trading";

        protected override void Define(IOntologyBuilder builder)
        {
            var contract = new ObjectTypeDescriptor
            {
                Name = "ContractPosition",
                DomainName = "Trading",
                SymbolKey = "contract://trading/ContractPosition",
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
}
