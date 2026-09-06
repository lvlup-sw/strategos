using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public record TestSearchRequest(string Query);
public record TestSearchResult(string[] Results);

public interface ITestSearchableWithAction
{
    string Title { get; }
    string Description { get; }
}

public record TestSearchablePosition(
    Guid Id,
    string Symbol,
    string DisplayDescription,
    string SearchEmbedding)
{
    public string Title => Symbol;
}

public class InterfaceActionTests
{
    [Test]
    public async Task InterfaceBuilder_Action_AddsInterfaceActionDescriptor()
    {
        var builder = new InterfaceBuilder<ITestSearchableWithAction>("Searchable");

        builder.Property(s => s.Title);
        builder.Action("Search")
            .Description("Search using semantic similarity")
            .Accepts<TestSearchRequest>()
            .Returns<TestSearchResult>();

        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions.Count).IsEqualTo(1);
        await Assert.That(descriptor.Actions[0].Name).IsEqualTo("Search");
        await Assert.That(descriptor.Actions[0].Description).IsEqualTo("Search using semantic similarity");
        await Assert.That(descriptor.Actions[0].AcceptsTypeName).IsEqualTo("TestSearchRequest");
        await Assert.That(descriptor.Actions[0].ReturnsTypeName).IsEqualTo("TestSearchResult");
    }

    [Test]
    public async Task InterfaceBuilder_MultipleActions()
    {
        var builder = new InterfaceBuilder<ITestSearchableWithAction>("Searchable");

        builder.Action("Search");
        builder.Action("Index");

        var descriptor = builder.Build();

        await Assert.That(descriptor.Actions.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InterfaceMapping_ActionVia_CreatesMapping()
    {
        var mapping = new InterfaceMapping<TestSearchablePosition, ITestSearchableWithAction>();

        mapping.ActionVia("Search", "SearchPositions");

        var actionMappings = mapping.GetActionMappings();
        await Assert.That(actionMappings.Count).IsEqualTo(1);
        await Assert.That(actionMappings[0].InterfaceActionName).IsEqualTo("Search");
        await Assert.That(actionMappings[0].ConcreteActionName).IsEqualTo("SearchPositions");
    }

    [Test]
    public async Task InterfaceMapping_ActionDefault_CreatesMapping()
    {
        var mapping = new InterfaceMapping<TestSearchablePosition, ITestSearchableWithAction>();

        mapping.ActionDefault("Search",
            tool => tool.BoundToTool("SearchMcpTools", "SearchAsync"));

        var actionMappings = mapping.GetActionMappings();
        await Assert.That(actionMappings.Count).IsEqualTo(1);
        await Assert.That(actionMappings[0].InterfaceActionName).IsEqualTo("Search");
    }

    [Test]
    public async Task InterfaceMapping_ActionDefault_RegistersDefaultAction()
    {
        var mapping = new InterfaceMapping<TestSearchablePosition, ITestSearchableWithAction>();

        mapping.ActionDefault("Search",
            tool => tool.BoundToTool("SearchMcpTools", "SearchAsync"));

        var subject = new ActionSubject("Trading", "TestSearchablePosition");
        var defaultActions = mapping.GetDefaultActions(subject);
        await Assert.That(defaultActions.Count).IsEqualTo(1);
        await Assert.That(defaultActions[0].BoundToolName).IsEqualTo("SearchMcpTools");
        await Assert.That(defaultActions[0].Subject).IsEqualTo(subject);
    }

    [Test]
    public async Task ObjectTypeBuilder_Implements_CapturesActionMappings()
    {
        var builder = new ObjectTypeBuilder<TestSearchablePosition>("Trading");
        builder.Key(p => p.Id);
        builder.Property(p => p.Symbol);
        builder.Action("SearchPositions").Description("Search positions");

        builder.Implements<ITestSearchableWithAction>(map =>
        {
            map.Via(p => p.Symbol, s => s.Title);
            map.ActionVia("Search", "SearchPositions");
        });

        var descriptor = builder.Build();

        await Assert.That(descriptor.InterfaceActionMappings.Count).IsEqualTo(1);
        await Assert.That(descriptor.InterfaceActionMappings[0].InterfaceActionName).IsEqualTo("Search");
        await Assert.That(descriptor.InterfaceActionMappings[0].ConcreteActionName).IsEqualTo("SearchPositions");
    }

    [Test]
    public async Task ObjectTypeBuilder_Implements_ActionDefault_AddsAction()
    {
        var builder = new ObjectTypeBuilder<TestSearchablePosition>("Trading");
        builder.Key(p => p.Id);

        builder.Implements<ITestSearchableWithAction>(map =>
        {
            map.Via(p => p.Symbol, s => s.Title);
            map.ActionDefault("Search",
                tool => tool.BoundToTool("SearchMcpTools", "SearchAsync"));
        });

        var descriptor = builder.Build();

        // ActionDefault should have added a concrete action
        await Assert.That(descriptor.InterfaceActionMappings.Count).IsEqualTo(1);
        await Assert.That(descriptor.Actions.Count).IsGreaterThanOrEqualTo(1);
    }
}
