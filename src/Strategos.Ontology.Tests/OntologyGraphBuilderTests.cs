using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Extensions;

namespace Strategos.Ontology.Tests;

public class TestPosition
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class TestInstrument
{
    public string Ticker { get; set; } = "";
    public decimal Price { get; set; }
}

public class TestTradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });
    }
}

public class TestMarketDataOntology : DomainOntology
{
    public override string DomainName => "market-data";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestInstrument>(obj =>
        {
            obj.Key(i => i.Ticker);
            obj.Property(i => i.Price).Required();
        });
    }
}

// ---------------------------------------------------------------------------
// Track C fixtures — multi-registration and duplicate descriptor names
// ---------------------------------------------------------------------------

public class TrackCFoo1
{
    public string Id { get; set; } = "";
}

public class TrackCFoo2
{
    public string Id { get; set; } = "";
}

public class TrackCBar
{
    public string Id { get; set; } = "";
}

public class TrackCFoo
{
    public string Id { get; set; } = "";
}

public class TrackCSemanticDocument
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
}

public class TrackCCollection
{
    public string Id { get; set; } = "";
}

// C1: duplicate descriptor name in same domain (two different CLR types share name)
public class TrackCDuplicateNameSameDomainOntology : DomainOntology
{
    public override string DomainName => "track-c-dup";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCFoo1>("shared_name", obj =>
        {
            obj.Key(f => f.Id);
        });

        builder.Object<TrackCFoo2>("shared_name", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}

// C1: same descriptor name across different domains — happy path
public class TrackCSharedNameDomainAOntology : DomainOntology
{
    public override string DomainName => "track-c-domain-a";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCFoo>("shared_name", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}

public class TrackCSharedNameDomainBOntology : DomainOntology
{
    public override string DomainName => "track-c-domain-b";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCBar>("shared_name", obj =>
        {
            obj.Key(b => b.Id);
        });
    }
}

// C2: single explicit-name registration (TrackCFoo is registered once by its CLR type name)
public class TrackCSingleRegistrationOntology : DomainOntology
{
    public override string DomainName => "track-c-single";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCFoo>(obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}

// C2: multi-registration — same CLR type registered under two distinct names
public class TrackCMultiRegistrationOntology : DomainOntology
{
    public override string DomainName => "track-c-multi";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCFoo>("a", obj =>
        {
            obj.Key(f => f.Id);
        });

        builder.Object<TrackCFoo>("b", obj =>
        {
            obj.Key(f => f.Id);
        });
    }
}

// C3: multi-registered type referenced as a link TARGET from another type (should throw AONT041)
public class TrackCMultiRegLinkTargetOntology : DomainOntology
{
    public override string DomainName => "track-c-link-target";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCSemanticDocument>("a", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<TrackCSemanticDocument>("b", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<TrackCCollection>(obj =>
        {
            obj.Key(c => c.Id);
            obj.HasMany<TrackCSemanticDocument>("Documents");
        });
    }
}

// C3: multi-registered type declares an outgoing link from one of its registrations (should throw AONT041)
public class TrackCMultiRegLinkSourceOntology : DomainOntology
{
    public override string DomainName => "track-c-link-source";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCSemanticDocument>("a", obj =>
        {
            obj.Key(d => d.Id);
            obj.HasMany<TrackCCollection>("Collections");
        });

        builder.Object<TrackCSemanticDocument>("b", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<TrackCCollection>(obj =>
        {
            obj.Key(c => c.Id);
        });
    }
}

// C3: multi-registered leaf type with no link references anywhere — Basileus happy path
public class TrackCMultiRegLeafOnlyOntology : DomainOntology
{
    public override string DomainName => "track-c-leaf-only";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCSemanticDocument>("a", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<TrackCSemanticDocument>("b", obj =>
        {
            obj.Key(d => d.Id);
        });
    }
}

// C3 (cross-domain): multi-registered type declared as a cross-domain link SOURCE.
// SourceDomain registers TrackCSemanticDocument under "a" and "b" and then declares
// CrossDomainLink("doc_to_target").From<TrackCSemanticDocument>().ToExternal("xd-target", "Target").
// AONT041 must fire because the From<T>() resolution would have to silently bind to one of two
// registered descriptors.
public class TrackCMultiRegCrossDomainSourceOntology : DomainOntology
{
    public override string DomainName => "xd-source";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCSemanticDocument>("a", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<TrackCSemanticDocument>("b", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.CrossDomainLink("doc_to_target")
            .From<TrackCSemanticDocument>()
            .ToExternal("xd-target", "Target")
            .ManyToMany();
    }
}

// Companion target domain for the cross-domain SOURCE test. Registers a single-registration
// target type so the cross-domain link can resolve its target half successfully.
public class TrackCCrossDomainTargetOnlyOntology : DomainOntology
{
    public override string DomainName => "xd-target";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCCollection>("Target", obj =>
        {
            obj.Key(c => c.Id);
        });
    }
}

// C3 (cross-domain): multi-registered type declared as a cross-domain link TARGET.
// TargetDomain registers TrackCSemanticDocument under "x" and "y" — both names are unique
// within the target domain (AONT040 satisfied) but the underlying CLR type is multi-registered.
// SourceDomain declares CrossDomainLink("source_to_doc").From<NormalSource>().ToExternal("xd-mr-target", "x").
// AONT041 must fire because the resolved target descriptor's CLR type appears multiple times
// in the reverse index.
public class TrackCMultiRegCrossDomainTargetOntology : DomainOntology
{
    public override string DomainName => "xd-mr-target";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCSemanticDocument>("x", obj =>
        {
            obj.Key(d => d.Id);
        });

        builder.Object<TrackCSemanticDocument>("y", obj =>
        {
            obj.Key(d => d.Id);
        });
    }
}

// Companion source domain for the cross-domain TARGET test. Registers a normal,
// single-registration source and declares the cross-domain link pointing at "x".
public class TrackCCrossDomainSourceOnlyOntology : DomainOntology
{
    public override string DomainName => "xd-mr-source";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TrackCCollection>("Collection", obj =>
        {
            obj.Key(c => c.Id);
        });

        builder.CrossDomainLink("source_to_doc")
            .From<TrackCCollection>()
            .ToExternal("xd-mr-target", "x")
            .ManyToMany();
    }
}

// Workflow chain ambiguity fixtures: AmbiguousFoo is registered under its default
// CLR-name in TWO different domains, so allObjectTypes has two entries with Name
// "AmbiguousFoo". A workflow metadata that Consumes<AmbiguousFoo>() sets the
// ConsumedTypeName to "AmbiguousFoo" — which now matches both descriptors. Under
// the warn-and-skip semantics, BuildWorkflowChains must emit a warning naming
// both domains and skip the chain rather than silently first-wins-binding.

public class AmbiguousFoo
{
    public string Id { get; set; } = string.Empty;
}

public class AmbiguousBar
{
    public string Id { get; set; } = string.Empty;
}

public class AmbiguousDomainAOntology : DomainOntology
{
    public override string DomainName => "ambiguous-a";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AmbiguousFoo>(obj => obj.Key(f => f.Id));
    }
}

public class AmbiguousDomainBOntology : DomainOntology
{
    public override string DomainName => "ambiguous-b";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AmbiguousFoo>(obj => obj.Key(f => f.Id));
    }
}

public class UnambiguousDomainOntology : DomainOntology
{
    public override string DomainName => "unambiguous";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AmbiguousFoo>(obj => obj.Key(f => f.Id));
        builder.Object<AmbiguousBar>(obj => obj.Key(b => b.Id));
    }
}

public class OntologyGraphBuilderTests
{
    [Test]
    public async Task OntologyGraphBuilder_AddDomain_RegistersDomainOntology()
    {
        var graphBuilder = new OntologyGraphBuilder();

        graphBuilder.AddDomain<TestTradingOntology>();

        var graph = graphBuilder.Build();
        await Assert.That(graph.Domains).Count().IsEqualTo(1);
        await Assert.That(graph.Domains[0].DomainName).IsEqualTo("trading");
    }

    [Test]
    public async Task OntologyGraphBuilder_AddDomain_Multiple_AllRegistered()
    {
        var graphBuilder = new OntologyGraphBuilder();

        graphBuilder.AddDomain<TestTradingOntology>();
        graphBuilder.AddDomain<TestMarketDataOntology>();

        var graph = graphBuilder.Build();
        await Assert.That(graph.Domains).Count().IsEqualTo(2);
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_ProducesOntologyGraph()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph).IsNotNull();
        await Assert.That(graph).IsTypeOf<OntologyGraph>();
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_DomainDescriptorsPopulated()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.Domains[0].ObjectTypes).Count().IsEqualTo(1);
        await Assert.That(graph.Domains[0].ObjectTypes[0].Name).IsEqualTo("TestPosition");
        await Assert.That(graph.ObjectTypes).Count().IsEqualTo(1);
        await Assert.That(graph.ObjectTypes[0].Name).IsEqualTo("TestPosition");
    }

    // -----------------------------------------------------------------------
    // Track C1 — AONT040 DuplicateObjectTypeName diagnostic
    // -----------------------------------------------------------------------

    [Test]
    public async Task GraphBuilder_WithDuplicateDescriptorNameInSameDomain_ThrowsAONT040()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCDuplicateNameSameDomainOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("AONT040");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("shared_name");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining(typeof(TrackCFoo1).FullName!);

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining(typeof(TrackCFoo2).FullName!);
    }

    [Test]
    public async Task GraphBuilder_WithSameDescriptorNameAcrossDifferentDomains_Succeeds()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCSharedNameDomainAOntology>();
        graphBuilder.AddDomain<TrackCSharedNameDomainBOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.Domains).Count().IsEqualTo(2);
        await Assert.That(graph.ObjectTypes).Count().IsEqualTo(2);
        await Assert.That(graph.ObjectTypes.Count(ot => ot.Name == "shared_name")).IsEqualTo(2);
    }

    // -----------------------------------------------------------------------
    // Track C2 — OntologyGraph.ObjectTypeNamesByType reverse index
    // -----------------------------------------------------------------------

    [Test]
    public async Task OntologyGraph_ObjectTypeNamesByType_PopulatedForSingleRegistration()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCSingleRegistrationOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypeNamesByType).IsNotNull();
        await Assert.That(graph.ObjectTypeNamesByType.ContainsKey(typeof(TrackCFoo))).IsTrue();

        var names = graph.ObjectTypeNamesByType[typeof(TrackCFoo)];
        await Assert.That(names).Count().IsEqualTo(1);
        await Assert.That(names[0]).IsEqualTo(nameof(TrackCFoo));
    }

    [Test]
    public async Task OntologyGraph_ObjectTypeNamesByType_PopulatedForMultiRegistration()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCMultiRegistrationOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypeNamesByType.ContainsKey(typeof(TrackCFoo))).IsTrue();

        var names = graph.ObjectTypeNamesByType[typeof(TrackCFoo)];
        await Assert.That(names).Count().IsEqualTo(2);
        await Assert.That(names[0]).IsEqualTo("a");
        await Assert.That(names[1]).IsEqualTo("b");
    }

    [Test]
    public async Task OntologyGraph_ObjectTypeNamesByType_UnregisteredTypeReturnsEmpty()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCSingleRegistrationOntology>();

        var graph = graphBuilder.Build();

        IReadOnlyList<string> names = graph.ObjectTypeNamesByType
            .GetValueOrDefault(typeof(TrackCBar), Array.Empty<string>());

        await Assert.That(names).Count().IsEqualTo(0);
    }

    // -----------------------------------------------------------------------
    // Track C3 — AONT041 MultiRegisteredTypeInLink invariant
    // -----------------------------------------------------------------------

    [Test]
    public async Task GraphBuilder_WithMultiRegisteredTypeAsLinkTarget_ThrowsAONT041()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCMultiRegLinkTargetOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("AONT041");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining(typeof(TrackCSemanticDocument).FullName!);

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("'a'");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("'b'");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("Documents");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("#32");
    }

    [Test]
    public async Task GraphBuilder_WithMultiRegisteredTypeAsLinkSource_ThrowsAONT041()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCMultiRegLinkSourceOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("AONT041");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining(typeof(TrackCSemanticDocument).FullName!);

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("#32");
    }

    [Test]
    public async Task GraphBuilder_WithMultiRegisteredLeafType_NoLinks_Succeeds()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCMultiRegLeafOnlyOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypes).Count().IsEqualTo(2);
        await Assert.That(graph.ObjectTypeNamesByType[typeof(TrackCSemanticDocument)])
            .Count().IsEqualTo(2);
    }

    // -----------------------------------------------------------------------
    // Track C3 — AONT041 cross-domain coverage (closes finding #6 on PR #34)
    // -----------------------------------------------------------------------

    [Test]
    public async Task GraphBuilder_WithMultiRegisteredTypeAsCrossDomainLinkSource_ThrowsAONT041()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCMultiRegCrossDomainSourceOntology>();
        graphBuilder.AddDomain<TrackCCrossDomainTargetOnlyOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("AONT041");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining(typeof(TrackCSemanticDocument).FullName!);

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("doc_to_target");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("xd-source");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("#32");
    }

    // -----------------------------------------------------------------------
    // BuildWorkflowChains — warn-and-skip on ambiguous workflow type names
    // (closes follow-up review feedback on PR #34 finding #5)
    // -----------------------------------------------------------------------

    [Test]
    public async Task GraphBuilder_WorkflowMetadata_WithAmbiguousConsumedType_EmitsWarningAndSkipsChain()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<AmbiguousDomainAOntology>();
        graphBuilder.AddDomain<AmbiguousDomainBOntology>();
        graphBuilder.AddWorkflowMetadata(new[]
        {
            new WorkflowMetadataBuilder("ambiguous-workflow")
                .Consumes<AmbiguousFoo>()
                .Produces<AmbiguousFoo>(),
        });

        var graph = graphBuilder.Build();

        // Chain must NOT be silently first-wins bound — it must be skipped entirely.
        await Assert.That(graph.WorkflowChains).Count().IsEqualTo(0);

        // A warning must name the workflow, the ambiguous type, and both domains.
        var ambiguityWarnings = graph.Warnings
            .Where(w => w.Contains("ambiguous-workflow") && w.Contains("AmbiguousFoo") && w.Contains("ambiguous"))
            .ToList();
        await Assert.That(ambiguityWarnings.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(ambiguityWarnings[0]).Contains("ambiguous-a");
        await Assert.That(ambiguityWarnings[0]).Contains("ambiguous-b");
    }

    [Test]
    public async Task GraphBuilder_WorkflowMetadata_WithUnambiguousTypes_BuildsChainCleanly()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<UnambiguousDomainOntology>();
        graphBuilder.AddWorkflowMetadata(new[]
        {
            new WorkflowMetadataBuilder("unambiguous-workflow")
                .Consumes<AmbiguousFoo>()
                .Produces<AmbiguousBar>(),
        });

        var graph = graphBuilder.Build();

        await Assert.That(graph.WorkflowChains).Count().IsEqualTo(1);
        await Assert.That(graph.WorkflowChains[0].WorkflowName).IsEqualTo("unambiguous-workflow");
        await Assert.That(graph.WorkflowChains[0].ConsumedType.Name).IsEqualTo(nameof(AmbiguousFoo));
        await Assert.That(graph.WorkflowChains[0].ProducedType.Name).IsEqualTo(nameof(AmbiguousBar));
        await Assert.That(graph.Warnings.Where(w => w.Contains("unambiguous-workflow"))).Count().IsEqualTo(0);
    }

    [Test]
    public async Task GraphBuilder_WorkflowMetadata_WithUnknownConsumedType_EmitsWarningAndSkipsChain()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<UnambiguousDomainOntology>();
        graphBuilder.AddWorkflowMetadata(new[]
        {
            new WorkflowMetadataBuilder("unknown-consumed-workflow")
                .Consumes<TrackCBar>() // not registered in UnambiguousDomainOntology
                .Produces<AmbiguousBar>(),
        });

        var graph = graphBuilder.Build();

        await Assert.That(graph.WorkflowChains).Count().IsEqualTo(0);
        var unknownWarnings = graph.Warnings
            .Where(w => w.Contains("unknown-consumed-workflow") && w.Contains("unknown") && w.Contains("TrackCBar"))
            .ToList();
        await Assert.That(unknownWarnings.Count).IsGreaterThanOrEqualTo(1);
    }

    // -----------------------------------------------------------------------
    // Track A — CrossDomainLink.Description threading to ResolvedCrossDomainLink
    // -----------------------------------------------------------------------

    [Test]
    public async Task CrossDomainLink_Description_ThreadedToResolved()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new CrossDomainDescriptionSourceOntology());
        graphBuilder.AddDomain(new CrossDomainDescriptionTargetOntology());

        var graph = graphBuilder.Build();

        await Assert.That(graph.CrossDomainLinks).Count().IsEqualTo(1);
        await Assert.That(graph.CrossDomainLinks[0].Description).IsEqualTo("Market data informs position pricing");
    }

    [Test]
    public async Task GraphBuilder_WithMultiRegisteredTypeAsCrossDomainLinkTarget_ThrowsAONT041()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TrackCCrossDomainSourceOnlyOntology>();
        graphBuilder.AddDomain<TrackCMultiRegCrossDomainTargetOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("AONT041");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining(typeof(TrackCSemanticDocument).FullName!);

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("source_to_doc");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("xd-mr-target");

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("#32");
    }
}

// ---------------------------------------------------------------------------
// Track A — cross-domain link description threading fixtures
// ---------------------------------------------------------------------------

public class CrossDomainDescriptionSourceOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });

        builder.CrossDomainLink("market_data_informs")
            .From<TestPosition>()
            .ToExternal("market-data", "TestInstrument")
            .Description("Market data informs position pricing");
    }
}

public class CrossDomainDescriptionTargetOntology : DomainOntology
{
    public override string DomainName => "market-data";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestInstrument>(obj =>
        {
            obj.Key(i => i.Ticker);
            obj.Property(i => i.Price).Required();
        });
    }
}
