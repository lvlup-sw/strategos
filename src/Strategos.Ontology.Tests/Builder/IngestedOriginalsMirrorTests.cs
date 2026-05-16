using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

/// <summary>
/// DR-7: regression tests for the <c>_ingestedOriginals</c> snapshot the
/// <see cref="OntologyBuilder"/> retains so AONT201–AONT208 graph-freeze
/// diagnostics can diff hand-declared properties against the pre-merge
/// ingested side after MergeTwo has run.
/// </summary>
/// <remarks>
/// Three invariants under test:
///   1. Property/link <see cref="OntologyDelta"/>s mutate the ingested
///      snapshot when one exists for the same <c>(Domain, Name)</c> — the
///      snapshot would otherwise read stale pre-delta state after an
///      incremental stream.
///   2. An incoming hand-authored <c>UpdateObjectType</c> against an
///      existing ingested baseline preserves the ingested snapshot rather
///      than clearing it (the merge fold needs that baseline downstream).
///   3. <see cref="OntologyBuilder.IngestedOriginals"/> returns a
///      defensive read-only wrapper that consumers cannot down-cast and
///      mutate.
/// </remarks>
public class IngestedOriginalsMirrorTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private const string SourceId = "marten-typescript";

    private static OntologyDelta.AddObjectType IngestedAdd(
        string typeName,
        params PropertyDescriptor[] properties)
    {
        var descriptor = new ObjectTypeDescriptor
        {
            Name = typeName,
            DomainName = "Trading",
            SymbolKey = $"scip ./mod#{typeName}",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            Properties = properties,
        };
        return new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };
    }

    private static OntologyDelta.AddObjectType IngestedAddWithLinks(
        string typeName,
        params LinkDescriptor[] links)
    {
        var descriptor = new ObjectTypeDescriptor
        {
            Name = typeName,
            DomainName = "Trading",
            SymbolKey = $"scip ./mod#{typeName}",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            Links = links,
        };
        return new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };
    }

    [Test]
    public async Task AddProperty_OnIngestedBaseline_MirrorsToSnapshot()
    {
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAdd("Position", new PropertyDescriptor("Existing", typeof(int))));

        var added = new PropertyDescriptor("Symbol", typeof(string))
        {
            Source = DescriptorSource.Ingested,
        };
        b.ApplyDelta(new OntologyDelta.AddProperty("Trading", "Position", added)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var snapshot = builder.IngestedOriginals[("Trading", "Position")];
        await Assert.That(snapshot.Properties.Any(p => p.Name == "Symbol")).IsTrue();
        await Assert.That(snapshot.Properties.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RenameProperty_OnIngestedBaseline_MirrorsToSnapshot()
    {
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAdd("Position", new PropertyDescriptor("Sym", typeof(string))));

        b.ApplyDelta(new OntologyDelta.RenameProperty("Trading", "Position", "Sym", "Symbol")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var snapshot = builder.IngestedOriginals[("Trading", "Position")];
        await Assert.That(snapshot.Properties.Any(p => p.Name == "Symbol")).IsTrue();
        await Assert.That(snapshot.Properties.Any(p => p.Name == "Sym")).IsFalse();
    }

    [Test]
    public async Task RemoveProperty_OnIngestedBaseline_MirrorsToSnapshot()
    {
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAdd(
            "Position",
            new PropertyDescriptor("Keep", typeof(int)),
            new PropertyDescriptor("Drop", typeof(int))));

        b.ApplyDelta(new OntologyDelta.RemoveProperty("Trading", "Position", "Drop")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var snapshot = builder.IngestedOriginals[("Trading", "Position")];
        await Assert.That(snapshot.Properties.Any(p => p.Name == "Drop")).IsFalse();
        await Assert.That(snapshot.Properties.Any(p => p.Name == "Keep")).IsTrue();
    }

    [Test]
    public async Task AddLink_OnIngestedBaseline_MirrorsToSnapshot()
    {
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAddWithLinks("Position"));

        var link = new LinkDescriptor("Payments", "Payment", LinkCardinality.OneToMany);
        b.ApplyDelta(new OntologyDelta.AddLink("Trading", "Position", link)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var snapshot = builder.IngestedOriginals[("Trading", "Position")];
        await Assert.That(snapshot.Links.Any(l => l.Name == "Payments")).IsTrue();
    }

    [Test]
    public async Task RemoveLink_OnIngestedBaseline_MirrorsToSnapshot()
    {
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAddWithLinks(
            "Position",
            new LinkDescriptor("Payments", "Payment", LinkCardinality.OneToMany)));

        b.ApplyDelta(new OntologyDelta.RemoveLink("Trading", "Position", "Payments")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var snapshot = builder.IngestedOriginals[("Trading", "Position")];
        await Assert.That(snapshot.Links.Any(l => l.Name == "Payments")).IsFalse();
    }

    [Test]
    public async Task PropertyDelta_OnHandOnlyKey_DoesNotCreateSnapshot()
    {
        // No ingested baseline → mirror must be a no-op, not a fabrication.
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        var handAdd = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.HandAuthored,
        };
        b.ApplyDelta(new OntologyDelta.AddObjectType(handAdd)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        b.ApplyDelta(new OntologyDelta.AddProperty(
            "Trading",
            "Position",
            new PropertyDescriptor("Symbol", typeof(string)))
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        await Assert.That(builder.IngestedOriginals.ContainsKey(("Trading", "Position"))).IsFalse();
    }

    [Test]
    public async Task UpdateObjectType_HandOverIngested_PreservesIngestedSnapshot()
    {
        // DR-7 cross-provenance fold: incoming hand update against
        // existing ingested baseline must keep the snapshot so AONT201–
        // AONT208 can still diff. Previously SyncIngestedOriginal ran
        // before the merge decision and wiped the snapshot.
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAdd(
            "Position",
            new PropertyDescriptor("Symbol", typeof(string)) { Source = DescriptorSource.Ingested }));

        var handUpdate = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.HandAuthored,
            Properties = new List<PropertyDescriptor>
            {
                new("Quantity", typeof(int)) { Source = DescriptorSource.HandAuthored },
            },
        };

        b.ApplyDelta(new OntologyDelta.UpdateObjectType(handUpdate)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        await Assert.That(builder.IngestedOriginals.ContainsKey(("Trading", "Position"))).IsTrue();
        var snapshot = builder.IngestedOriginals[("Trading", "Position")];
        await Assert.That(snapshot.Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(snapshot.Properties.Any(p => p.Name == "Symbol")).IsTrue();
    }

    [Test]
    public async Task UpdateObjectType_HandReplacesHand_ClearsAnyStaleSnapshot()
    {
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        var handAdd = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.HandAuthored,
        };
        b.ApplyDelta(new OntologyDelta.AddObjectType(handAdd)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var handUpdate = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.HandAuthored,
        };
        b.ApplyDelta(new OntologyDelta.UpdateObjectType(handUpdate)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        await Assert.That(builder.IngestedOriginals.ContainsKey(("Trading", "Position"))).IsFalse();
    }

    [Test]
    public async Task IngestedOriginals_ReturnsDefensiveReadOnlyWrapper()
    {
        // Consumers must not be able to down-cast the property's
        // IReadOnlyDictionary to Dictionary and mutate it. Returning a
        // fresh ReadOnlyDictionary each call prevents this.
        var builder = new OntologyBuilder("Trading");
        IOntologyBuilder b = builder;

        b.ApplyDelta(IngestedAdd("Position"));

        var snapshot = builder.IngestedOriginals;

        await Assert.That(snapshot is Dictionary<(string, string), ObjectTypeDescriptor>).IsFalse();
    }
}
