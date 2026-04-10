using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetIncludeTests
{
    private IObjectSetProvider _provider = null!;
    private IActionDispatcher _dispatcher = null!;
    private IEventStreamProvider _eventProvider = null!;

    [Before(Test)]
    public Task Setup()
    {
        _provider = Substitute.For<IObjectSetProvider>();
        _dispatcher = Substitute.For<IActionDispatcher>();
        _eventProvider = Substitute.For<IEventStreamProvider>();
        return Task.CompletedTask;
    }

    [Test]
    public async Task ObjectSet_Include_SetsInclusionOnExpression()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var included = set.Include(ObjectSetInclusion.Properties);

        // Assert
        await Assert.That(included.Expression).IsTypeOf<IncludeExpression>();
        var includeExpr = (IncludeExpression)included.Expression;
        await Assert.That(includeExpr.Inclusion).IsEqualTo(ObjectSetInclusion.Properties);
    }

    [Test]
    public async Task ObjectSet_Include_Schema_IncludesPropertiesActionsLinksInterfaces()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var included = set.Include(ObjectSetInclusion.Schema);

        // Assert
        var includeExpr = (IncludeExpression)included.Expression;
        var inclusion = includeExpr.Inclusion;
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Properties)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Actions)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Links)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Interfaces)).IsTrue();
    }

    [Test]
    public async Task ObjectSet_Include_Full_IncludesEverything()
    {
        // Arrange
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var included = set.Include(ObjectSetInclusion.Full);

        // Assert
        var includeExpr = (IncludeExpression)included.Expression;
        var inclusion = includeExpr.Inclusion;
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Properties)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Actions)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Links)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Interfaces)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.Events)).IsTrue();
        await Assert.That(inclusion.HasFlag(ObjectSetInclusion.LinkedObjects)).IsTrue();
    }
}
