using Strategos.Ontology.Builder;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests.Query;

// --- Test fixtures ---

public class BrPosition
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class BrOrder
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class BrAccount
{
    public string Id { get; set; } = "";
}

public class BrInstrument
{
    public string Id { get; set; } = "";
}

public class BrPortfolio
{
    public string Id { get; set; } = "";
}

public class BrAuditEntry
{
    public string Id { get; set; } = "";
}

// Single-domain ontology with two linked types: Position --has-many--> Order
public sealed class BrTradingOnePairOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrPosition>(o =>
        {
            o.Key(p => p.Id);
            o.Property(p => p.Symbol);
            o.HasMany<BrOrder>("Orders");
        });

        builder.Object<BrOrder>(o =>
        {
            o.Key(p => p.Id);
            o.Property(p => p.Symbol);
        });
    }
}

// Single-domain ontology with three types and two distinct types touched at seed
public sealed class BrTradingMultiTypeOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrPosition>(o =>
        {
            o.Key(p => p.Id);
            o.HasMany<BrOrder>("Orders");
        });

        builder.Object<BrOrder>(o =>
        {
            o.Key(o => o.Id);
        });

        builder.Object<BrAccount>(o =>
        {
            o.Key(a => a.Id);
            o.HasMany<BrPosition>("Positions");
        });
    }
}

// Two-domain ontology with one cross-domain link
public sealed class BrTradingForCrossOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrPosition>(o =>
        {
            o.Key(p => p.Id);
        });
    }
}

public sealed class BrMarketDataOntology : DomainOntology
{
    public override string DomainName => "market-data";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrInstrument>(o =>
        {
            o.Key(i => i.Id);
        });

        builder.CrossDomainLink("InstrumentReferencedBy")
            .From<BrInstrument>()
            .ToExternal("trading", "BrPosition")
            .ManyToMany();
    }
}

// Four-domain ontology: domains chained via cross-domain links
public sealed class BrFourTradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrPosition>(o => o.Key(p => p.Id));
    }
}

public sealed class BrFourPortfolioOntology : DomainOntology
{
    public override string DomainName => "portfolio";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrPortfolio>(o => o.Key(p => p.Id));

        builder.CrossDomainLink("PortfolioContainsPosition")
            .From<BrPortfolio>()
            .ToExternal("trading", "BrPosition")
            .ManyToMany();
    }
}

public sealed class BrFourMarketDataOntology : DomainOntology
{
    public override string DomainName => "market-data";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrInstrument>(o => o.Key(i => i.Id));

        builder.CrossDomainLink("InstrumentReferencedBy")
            .From<BrInstrument>()
            .ToExternal("trading", "BrPosition")
            .ManyToMany();
    }
}

public sealed class BrFourAuditOntology : DomainOntology
{
    public override string DomainName => "audit";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrAuditEntry>(o => o.Key(a => a.Id));

        builder.CrossDomainLink("AuditTracksPosition")
            .From<BrAuditEntry>()
            .ToExternal("trading", "BrPosition")
            .ManyToMany();
    }
}

// Long chain: A -> B -> C -> D -> E (single domain, link-chain) for max-degree test
public class BrChainA
{
    public string Id { get; set; } = "";
}

public class BrChainB
{
    public string Id { get; set; } = "";
}

public class BrChainC
{
    public string Id { get; set; } = "";
}

public class BrChainD
{
    public string Id { get; set; } = "";
}

public class BrChainE
{
    public string Id { get; set; } = "";
}

public sealed class BrChainOntology : DomainOntology
{
    public override string DomainName => "chain";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrChainA>(o =>
        {
            o.Key(a => a.Id);
            o.HasOne<BrChainB>("B");
        });
        builder.Object<BrChainB>(o =>
        {
            o.Key(b => b.Id);
            o.HasOne<BrChainC>("C");
        });
        builder.Object<BrChainC>(o =>
        {
            o.Key(c => c.Id);
            o.HasOne<BrChainD>("D");
        });
        builder.Object<BrChainD>(o =>
        {
            o.Key(d => d.Id);
            o.HasOne<BrChainE>("E");
        });
        builder.Object<BrChainE>(o => o.Key(e => e.Id));
    }
}

// Two-node cycle for cycle-detection test: Cycle1 -> Cycle2 -> Cycle1
public class BrCycle1
{
    public string Id { get; set; } = "";
}

public class BrCycle2
{
    public string Id { get; set; } = "";
}

public sealed class BrCycleOntology : DomainOntology
{
    public override string DomainName => "cycle";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BrCycle1>(o =>
        {
            o.Key(c => c.Id);
            o.HasOne<BrCycle2>("Next");
        });

        builder.Object<BrCycle2>(o =>
        {
            o.Key(c => c.Id);
            o.HasOne<BrCycle1>("Back");
        });
    }
}

// --- Tests ---

public class EstimateBlastRadiusTests
{
    private static IOntologyQuery CreateQuery(params DomainOntology[] domains)
    {
        var graphBuilder = new OntologyGraphBuilder();
        foreach (var d in domains)
        {
            graphBuilder.AddDomain(d);
        }

        return new OntologyQueryService(graphBuilder.Build());
    }

    [Test]
    public async Task EstimateBlastRadius_SingleDomainSeed_ReturnsLocalScope()
    {
        var query = CreateQuery(new BrTradingOnePairOntology());
        var seed = new OntologyNodeRef("trading", "BrPosition");

        var result = query.EstimateBlastRadius([seed]);

        await Assert.That(result.DirectlyAffected).HasCount().EqualTo(1);
        await Assert.That(result.DirectlyAffected[0]).IsEqualTo(seed);
        await Assert.That(result.CrossDomainHops).IsEmpty();
        // Single-type seed AND only same-type expansion would be Local; if expansion
        // reached BrOrder (a different type within same domain) then scope is Domain.
        // The plan §C1 test 1 calls this Local — interpret as "seed-only single-type
        // touched". Asserting CrossDomainHops==0 + single Domain captures the spirit.
        await Assert.That(result.Scope).IsEqualTo(BlastRadiusScope.Local);
    }

    [Test]
    public async Task EstimateBlastRadius_MultipleObjectTypesOneDomain_ReturnsDomainScope()
    {
        var query = CreateQuery(new BrTradingMultiTypeOntology());
        var seedAccount = new OntologyNodeRef("trading", "BrAccount");
        var seedOrder = new OntologyNodeRef("trading", "BrOrder");

        var result = query.EstimateBlastRadius([seedAccount, seedOrder]);

        await Assert.That(result.CrossDomainHops).IsEmpty();
        await Assert.That(result.Scope).IsEqualTo(BlastRadiusScope.Domain);
    }

    [Test]
    public async Task EstimateBlastRadius_AcrossCrossDomainLink_ReturnsCrossDomainScope()
    {
        var query = CreateQuery(new BrTradingForCrossOntology(), new BrMarketDataOntology());
        var seed = new OntologyNodeRef("trading", "BrPosition");

        var result = query.EstimateBlastRadius([seed]);

        await Assert.That(result.CrossDomainHops.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(result.Scope).IsEqualTo(BlastRadiusScope.CrossDomain);
    }

    [Test]
    public async Task EstimateBlastRadius_FourDomains_ReturnsGlobalScope()
    {
        var query = CreateQuery(
            new BrFourTradingOntology(),
            new BrFourPortfolioOntology(),
            new BrFourMarketDataOntology(),
            new BrFourAuditOntology());
        var seed = new OntologyNodeRef("trading", "BrPosition");

        var result = query.EstimateBlastRadius([seed]);

        await Assert.That(result.CrossDomainHops.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(result.Scope).IsEqualTo(BlastRadiusScope.Global);
    }

    [Test]
    public async Task EstimateBlastRadius_SameInputs_DeterministicOutput()
    {
        var query = CreateQuery(new BrTradingMultiTypeOntology());
        var seedAccount = new OntologyNodeRef("trading", "BrAccount");
        var seedOrder = new OntologyNodeRef("trading", "BrOrder");

        var first = query.EstimateBlastRadius([seedAccount, seedOrder]);
        var second = query.EstimateBlastRadius([seedAccount, seedOrder]);

        // BlastRadius is a record but its collection-typed fields use reference
        // equality under record auto-equals, so compare each list structurally.
        await Assert.That(first.Scope).IsEqualTo(second.Scope);
        await Assert.That(first.DirectlyAffected).IsEquivalentTo(second.DirectlyAffected);
        await Assert.That(first.TransitivelyAffected).IsEquivalentTo(second.TransitivelyAffected);
        await Assert.That(first.CrossDomainHops).IsEquivalentTo(second.CrossDomainHops);

        // Determinism for ordering: identical sequences (not just sets).
        for (var i = 0; i < first.DirectlyAffected.Count; i++)
        {
            await Assert.That(first.DirectlyAffected[i]).IsEqualTo(second.DirectlyAffected[i]);
        }

        for (var i = 0; i < first.TransitivelyAffected.Count; i++)
        {
            await Assert.That(first.TransitivelyAffected[i]).IsEqualTo(second.TransitivelyAffected[i]);
        }
    }

    [Test]
    public async Task EstimateBlastRadius_MaxExpansionDegree_StopsExpansion()
    {
        var query = CreateQuery(new BrChainOntology());
        var seed = new OntologyNodeRef("chain", "BrChainA");

        var result = query.EstimateBlastRadius(
            [seed],
            new BlastRadiusOptions(MaxExpansionDegree: 2));

        // With MaxExpansionDegree=2, BFS may dequeue at most 2 nodes from the
        // frontier — A (the seed) plus 2 more, capping TransitivelyAffected at 2
        // (i.e. it must NOT have reached E, the 4th-hop node).
        await Assert.That(result.TransitivelyAffected.Count).IsLessThanOrEqualTo(2);
        await Assert.That(result.TransitivelyAffected.Any(n => n.ObjectTypeName == "BrChainE")).IsFalse();
    }

    [Test]
    public async Task EstimateBlastRadius_GraphWithCycle_TerminatesAndReturnsBoundedSet()
    {
        var query = CreateQuery(new BrCycleOntology());
        var seed = new OntologyNodeRef("cycle", "BrCycle1");

        var result = query.EstimateBlastRadius([seed]);

        // Cycle: BrCycle1 -> BrCycle2 -> BrCycle1. The HashSet<OntologyNodeRef>
        // guard must prevent the seed from being re-queued. Total distinct nodes
        // observed must be {BrCycle1, BrCycle2}, with no duplicates.
        var allAffected = result.DirectlyAffected.Concat(result.TransitivelyAffected).ToList();
        await Assert.That(allAffected.Count).IsLessThanOrEqualTo(2);
        await Assert.That(allAffected.Distinct().Count()).IsEqualTo(allAffected.Count);
    }
}
