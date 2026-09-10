using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP.Tests;

// Test domain types
public class TestPosition
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class TestOrder
{
    public string OrderId { get; set; } = "";
    public string PositionId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class TestTradeExecutionRequest
{
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
}

public class TestTradeExecutionResult
{
    public bool Success { get; set; }
    public string TradeId { get; set; } = "";
}

public class TestTradeExecutedEvent
{
    public string TradeId { get; set; } = "";
    public string OrderId { get; set; } = "";
}

public interface ISearchable
{
    string Id { get; }
}

/// <summary>
/// A rich test domain ontology that exercises all features:
/// properties, links, actions, events, interfaces.
/// </summary>
public class TestTradingDomainOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ISearchable>("Searchable", intf =>
        {
            intf.Property(s => s.Id);
        });

        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.Quantity);
            obj.HasMany<TestOrder>("Orders")
                .Description("Orders placed against this position");
            obj.Action("execute_trade")
                .Description("Execute a trade on the position")
                .Accepts<TestTradeExecutionRequest>()
                .Returns<TestTradeExecutionResult>();
            obj.Event<TestTradeExecutedEvent>(evt =>
            {
                evt.Description("Trade was executed");
                evt.MaterializesLink<TestPosition>("Orders", e => e.OrderId);
                evt.Severity(EventSeverity.Info);
            });
            obj.Implements<ISearchable>(map =>
            {
                map.Via(p => p.Id, s => s.Id);
            });
        });

        builder.Object<TestOrder>(obj =>
        {
            obj.Key(o => o.OrderId);
            obj.Property(o => o.Amount).Required();
            obj.HasOne<TestPosition>("Position");
        });
    }
}

/// <summary>
/// Factory to build test OntologyGraph instances using the builder.
/// </summary>
public static class TestOntologyGraphFactory
{
    public static OntologyGraph CreateTradingGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<TestTradingDomainOntology>();
        return builder.Build();
    }

    public static OntologyGraph CreateVectorGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<TestVectorDomainOntology>();
        return builder.Build();
    }

    /// <summary>
    /// A graph carrying a reified association (DR-4): two <see cref="TestParty"/>
    /// entities linked by a <see cref="TestCounterparty"/> association with a
    /// Buyer/Seller endpoint pair and a <c>Role</c> edge attribute. Used by the
    /// DR-15 association/traversal MCP surface tests.
    /// </summary>
    public static OntologyGraph CreateAssociationGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<TestAssociationDomainOntology>();
        return builder.Build();
    }
}

// Test types for the reified-association domain (DR-15).
public class TestParty
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TestCounterparty
{
    public string Id { get; set; } = "";
    public TestParty Buyer { get; set; } = new();
    public TestParty Seller { get; set; } = new();
    public string Role { get; set; } = "";
}

/// <summary>
/// A domain with a plain entity (<see cref="TestParty"/>) and a reified
/// association (<see cref="TestCounterparty"/>) so association objects can be
/// exercised distinctly from plain objects.
/// </summary>
public class TestAssociationDomainOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestParty>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Name).Required();
            obj.HasMany<TestParty>("counterparties");
        });

        builder.Association<TestCounterparty>("TestCounterparty", a =>
        {
            a.Key(c => c.Id);
            a.Between(c => c.Buyer).And(c => c.Seller);
            a.Property(c => c.Role).Required();
        });
    }
}

// Test types for vector domain
public class TestDocument
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public float[] Embedding { get; set; } = [];
}

public class TestImage
{
    public string ImageId { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// A test domain ontology with vector properties for testing semantic search support.
/// </summary>
public class TestVectorDomainOntology : DomainOntology
{
    public override string DomainName => "content";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestDocument>(obj =>
        {
            obj.Key(d => d.Id);
            obj.Property(d => d.Title).Required();
            obj.Property(d => d.Embedding).Vector(1536);
        });

        builder.Object<TestImage>(obj =>
        {
            obj.Key(i => i.ImageId);
            obj.Property(i => i.Description);
        });
    }
}
