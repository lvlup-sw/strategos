using System.Linq.Expressions;
using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public interface ITestSearchable
{
    string Title { get; }
    string Description { get; }
}

public record TestSearchableObject(string Symbol, string DisplayDescription);

public class IInterfaceBuilderTests
{
    [Test]
    public async Task IInterfaceBuilder_Property_AcceptsExpression()
    {
        var substitute = Substitute.For<IInterfaceBuilder<ITestSearchable>>();
        substitute.Property(Arg.Any<Expression<Func<ITestSearchable, object>>>())
            .Returns(substitute);

        var result = substitute.Property(s => s.Title);

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IInterfaceMapping_Via_MapsSourceToTarget()
    {
        var substitute = Substitute.For<IInterfaceMapping<TestSearchableObject, ITestSearchable>>();
        substitute.Via(
            Arg.Any<Expression<Func<TestSearchableObject, object>>>(),
            Arg.Any<Expression<Func<ITestSearchable, object>>>())
            .Returns(substitute);

        var result = substitute.Via(p => p.Symbol, s => s.Title);

        await Assert.That(result).IsEqualTo(substitute);
    }
}
