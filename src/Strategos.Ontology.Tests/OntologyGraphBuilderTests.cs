using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

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

public class OntologyGraphBuilderTests
{
    [Test]
    public async Task OntologyGraphBuilder_AddDomain_RegistersDomainOntology()
    {
        var graphBuilder = new OntologyGraphBuilder();

        graphBuilder.AddDomain<TestTradingOntology>();

        var graph = graphBuilder.Build();
        await Assert.That(graph.Domains).HasCount().EqualTo(1);
        await Assert.That(graph.Domains[0].DomainName).IsEqualTo("trading");
    }

    [Test]
    public async Task OntologyGraphBuilder_AddDomain_Multiple_AllRegistered()
    {
        var graphBuilder = new OntologyGraphBuilder();

        graphBuilder.AddDomain<TestTradingOntology>();
        graphBuilder.AddDomain<TestMarketDataOntology>();

        var graph = graphBuilder.Build();
        await Assert.That(graph.Domains).HasCount().EqualTo(2);
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

        await Assert.That(graph.Domains[0].ObjectTypes).HasCount().EqualTo(1);
        await Assert.That(graph.Domains[0].ObjectTypes[0].Name).IsEqualTo("TestPosition");
        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(1);
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

        await Assert.That(graph.Domains).HasCount().EqualTo(2);
        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(2);
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
        await Assert.That(names).HasCount().EqualTo(1);
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
        await Assert.That(names).HasCount().EqualTo(2);
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

        await Assert.That(names).HasCount().EqualTo(0);
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

        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(2);
        await Assert.That(graph.ObjectTypeNamesByType[typeof(TrackCSemanticDocument)])
            .HasCount().EqualTo(2);
    }
}
