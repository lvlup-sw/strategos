using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

public class BlastRadiusTypesTests
{
    [Test]
    public async Task BlastRadiusScope_HasAllFourValues()
    {
        var values = Enum.GetValues<BlastRadiusScope>();

        // Lock the public surface — adding a new BlastRadiusScope member
        // without updating consumers (Validate verdict serialization,
        // ClassifyScope, agent contracts) silently changes API behavior.
        await Assert.That(values).HasCount().EqualTo(4);
        await Assert.That(values).Contains(BlastRadiusScope.Local);
        await Assert.That(values).Contains(BlastRadiusScope.Domain);
        await Assert.That(values).Contains(BlastRadiusScope.CrossDomain);
        await Assert.That(values).Contains(BlastRadiusScope.Global);
    }

    [Test]
    public async Task BlastRadiusOptions_Construction_DefaultMaxExpansionDegreeIs16()
    {
        var options = new BlastRadiusOptions();

        await Assert.That(options.MaxExpansionDegree).IsEqualTo(16);
    }

    [Test]
    public async Task BlastRadiusOptions_Construction_CustomMaxExpansionDegree()
    {
        var options = new BlastRadiusOptions(MaxExpansionDegree: 8);

        await Assert.That(options.MaxExpansionDegree).IsEqualTo(8);
    }

    [Test]
    public async Task CrossDomainHop_Construction_PreservesAllFields()
    {
        var sourceNode = new OntologyNodeRef("trading", "Position");
        var targetNode = new OntologyNodeRef("market-data", "Instrument");

        var hop = new CrossDomainHop(
            FromDomain: "trading",
            ToDomain: "market-data",
            SourceNode: sourceNode,
            TargetNode: targetNode);

        await Assert.That(hop.FromDomain).IsEqualTo("trading");
        await Assert.That(hop.ToDomain).IsEqualTo("market-data");
        await Assert.That(hop.SourceNode).IsEqualTo(sourceNode);
        await Assert.That(hop.TargetNode).IsEqualTo(targetNode);
    }

    [Test]
    public async Task BlastRadius_Construction_PreservesAllFields()
    {
        var directNode = new OntologyNodeRef("trading", "Order");
        var transitiveNode = new OntologyNodeRef("trading", "Position");
        var sourceNode = new OntologyNodeRef("trading", "Position");
        var targetNode = new OntologyNodeRef("market-data", "Instrument");

        var hop = new CrossDomainHop(
            FromDomain: "trading",
            ToDomain: "market-data",
            SourceNode: sourceNode,
            TargetNode: targetNode);

        var blastRadius = new BlastRadius(
            DirectlyAffected: [directNode],
            TransitivelyAffected: [transitiveNode],
            CrossDomainHops: [hop],
            Scope: BlastRadiusScope.CrossDomain);

        await Assert.That(blastRadius.DirectlyAffected).HasCount().EqualTo(1);
        await Assert.That(blastRadius.DirectlyAffected[0]).IsEqualTo(directNode);
        await Assert.That(blastRadius.TransitivelyAffected).HasCount().EqualTo(1);
        await Assert.That(blastRadius.TransitivelyAffected[0]).IsEqualTo(transitiveNode);
        await Assert.That(blastRadius.CrossDomainHops).HasCount().EqualTo(1);
        await Assert.That(blastRadius.CrossDomainHops[0]).IsEqualTo(hop);
        await Assert.That(blastRadius.Scope).IsEqualTo(BlastRadiusScope.CrossDomain);
    }

    [Test]
    public async Task BlastRadius_Construction_EmptyListsAreValid()
    {
        var blastRadius = new BlastRadius(
            DirectlyAffected: [],
            TransitivelyAffected: [],
            CrossDomainHops: [],
            Scope: BlastRadiusScope.Local);

        await Assert.That(blastRadius.DirectlyAffected).IsEmpty();
        await Assert.That(blastRadius.TransitivelyAffected).IsEmpty();
        await Assert.That(blastRadius.CrossDomainHops).IsEmpty();
        await Assert.That(blastRadius.Scope).IsEqualTo(BlastRadiusScope.Local);
    }
}
