using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

/// <summary>
/// DR-5 (Task 10 + Task 11): exercises the <c>IOntologyBuilder.ApplyDelta</c>
/// switch across each <see cref="OntologyDelta"/> variant. Task 10 covers
/// <see cref="OntologyDelta.AddObjectType"/>; Task 11 covers the remaining
/// seven variants plus the unknown-variant fallthrough.
/// </summary>
public class IOntologyBuilderApplyDeltaTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private const string SourceId = "marten-typescript";

    [Test]
    public async Task ApplyDelta_AddObjectType_RegistersDescriptor()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
        };

        OntologyDelta delta = new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        };

        builder.ApplyDelta(delta);

        var built = ((OntologyBuilder)builder).ObjectTypes;
        await Assert.That(built.Count).IsEqualTo(1);
        await Assert.That(built[0].Name).IsEqualTo("Position");
        await Assert.That(built[0].Source).IsEqualTo(DescriptorSource.Ingested);
    }

    [Test]
    public async Task ApplyDelta_NullDelta_Throws()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        ArgumentNullException? caught = null;
        try
        {
            builder.ApplyDelta(null!);
        }
        catch (ArgumentNullException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
    }

    // ----- Task 11: remaining seven variants + unknown-variant fallthrough -----

    private static OntologyDelta.AddObjectType AddDelta(ObjectTypeDescriptor d) =>
        new(d) { SourceId = SourceId, Timestamp = Timestamp };

    [Test]
    public async Task ApplyDelta_UpdateObjectType_OverwritesExisting()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var original = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.HandAuthored,
        };
        var updated = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            SymbolKey = "scip ./pos.ts#Position",
        };

        builder.ApplyDelta(AddDelta(original));
        builder.ApplyDelta(new OntologyDelta.UpdateObjectType(updated)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var built = ((OntologyBuilder)builder).ObjectTypes;
        await Assert.That(built.Count).IsEqualTo(1);
        await Assert.That(built[0].Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(built[0].SymbolKey).IsEqualTo("scip ./pos.ts#Position");
    }

    [Test]
    public async Task ApplyDelta_RemoveObjectType_DropsType()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var d = new ObjectTypeDescriptor("Position", typeof(string), "Trading");
        builder.ApplyDelta(AddDelta(d));

        builder.ApplyDelta(new OntologyDelta.RemoveObjectType("Trading", "Position")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        await Assert.That(((OntologyBuilder)builder).ObjectTypes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ApplyDelta_AddProperty_AppendsToParent()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var parent = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Properties = new List<PropertyDescriptor>
            {
                new("Existing", typeof(int)),
            },
        };
        builder.ApplyDelta(AddDelta(parent));

        var newProp = new PropertyDescriptor("Symbol", typeof(string))
        {
            Source = DescriptorSource.Ingested,
        };

        builder.ApplyDelta(new OntologyDelta.AddProperty("Trading", "Position", newProp)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var built = ((OntologyBuilder)builder).ObjectTypes;
        await Assert.That(built.Count).IsEqualTo(1);
        await Assert.That(built[0].Properties.Count).IsEqualTo(2);
        await Assert.That(built[0].Properties.Any(p => p.Name == "Symbol")).IsTrue();
        await Assert.That(built[0].Properties.Single(p => p.Name == "Symbol").Source)
            .IsEqualTo(DescriptorSource.Ingested);
    }

    [Test]
    public async Task ApplyDelta_RenameProperty_PreservesIdentity()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var parent = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Properties = new List<PropertyDescriptor>
            {
                new("Sym", typeof(string)) { Source = DescriptorSource.Ingested },
            },
        };
        builder.ApplyDelta(AddDelta(parent));

        builder.ApplyDelta(new OntologyDelta.RenameProperty(
            "Trading", "Position", "Sym", "Symbol")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var built = ((OntologyBuilder)builder).ObjectTypes[0];
        await Assert.That(built.Properties.Count).IsEqualTo(1);
        await Assert.That(built.Properties[0].Name).IsEqualTo("Symbol");
        // Identity preserved: other fields including Source carry through
        await Assert.That(built.Properties[0].Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(built.Properties[0].PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task ApplyDelta_RemoveProperty_DropsByName()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var parent = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Properties = new List<PropertyDescriptor>
            {
                new("Keep", typeof(int)),
                new("Drop", typeof(int)),
            },
        };
        builder.ApplyDelta(AddDelta(parent));

        builder.ApplyDelta(new OntologyDelta.RemoveProperty("Trading", "Position", "Drop")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var built = ((OntologyBuilder)builder).ObjectTypes[0];
        await Assert.That(built.Properties.Count).IsEqualTo(1);
        await Assert.That(built.Properties[0].Name).IsEqualTo("Keep");
    }

    [Test]
    public async Task ApplyDelta_AddLink_AppendsToSourceType()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var parent = new ObjectTypeDescriptor("Position", typeof(string), "Trading");
        builder.ApplyDelta(AddDelta(parent));

        var link = new LinkDescriptor("Orders", "Order", LinkCardinality.OneToMany)
        {
            Source = DescriptorSource.Ingested,
        };

        builder.ApplyDelta(new OntologyDelta.AddLink("Trading", "Position", link)
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var built = ((OntologyBuilder)builder).ObjectTypes[0];
        await Assert.That(built.Links.Count).IsEqualTo(1);
        await Assert.That(built.Links[0].Name).IsEqualTo("Orders");
        await Assert.That(built.Links[0].Source).IsEqualTo(DescriptorSource.Ingested);
    }

    [Test]
    public async Task ApplyDelta_RemoveLink_DropsByName()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        var parent = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Links = new List<LinkDescriptor>
            {
                new("Keep", "Other", LinkCardinality.OneToOne),
                new("Drop", "Other", LinkCardinality.OneToOne),
            },
        };
        builder.ApplyDelta(AddDelta(parent));

        builder.ApplyDelta(new OntologyDelta.RemoveLink("Trading", "Position", "Drop")
        {
            SourceId = SourceId,
            Timestamp = Timestamp,
        });

        var built = ((OntologyBuilder)builder).ObjectTypes[0];
        await Assert.That(built.Links.Count).IsEqualTo(1);
        await Assert.That(built.Links[0].Name).IsEqualTo("Keep");
    }

    [Test]
    public async Task ApplyDelta_UnknownVariant_ThrowsNotSupportedException()
    {
        IOntologyBuilder builder = new OntologyBuilder("Trading");

        NotSupportedException? caught = null;
        try
        {
            builder.ApplyDelta(new UnknownVariantDelta
            {
                SourceId = SourceId,
                Timestamp = Timestamp,
            });
        }
        catch (NotSupportedException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("UnknownVariantDelta");
    }

    /// <summary>
    /// Test-only derived record exercising the unknown-variant fallthrough.
    /// The production sealed-record hierarchy is closed; this fixture lives
    /// outside it precisely so the default switch arm is reachable from
    /// test code per DR-5 AC2.
    /// </summary>
    private sealed record UnknownVariantDelta : OntologyDelta;
}
