using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.MCP.Hosting.Tests;

// Test domain types mirroring the rich fixture used by Strategos.Ontology.MCP.Tests.
// Colocated in one fixture file (matching the sibling test project's
// TestOntologyGraphFactory.cs); DTO-like types are immutable records, the
// ontology classes are sealed.
public sealed record TestPosition
{
    public string Id { get; init; } = "";
    public string Symbol { get; init; } = "";
    public decimal Quantity { get; init; }
}

public sealed record TestOrder
{
    public string OrderId { get; init; } = "";
    public string PositionId { get; init; } = "";
    public decimal Amount { get; init; }
}

public sealed record TestTradeExecutionRequest
{
    public string Symbol { get; init; } = "";
    public decimal Quantity { get; init; }
}

public sealed record TestTradeExecutionResult
{
    public bool Success { get; init; }
    public string TradeId { get; init; } = "";
}

public sealed record TestTradeExecutedEvent
{
    public string TradeId { get; init; } = "";
    public string OrderId { get; init; } = "";
}

public interface ISearchable
{
    string Id { get; }
}

/// <summary>
/// A rich test domain ontology that exercises properties, links, actions, events, interfaces.
/// </summary>
public sealed class TestTradingDomainOntology : DomainOntology
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

    public static OntologyGraph CreateConstrainedGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<ConstrainedDomainOntology>();
        return builder.Build();
    }
}

// Test domain types for constrained action tests.

public sealed record TestAccount
{
    public string Id { get; init; } = "";
    public string Status { get; init; } = "active";
    public decimal Balance { get; init; }
}

public sealed record TestTransaction
{
    public string TransactionId { get; init; } = "";
    public string AccountId { get; init; } = "";
    public decimal Amount { get; init; }
}

public sealed record TestCloseAccountRequest
{
    public string Reason { get; init; } = "";
}

public sealed record TestCloseAccountResult
{
    public bool Success { get; init; }
}

/// <summary>
/// A test domain ontology with actions that carry preconditions, used to verify
/// constraint summaries survive the server-tool adapter.
/// </summary>
public sealed class ConstrainedDomainOntology : DomainOntology
{
    public override string DomainName => "banking";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestAccount>(obj =>
        {
            obj.Key(a => a.Id);
            obj.Property(a => a.Status).Required();
            obj.Property(a => a.Balance);
            obj.HasMany<TestTransaction>("Transactions");

            obj.Action("close_account")
                .Description("Close the account permanently")
                .Accepts<TestCloseAccountRequest>()
                .Returns<TestCloseAccountResult>()
                .Requires(a => a.Status == "active")
                .RequiresLink("Transactions")
                .RequiresSoft(a => a.Balance == 0);
        });

        builder.Object<TestTransaction>(obj =>
        {
            obj.Key(t => t.TransactionId);
            obj.Property(t => t.Amount).Required();
            obj.HasOne<TestAccount>("Account");
        });
    }
}
