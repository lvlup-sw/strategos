using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

/// <summary>
/// Shared fixture domain ontologies for <see cref="OntologyGraphVersionTests"/>.
/// Each domain class is a controlled minimal shape so that hash-sensitivity
/// tests can isolate exactly one structural mutation per pair of fixtures.
/// </summary>
internal sealed class VersionFixtureA : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        // Empty domain — baseline for "added object type" sensitivity tests.
    }
}

internal sealed class VersionFixtureWithOneType : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
    }
}

internal sealed class VersionFixtureWithExtraProperty : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Property(w => w.Name);
        });
    }
}

internal sealed class VersionFixtureWithActionA : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Action("doThing");
        });
    }
}

internal sealed class VersionFixtureWithActionB : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Action("doOtherThing");
        });
    }
}

internal sealed class VersionFixtureWithLink : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.HasMany<VersionGadget>("Gadgets");
        });
        builder.Object<VersionGadget>(o => o.Key(g => g.Id));
    }
}

internal sealed class VersionFixtureWithLinkSibling : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
        builder.Object<VersionGadget>(o => o.Key(g => g.Id));
    }
}

internal sealed class VersionFixtureWithEvent : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Event<WidgetCreated>(_ => { });
        });
    }
}

internal sealed class VersionFixtureWithoutEvent : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
    }
}

internal sealed class VersionFixtureWithLifecyclePending : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidgetWithStatus>(o =>
        {
            o.Key(w => w.Id);
            o.Property(w => w.Status);
            o.Lifecycle<WidgetStatus>(w => w.Status, l =>
            {
                l.State(WidgetStatus.Pending).Initial();
                l.State(WidgetStatus.Closed).Terminal();
            });
        });
    }
}

internal sealed class VersionFixtureWithLifecyclePendingPlusActive : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidgetWithStatus>(o =>
        {
            o.Key(w => w.Id);
            o.Property(w => w.Status);
            o.Lifecycle<WidgetStatus>(w => w.Status, l =>
            {
                l.State(WidgetStatus.Pending).Initial();
                l.State(WidgetStatus.Active);
                l.State(WidgetStatus.Closed).Terminal();
            });
        });
    }
}

internal sealed class VersionFixtureWithImplementedInterface : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IVersionLabeled>("Labeled", i =>
        {
            i.Property(x => x.Id);
        });
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Implements<IVersionLabeled>(map => map.Via(w => w.Id, x => x.Id));
        });
    }
}

internal sealed class VersionFixtureWithoutImplementedInterface : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IVersionLabeled>("Labeled", i =>
        {
            i.Property(x => x.Id);
        });
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
    }
}

internal sealed class VersionFixtureWithoutInterface : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
    }
}

internal sealed class VersionFixtureWithInterface : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IVersionLabeled>("Labeled", i =>
        {
            i.Property(x => x.Id);
        });
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
    }
}

internal sealed class VersionFixtureWithActionDescriptionA : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Action("doThing").Description("first description");
        });
    }
}

internal sealed class VersionFixtureWithActionDescriptionB : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Action("doThing").Description("second different description text");
        });
    }
}

internal sealed class VersionFixtureWithLinkDescriptionA : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.HasMany<VersionGadget>("Gadgets").Description("first description");
        });
        builder.Object<VersionGadget>(o => o.Key(g => g.Id));
    }
}

internal sealed class VersionFixtureWithLinkDescriptionB : DomainOntology
{
    public override string DomainName => "fixture";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o =>
        {
            o.Key(w => w.Id);
            o.HasMany<VersionGadget>("Gadgets").Description("second different description");
        });
        builder.Object<VersionGadget>(o => o.Key(g => g.Id));
    }
}

// Cross-domain link source/target — used by A3 cross-domain link tests.
internal sealed class VersionFixtureXSource : DomainOntology
{
    public override string DomainName => "source";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
    }
}

internal sealed class VersionFixtureXSourceWithLink : DomainOntology
{
    public override string DomainName => "source";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionWidget>(o => o.Key(w => w.Id));
        builder.CrossDomainLink("WidgetToGadget")
            .From<VersionWidget>()
            .ToExternal("target", nameof(VersionGadget))
            .ManyToMany();
    }
}

internal sealed class VersionFixtureXTarget : DomainOntology
{
    public override string DomainName => "target";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionGadget>(o => o.Key(g => g.Id));
    }
}

// Reference fixture for A5 — small two-domain fixture pinning a known hash.
// Uses distinct CLR types per domain to avoid AONT041 (multi-registered types
// cannot participate in structural links).
internal sealed class VersionReferenceFixtureSource : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionRefSourceWidget>(o =>
        {
            o.Key(w => w.Id);
            o.Property(w => w.Name);
            o.HasMany<VersionRefSourceLocal>("Locals");
            o.Action("doThing");
        });
        builder.Object<VersionRefSourceLocal>(o => o.Key(g => g.Id));
        builder.CrossDomainLink("WidgetToInstrument")
            .From<VersionRefSourceWidget>()
            .ToExternal("market-data", nameof(VersionRefTargetInstrument))
            .ManyToMany();
    }
}

internal sealed class VersionReferenceFixtureTarget : DomainOntology
{
    public override string DomainName => "market-data";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<VersionRefTargetInstrument>(o => o.Key(g => g.Id));
    }
}

public sealed class VersionRefSourceWidget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class VersionRefSourceLocal
{
    public string Id { get; set; } = "";
}

public sealed class VersionRefTargetInstrument
{
    public string Id { get; set; } = "";
}

// CLR types used by the fixture domains above. Kept top-level so the builder
// reflection layer (which inspects ClrType.FullName) can name them.
public sealed class VersionWidget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class VersionWidgetWithStatus
{
    public string Id { get; set; } = "";
    public WidgetStatus Status { get; set; }
}

public sealed class VersionGadget
{
    public string Id { get; set; } = "";
}

public sealed class WidgetCreated
{
    public string Id { get; set; } = "";
}

public enum WidgetStatus
{
    Pending,
    Active,
    Closed,
}

public interface IVersionLabeled
{
    string Id { get; }
}
