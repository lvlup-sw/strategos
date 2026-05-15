using Microsoft.Extensions.DependencyInjection;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.MCP.Tests.Tools;

/// <summary>
/// DR-8 Task 32: <see cref="OntologyQueryService.FindObjectType"/> — invoked
/// transitively by <see cref="OntologyValidateTool"/>'s constraint-collection
/// path — must require a <c>(domain, name)</c> pair and throw on bare-name
/// ambiguity. This closes the PR #59 follow-up where a domain-agnostic
/// fallback could silently resolve to a same-named type in a different
/// domain.
/// </summary>
/// <remarks>
/// The function under test is private to <c>OntologyQueryService</c>; tests
/// drive it via the public <see cref="IOntologyQuery.GetActions(string)"/>
/// entry point (bare name) and the domain-qualified
/// <see cref="IOntologyQuery.GetActionConstraintReport(string, string, System.Collections.Generic.IReadOnlyDictionary{string, object?}?)"/>
/// overload (which routes through <c>OntologyGraph.GetObjectType(domain, name)</c>
/// directly — the unambiguous path).
/// </remarks>
public class OntologyValidateToolFindObjectTypeTests
{
    private static IOntologyQuery BuildCrossDomainQuery()
    {
        var services = new ServiceCollection();
        services.AddOntology(opts =>
        {
            opts.AddDomain<TradingOrderDomain>();
            opts.AddDomain<FulfillmentOrderDomain>();
        });
        return services.BuildServiceProvider().GetRequiredService<IOntologyQuery>();
    }

    [Test]
    public async Task FindObjectType_CrossDomainSameName_ResolvesByDomain()
    {
        // Two domains both register an "Order" descriptor. Domain-qualified
        // lookup must return the descriptor from the requested domain, not
        // silently fall through to a same-named descriptor in another domain.
        var query = BuildCrossDomainQuery();

        var tradingReports = query.GetActionConstraintReport(
            "trading", "Order", knownProperties: null);
        var fulfillmentReports = query.GetActionConstraintReport(
            "fulfillment", "Order", knownProperties: null);

        // Each domain registers a distinct action on its Order — the report
        // shapes diverge, which is the ground-truth signal that resolution
        // honored the (domain, name) tuple.
        await Assert.That(tradingReports.Any(r => r.Action.Name == "submit_order")).IsTrue();
        await Assert.That(fulfillmentReports.Any(r => r.Action.Name == "ship_order")).IsTrue();
        await Assert.That(tradingReports.Any(r => r.Action.Name == "ship_order")).IsFalse();
        await Assert.That(fulfillmentReports.Any(r => r.Action.Name == "submit_order")).IsFalse();
    }

    [Test]
    public async Task FindObjectType_AmbiguousByNameOnly_Throws()
    {
        // Bare-name lookup against an ambiguous catalog (two "Order"s across
        // domains) must throw rather than silently return the first match.
        // The diagnostic must enumerate all matching (DomainName, Name)
        // candidates so the operator can disambiguate.
        var query = BuildCrossDomainQuery();

        var exception = await Assert.That(() => query.GetActions("Order"))
            .ThrowsException()
            .WithExceptionType(typeof(InvalidOperationException));

        await Assert.That(exception!.Message).Contains("Order");
        await Assert.That(exception.Message).Contains("trading");
        await Assert.That(exception.Message).Contains("fulfillment");
    }

    [Test]
    public async Task FindObjectType_UnambiguousByNameOnly_ResolvesNormally()
    {
        // When only one domain registers a given simple name, the bare-name
        // path must still return that descriptor — the throw-on-ambiguity
        // rule only fires when there are 2+ candidates. This guards against
        // breaking the many existing IOntologyQuery callers that pass bare
        // names against single-domain graphs.
        var services = new ServiceCollection();
        services.AddOntology(opts => opts.AddDomain<TradingOrderDomain>());
        var query = services.BuildServiceProvider().GetRequiredService<IOntologyQuery>();

        var actions = query.GetActions("Order");

        await Assert.That(actions.Any(a => a.Name == "submit_order")).IsTrue();
    }
}

// Trading and Fulfillment each register an "Order" type with a distinct action
// so cross-domain resolution can be detected by *which* action shows up in
// the constraint report.
public class TradingOrder
{
    public string Id { get; set; } = "";
}

public class FulfillmentOrder
{
    public string Id { get; set; } = "";
}

public class TradingOrderDomain : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TradingOrder>("Order", obj =>
        {
            obj.Key(o => o.Id);
            obj.Action("submit_order");
        });
    }
}

public class FulfillmentOrderDomain : DomainOntology
{
    public override string DomainName => "fulfillment";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<FulfillmentOrder>("Order", obj =>
        {
            obj.Key(o => o.Id);
            obj.Action("ship_order");
        });
    }
}
