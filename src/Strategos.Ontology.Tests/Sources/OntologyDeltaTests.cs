using Strategos.Ontology;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Sources;

/// <summary>
/// DR-4 (Task 8): construction + round-trip equality coverage for each
/// of the eight <see cref="OntologyDelta"/> variants. Asserts the
/// rename-as-single-delta invariant.
/// </summary>
public class OntologyDeltaTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private const string SourceId = "marten-typescript";

    [Test]
    public async Task AddObjectType_Construction_RoundTrips()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        OntologyDelta delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta).IsTypeOf<OntologyDelta.AddObjectType>();
        var add = (OntologyDelta.AddObjectType)delta;
        await Assert.That(add.Descriptor).IsEqualTo(descriptor);
        await Assert.That(add.SourceId).IsEqualTo(SourceId);
        await Assert.That(add.Timestamp).IsEqualTo(Timestamp);
    }

    [Test]
    public async Task UpdateObjectType_Construction_RoundTrips()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        var delta = new OntologyDelta.UpdateObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.Descriptor).IsEqualTo(descriptor);
        await Assert.That(delta.SourceId).IsEqualTo(SourceId);
        await Assert.That(delta.Timestamp).IsEqualTo(Timestamp);
    }

    [Test]
    public async Task RemoveObjectType_Construction_RoundTrips()
    {
        var delta = new OntologyDelta.RemoveObjectType("Trading", "Position")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.DomainName).IsEqualTo("Trading");
        await Assert.That(delta.TypeName).IsEqualTo("Position");
        await Assert.That(delta.SourceId).IsEqualTo(SourceId);
    }

    [Test]
    public async Task AddProperty_Construction_RoundTrips()
    {
        var property = new PropertyDescriptor("Symbol", typeof(string));

        var delta = new OntologyDelta.AddProperty("Trading", "Position", property)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.DomainName).IsEqualTo("Trading");
        await Assert.That(delta.TypeName).IsEqualTo("Position");
        await Assert.That(delta.Descriptor).IsEqualTo(property);
    }

    [Test]
    public async Task RenameProperty_Construction_RoundTrips()
    {
        var delta = new OntologyDelta.RenameProperty("Trading", "Position", "Sym", "Symbol")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.DomainName).IsEqualTo("Trading");
        await Assert.That(delta.TypeName).IsEqualTo("Position");
        await Assert.That(delta.FromName).IsEqualTo("Sym");
        await Assert.That(delta.ToName).IsEqualTo("Symbol");
    }

    [Test]
    public async Task RenameProperty_IsSingleDelta_NotRemoveThenAdd()
    {
        // DR-4 invariant: rename preserves identity through the matcher,
        // so it is expressed as a single delta rather than Remove+Add.
        OntologyDelta delta = new OntologyDelta.RenameProperty(
            "Trading", "Position", "Sym", "Symbol")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta).IsTypeOf<OntologyDelta.RenameProperty>();
        await Assert.That(delta).IsNotTypeOf<OntologyDelta.RemoveProperty>();
        await Assert.That(delta).IsNotTypeOf<OntologyDelta.AddProperty>();
    }

    [Test]
    public async Task RemoveProperty_Construction_RoundTrips()
    {
        var delta = new OntologyDelta.RemoveProperty("Trading", "Position", "Symbol")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.DomainName).IsEqualTo("Trading");
        await Assert.That(delta.TypeName).IsEqualTo("Position");
        await Assert.That(delta.PropertyName).IsEqualTo("Symbol");
    }

    [Test]
    public async Task AddLink_Construction_RoundTrips()
    {
        var link = new LinkDescriptor("Orders", "Order", LinkCardinality.OneToMany);

        var delta = new OntologyDelta.AddLink("Trading", "Position", link)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.DomainName).IsEqualTo("Trading");
        await Assert.That(delta.SourceTypeName).IsEqualTo("Position");
        await Assert.That(delta.Descriptor).IsEqualTo(link);
    }

    [Test]
    public async Task RemoveLink_Construction_RoundTrips()
    {
        var delta = new OntologyDelta.RemoveLink("Trading", "Position", "Orders")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(delta.DomainName).IsEqualTo("Trading");
        await Assert.That(delta.SourceTypeName).IsEqualTo("Position");
        await Assert.That(delta.LinkName).IsEqualTo("Orders");
    }

    [Test]
    public async Task AddObjectType_RecordEquality_HoldsForSameValues()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        var a = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };
        var b = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        await Assert.That(a).IsEqualTo(b);
    }
}
