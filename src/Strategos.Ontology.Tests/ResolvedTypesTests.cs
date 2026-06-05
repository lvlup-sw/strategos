using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public class ResolvedTypesTests
{
    private static ObjectTypeDescriptor CreateObjectType(string name, string domain) =>
        new(name, typeof(object), domain);

    [Test]
    public async Task ResolvedCrossDomainLink_Create_HasSourceAndTargetDomains()
    {
        var sourceType = CreateObjectType("Position", "trading");
        var targetType = CreateObjectType("Instrument", "market-data");

        var link = new ResolvedCrossDomainLink(
            Name: "PositionToInstrument",
            SourceDomain: "trading",
            SourceObjectType: sourceType,
            TargetDomain: "market-data",
            TargetObjectType: targetType,
            Cardinality: LinkCardinality.OneToOne);

        await Assert.That(link.SourceDomain).IsEqualTo("trading");
        await Assert.That(link.TargetDomain).IsEqualTo("market-data");
        await Assert.That(link.Name).IsEqualTo("PositionToInstrument");
        await Assert.That(link.Cardinality).IsEqualTo(LinkCardinality.OneToOne);
    }

    [Test]
    public async Task ResolvedCrossDomainLink_Create_HasResolvedObjectTypes()
    {
        var sourceType = CreateObjectType("Position", "trading");
        var targetType = CreateObjectType("Instrument", "market-data");

        var link = new ResolvedCrossDomainLink(
            Name: "PositionToInstrument",
            SourceDomain: "trading",
            SourceObjectType: sourceType,
            TargetDomain: "market-data",
            TargetObjectType: targetType,
            Cardinality: LinkCardinality.ManyToMany);

        await Assert.That(link.SourceObjectType).IsEqualTo(sourceType);
        await Assert.That(link.TargetObjectType).IsEqualTo(targetType);
    }

    [Test]
    public async Task WorkflowChain_Create_HasWorkflowNameAndConsumedProducedTypes()
    {
        var consumedType = CreateObjectType("Order", "trading");
        var producedType = CreateObjectType("Position", "trading");

        var chain = new WorkflowChain(
            WorkflowName: "OrderExecution",
            ConsumedType: consumedType,
            ProducedType: producedType);

        await Assert.That(chain.WorkflowName).IsEqualTo("OrderExecution");
        await Assert.That(chain.ConsumedType).IsEqualTo(consumedType);
        await Assert.That(chain.ProducedType).IsEqualTo(producedType);
    }
}
