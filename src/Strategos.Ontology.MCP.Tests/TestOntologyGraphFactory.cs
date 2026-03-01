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
            obj.HasMany<TestOrder>("Orders");
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
}
