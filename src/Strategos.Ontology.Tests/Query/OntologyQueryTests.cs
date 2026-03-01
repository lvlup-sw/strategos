using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;
using Strategos.Ontology.Tests.Builder;

namespace Strategos.Ontology.Tests.Query;

// --- Test domain models for query tests ---

public enum QueryTestStatus
{
    Draft,
    Active,
    Closed,
}

public record QueryPosition(
    Guid Id,
    string Symbol,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal UnrealizedPnL,
    decimal PortfolioWeight,
    QueryTestStatus Status);

public record QueryOrder(Guid Id, string Symbol, decimal Amount);

public record QueryNote(Guid Id, string Title, string Content);

public record QueryTradeExecutedEvent(Guid OrderId);

public interface IQuerySearchable
{
    string Title { get; }
}

// --- Domain ontologies for query tests ---

public class QueryTradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IQuerySearchable>("Searchable", iface =>
        {
            iface.Property(s => s.Title);
            iface.Action("Search")
                .Description("Semantic search")
                .Accepts<string>()
                .Returns<string[]>();
        });

        builder.Object<QueryPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.Quantity);
            obj.Property(p => p.AverageCost);
            obj.Property(p => p.CurrentPrice);
            obj.Property(p => p.UnrealizedPnL)
                .Computed()
                .DerivedFrom(p => p.Quantity, p => p.AverageCost, p => p.CurrentPrice);
            obj.Property(p => p.PortfolioWeight)
                .Computed()
                .DerivedFrom(p => p.UnrealizedPnL);
            obj.Property(p => p.Status);

            obj.HasMany<QueryOrder>("Orders");

            obj.Action("OpenPosition")
                .Description("Open a new position");
            obj.Action("ExecuteTrade")
                .Description("Execute a trade")
                .Requires(p => p.Status == QueryTestStatus.Active)
                .RequiresLink("Orders")
                .Modifies(p => p.Quantity)
                .Modifies(p => p.UnrealizedPnL)
                .CreatesLinked<QueryOrder>("Orders")
                .EmitsEvent<QueryTradeExecutedEvent>();
            obj.Action("ClosePosition")
                .Description("Close the position")
                .Requires(p => p.Quantity > 0);

            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<QueryTestStatus>>)(lifecycle =>
            {
                lifecycle.State(QueryTestStatus.Draft)
                    .Initial();
                lifecycle.State(QueryTestStatus.Active);
                lifecycle.State(QueryTestStatus.Closed)
                    .Terminal();

                lifecycle.Transition(QueryTestStatus.Draft, QueryTestStatus.Active)
                    .TriggeredByAction("OpenPosition");
                lifecycle.Transition(QueryTestStatus.Active, QueryTestStatus.Active)
                    .TriggeredByAction("ExecuteTrade");
                lifecycle.Transition(QueryTestStatus.Active, QueryTestStatus.Closed)
                    .TriggeredByAction("ClosePosition");
            }));

            obj.Implements<IQuerySearchable>(map =>
            {
                map.Via(p => p.Symbol, s => s.Title);
                map.ActionVia("Search", "ExecuteTrade");
            });

            obj.AcceptsExternalLinks("KnowledgeLinks", ext =>
            {
                ext.FromInterface<IQuerySearchable>();
                ext.Description("Knowledge sources");
            });
        });

        builder.Object<QueryOrder>(obj =>
        {
            obj.Key(o => o.Id);
            obj.Property(o => o.Symbol).Required();
            obj.Property(o => o.Amount);
            obj.Action("CancelOrder").Description("Cancel the order");
        });
    }
}

public class QueryKnowledgeOntology : DomainOntology
{
    public override string DomainName => "knowledge";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<IQuerySearchable>("Searchable", iface =>
        {
            iface.Property(s => s.Title);
            iface.Action("Search")
                .Description("Semantic search")
                .Accepts<string>()
                .Returns<string[]>();
        });

        builder.Object<QueryNote>(obj =>
        {
            obj.Key(n => n.Id);
            obj.Property(n => n.Title).Required();
            obj.Property(n => n.Content);

            obj.Action("Publish").Description("Publish the note");

            obj.Implements<IQuerySearchable>(map =>
            {
                map.Via(n => n.Title, s => s.Title);
                map.ActionVia("Search", "Publish");
            });
        });

        builder.CrossDomainLink("NoteInformsPosition")
            .From<QueryNote>()
            .ToExternal("trading", "QueryPosition")
            .ManyToMany();
    }
}

// --- Helper to build the shared test graph ---

public static class QueryTestGraphFactory
{
    public static OntologyGraph Build()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new QueryTradingOntology());
        graphBuilder.AddDomain(new QueryKnowledgeOntology());
        return graphBuilder.Build();
    }

    public static IOntologyQuery CreateQueryService()
    {
        return new OntologyQueryService(Build());
    }
}

// --- Tests ---

public class OntologyQueryServiceCoreTests
{
    [Test]
    public async Task GetObjectTypes_NoFilter_ReturnsAll()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var types = query.GetObjectTypes();

        await Assert.That(types.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetObjectTypes_FilterByDomain_ReturnsOnlyDomain()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var types = query.GetObjectTypes(domain: "trading");

        await Assert.That(types.Count).IsEqualTo(2);
        await Assert.That(types.All(t => t.DomainName == "trading")).IsTrue();
    }

    [Test]
    public async Task GetObjectTypes_FilterByInterface_ReturnsImplementors()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var types = query.GetObjectTypes(implementsInterface: "IQuerySearchable");

        await Assert.That(types.Count).IsEqualTo(2);
        var names = types.Select(t => t.Name).OrderBy(n => n).ToList();
        await Assert.That(names[0]).IsEqualTo("QueryNote");
        await Assert.That(names[1]).IsEqualTo("QueryPosition");
    }

    [Test]
    public async Task GetObjectTypes_FilterByDomainAndInterface_ReturnsIntersection()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var types = query.GetObjectTypes(domain: "knowledge", implementsInterface: "IQuerySearchable");

        await Assert.That(types.Count).IsEqualTo(1);
        await Assert.That(types[0].Name).IsEqualTo("QueryNote");
    }

    [Test]
    public async Task GetObjectTypes_NoMatch_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var types = query.GetObjectTypes(domain: "nonexistent");

        await Assert.That(types.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetActions_ValidObjectType_ReturnsActions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetActions("QueryPosition");

        await Assert.That(actions.Count).IsEqualTo(3);
        var names = actions.Select(a => a.Name).OrderBy(n => n).ToList();
        await Assert.That(names).Contains("OpenPosition");
        await Assert.That(names).Contains("ExecuteTrade");
        await Assert.That(names).Contains("ClosePosition");
    }

    [Test]
    public async Task GetActions_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetActions("NonExistent");

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetLinks_ValidObjectType_ReturnsLinks()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var links = query.GetLinks("QueryPosition");

        await Assert.That(links.Count).IsEqualTo(1);
        await Assert.That(links[0].Name).IsEqualTo("Orders");
    }

    [Test]
    public async Task GetLinks_ObjectWithNoLinks_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var links = query.GetLinks("QueryOrder");

        await Assert.That(links.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetImplementors_ReturnsImplementingTypes()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var implementors = query.GetImplementors("IQuerySearchable");

        await Assert.That(implementors.Count).IsEqualTo(2);
    }

    [Test]
    public async Task GetImplementors_UnknownInterface_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var implementors = query.GetImplementors("NonExistentInterface");

        await Assert.That(implementors.Count).IsEqualTo(0);
    }
}

public class OntologyQueryServicePreconditionTests
{
    [Test]
    public async Task GetValidActions_NoKnownProperties_ReturnsAll()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetValidActions("QueryPosition");

        await Assert.That(actions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetValidActions_WithKnownProperties_FiltersActions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = QueryTestStatus.Active,
            ["Quantity"] = 100m,
            ["Orders"] = true, // link exists
        };

        var actions = query.GetValidActions("QueryPosition", knownProps);

        // All actions should be returned since:
        // - OpenPosition has no preconditions
        // - ExecuteTrade has PropertyPredicate (Status==Active) + LinkExists (Orders=true)
        // - ClosePosition has PropertyPredicate (Quantity > 0) -> we provide Quantity=100
        await Assert.That(actions.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetValidActions_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetValidActions("NonExistent");

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetValidActions_PropertyPredicateUnsat_FiltersAction()
    {
        var query = QueryTestGraphFactory.CreateQueryService();
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = QueryTestStatus.Closed,
        };

        var actions = query.GetValidActions("QueryPosition", knownProps);

        // ExecuteTrade requires Status == Active, but we have Status == Closed
        // So ExecuteTrade should be filtered out
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).DoesNotContain("ExecuteTrade");
    }

    [Test]
    public async Task GetValidActions_PropertyPredicateSat_IncludesAction()
    {
        var query = QueryTestGraphFactory.CreateQueryService();
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = QueryTestStatus.Active,
            ["Quantity"] = 100m,
            ["Orders"] = true, // link exists
        };

        var actions = query.GetValidActions("QueryPosition", knownProps);

        // ExecuteTrade requires Status == Active (satisfied) and link Orders (satisfied)
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).Contains("ExecuteTrade");
    }

    [Test]
    public async Task GetValidActions_LinkExistsUnsat_FiltersAction()
    {
        var query = QueryTestGraphFactory.CreateQueryService();
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = QueryTestStatus.Active,
            // No "Orders" key — link does not exist
        };

        var actions = query.GetValidActions("QueryPosition", knownProps);

        // ExecuteTrade requires link "Orders" to exist, but no link info provided
        // Should be filtered out
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).DoesNotContain("ExecuteTrade");
    }

    [Test]
    public async Task GetValidActions_LinkExistsSat_IncludesAction()
    {
        var query = QueryTestGraphFactory.CreateQueryService();
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = QueryTestStatus.Active,
            ["Orders"] = true, // link exists
        };

        var actions = query.GetValidActions("QueryPosition", knownProps);

        // ExecuteTrade requires Status == Active (satisfied) and link Orders (satisfied via bool)
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).Contains("ExecuteTrade");
    }

    [Test]
    public async Task GetValidActions_NoPreconditions_IncludesAll()
    {
        var query = QueryTestGraphFactory.CreateQueryService();
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = QueryTestStatus.Closed,
        };

        var actions = query.GetValidActions("QueryPosition", knownProps);

        // OpenPosition has no preconditions, so it should always be included
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).Contains("OpenPosition");
    }

    [Test]
    public async Task TracePostconditions_ValidAction_ReturnsTraces()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var traces = query.TracePostconditions("QueryPosition", "ExecuteTrade");

        await Assert.That(traces.Count).IsEqualTo(4);
        await Assert.That(traces.All(t => t.ActionName == "ExecuteTrade")).IsTrue();
        await Assert.That(traces.All(t => t.AffectedObjectType == "QueryPosition")).IsTrue();
    }

    [Test]
    public async Task TracePostconditions_ModifiesPropertyPostconditions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var traces = query.TracePostconditions("QueryPosition", "ExecuteTrade");
        var modifies = traces.Where(t => t.Postcondition.Kind == PostconditionKind.ModifiesProperty).ToList();

        await Assert.That(modifies.Count).IsEqualTo(2);
        var propNames = modifies.Select(m => m.Postcondition.PropertyName).OrderBy(n => n).ToList();
        await Assert.That(propNames).Contains("Quantity");
        await Assert.That(propNames).Contains("UnrealizedPnL");
    }

    [Test]
    public async Task TracePostconditions_EmitsEventPostcondition()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var traces = query.TracePostconditions("QueryPosition", "ExecuteTrade");
        var emits = traces.Where(t => t.Postcondition.Kind == PostconditionKind.EmitsEvent).ToList();

        await Assert.That(emits.Count).IsEqualTo(1);
        await Assert.That(emits[0].Postcondition.EventTypeName).IsEqualTo("QueryTradeExecutedEvent");
    }

    [Test]
    public async Task TracePostconditions_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var traces = query.TracePostconditions("NonExistent", "SomeAction");

        await Assert.That(traces.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TracePostconditions_UnknownAction_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var traces = query.TracePostconditions("QueryPosition", "NonExistentAction");

        await Assert.That(traces.Count).IsEqualTo(0);
    }
}

public class OntologyQueryServiceLifecycleTests
{
    [Test]
    public async Task GetActionsForState_DraftState_ReturnsActivationAction()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetActionsForState("QueryPosition", "Draft");

        // OpenPosition triggers Draft->Active, so it should be included
        // Plus any actions not bound to lifecycle transitions
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).Contains("OpenPosition");
    }

    [Test]
    public async Task GetActionsForState_ActiveState_ReturnsTradeAndCloseActions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetActionsForState("QueryPosition", "Active");

        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).Contains("ExecuteTrade");
        await Assert.That(names).Contains("ClosePosition");
    }

    [Test]
    public async Task GetActionsForState_ClosedState_NoTransitionsOut()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetActionsForState("QueryPosition", "Closed");

        // Terminal state: no actions trigger transitions FROM Closed
        // Only non-lifecycle-bound actions should remain (there are none for QueryPosition)
        var names = actions.Select(a => a.Name).ToList();
        await Assert.That(names).DoesNotContain("OpenPosition");
        await Assert.That(names).DoesNotContain("ExecuteTrade");
        await Assert.That(names).DoesNotContain("ClosePosition");
    }

    [Test]
    public async Task GetActionsForState_ObjectWithNoLifecycle_ReturnsAllActions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetActionsForState("QueryOrder", "AnyState");

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Name).IsEqualTo("CancelOrder");
    }

    [Test]
    public async Task GetTransitionsFrom_DraftState_ReturnsOneTransition()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var transitions = query.GetTransitionsFrom("QueryPosition", "Draft");

        await Assert.That(transitions.Count).IsEqualTo(1);
        await Assert.That(transitions[0].FromState).IsEqualTo("Draft");
        await Assert.That(transitions[0].ToState).IsEqualTo("Active");
        await Assert.That(transitions[0].TriggerActionName).IsEqualTo("OpenPosition");
    }

    [Test]
    public async Task GetTransitionsFrom_ActiveState_ReturnsTwoTransitions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var transitions = query.GetTransitionsFrom("QueryPosition", "Active");

        await Assert.That(transitions.Count).IsEqualTo(2);
        var toStates = transitions.Select(t => t.ToState).OrderBy(s => s).ToList();
        await Assert.That(toStates).Contains("Active");
        await Assert.That(toStates).Contains("Closed");
    }

    [Test]
    public async Task GetTransitionsFrom_ClosedState_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var transitions = query.GetTransitionsFrom("QueryPosition", "Closed");

        await Assert.That(transitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetTransitionsFrom_NoLifecycle_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var transitions = query.GetTransitionsFrom("QueryOrder", "AnyState");

        await Assert.That(transitions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetTransitionsFrom_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var transitions = query.GetTransitionsFrom("NonExistent", "Active");

        await Assert.That(transitions.Count).IsEqualTo(0);
    }
}

public class OntologyQueryServiceDerivationTests
{
    [Test]
    public async Task GetAffectedProperties_DirectDependency()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var affected = query.GetAffectedProperties("QueryPosition", "Quantity");

        // UnrealizedPnL directly depends on Quantity
        var direct = affected.Where(a => a.IsDirect).ToList();
        await Assert.That(direct.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(direct.Any(a => a.PropertyName == "UnrealizedPnL")).IsTrue();
    }

    [Test]
    public async Task GetAffectedProperties_TransitiveDependency()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var affected = query.GetAffectedProperties("QueryPosition", "Quantity");

        // PortfolioWeight transitively depends on Quantity (via UnrealizedPnL)
        var transitive = affected.Where(a => !a.IsDirect).ToList();
        await Assert.That(transitive.Any(a => a.PropertyName == "PortfolioWeight")).IsTrue();
    }

    [Test]
    public async Task GetAffectedProperties_LeafProperty_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var affected = query.GetAffectedProperties("QueryPosition", "Symbol");

        await Assert.That(affected.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetAffectedProperties_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var affected = query.GetAffectedProperties("NonExistent", "Quantity");

        await Assert.That(affected.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetDerivationChain_ComputedProperty_ReturnsChain()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var chain = query.GetDerivationChain("QueryPosition", "UnrealizedPnL");

        // UnrealizedPnL depends on Quantity, AverageCost, CurrentPrice
        await Assert.That(chain.Count).IsEqualTo(3);
        var propNames = chain.Select(s => s.PropertyName).OrderBy(n => n).ToList();
        await Assert.That(propNames).Contains("AverageCost");
        await Assert.That(propNames).Contains("CurrentPrice");
        await Assert.That(propNames).Contains("Quantity");
    }

    [Test]
    public async Task GetDerivationChain_TransitiveProperty_ReturnsFullChain()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var chain = query.GetDerivationChain("QueryPosition", "PortfolioWeight");

        // PortfolioWeight -> UnrealizedPnL -> Quantity, AverageCost, CurrentPrice (transitive)
        await Assert.That(chain.Count).IsEqualTo(4);
    }

    [Test]
    public async Task GetDerivationChain_NonComputedProperty_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var chain = query.GetDerivationChain("QueryPosition", "Symbol");

        await Assert.That(chain.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetDerivationChain_UnknownProperty_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var chain = query.GetDerivationChain("QueryPosition", "NonExistentProp");

        await Assert.That(chain.Count).IsEqualTo(0);
    }
}

public class OntologyQueryServiceInterfaceActionTests
{
    [Test]
    public async Task GetInterfaceActions_ReturnsDefinedActions()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetInterfaceActions("Searchable");

        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Name).IsEqualTo("Search");
        await Assert.That(actions[0].Description).IsEqualTo("Semantic search");
    }

    [Test]
    public async Task GetInterfaceActions_UnknownInterface_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var actions = query.GetInterfaceActions("NonExistentInterface");

        await Assert.That(actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ResolveInterfaceAction_MappedAction_ReturnsConcreteAction()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var action = query.ResolveInterfaceAction("QueryPosition", "Search");

        await Assert.That(action).IsNotNull();
        await Assert.That(action!.Name).IsEqualTo("ExecuteTrade");
    }

    [Test]
    public async Task ResolveInterfaceAction_UnmappedAction_ReturnsNull()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var action = query.ResolveInterfaceAction("QueryOrder", "Search");

        await Assert.That(action).IsNull();
    }

    [Test]
    public async Task ResolveInterfaceAction_UnknownObjectType_ReturnsNull()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var action = query.ResolveInterfaceAction("NonExistent", "Search");

        await Assert.That(action).IsNull();
    }
}

public class OntologyQueryServiceExtensionPointTests
{
    [Test]
    public async Task GetExtensionPoints_ReturnsDefinedPoints()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var points = query.GetExtensionPoints("QueryPosition");

        await Assert.That(points.Count).IsEqualTo(1);
        await Assert.That(points[0].Name).IsEqualTo("KnowledgeLinks");
        await Assert.That(points[0].Description).IsEqualTo("Knowledge sources");
    }

    [Test]
    public async Task GetExtensionPoints_NoExtensionPoints_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var points = query.GetExtensionPoints("QueryOrder");

        await Assert.That(points.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetExtensionPoints_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var points = query.GetExtensionPoints("NonExistent");

        await Assert.That(points.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetIncomingCrossDomainLinks_ReturnsLinks()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var links = query.GetIncomingCrossDomainLinks("QueryPosition");

        await Assert.That(links.Count).IsEqualTo(1);
        await Assert.That(links[0].Name).IsEqualTo("NoteInformsPosition");
        await Assert.That(links[0].SourceDomain).IsEqualTo("knowledge");
        await Assert.That(links[0].TargetDomain).IsEqualTo("trading");
    }

    [Test]
    public async Task GetIncomingCrossDomainLinks_NoIncoming_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var links = query.GetIncomingCrossDomainLinks("QueryNote");

        await Assert.That(links.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetIncomingCrossDomainLinks_UnknownObjectType_ReturnsEmpty()
    {
        var query = QueryTestGraphFactory.CreateQueryService();

        var links = query.GetIncomingCrossDomainLinks("NonExistent");

        await Assert.That(links.Count).IsEqualTo(0);
    }
}

public class OntologyQueryServiceDiTests
{
    [Test]
    public async Task AddOntology_RegistersIOntologyQueryAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<QueryTradingOntology>();
            options.AddDomain<QueryKnowledgeOntology>();
        });

        var provider = services.BuildServiceProvider();
        var query1 = provider.GetRequiredService<IOntologyQuery>();
        var query2 = provider.GetRequiredService<IOntologyQuery>();

        await Assert.That(query1).IsNotNull();
        await Assert.That(query1).IsSameReferenceAs(query2);
    }

    [Test]
    public async Task AddOntology_IOntologyQuery_CanQueryRegisteredTypes()
    {
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<QueryTradingOntology>();
            options.AddDomain<QueryKnowledgeOntology>();
        });

        var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<IOntologyQuery>();

        var types = query.GetObjectTypes();
        await Assert.That(types.Count).IsEqualTo(3);
    }
}
