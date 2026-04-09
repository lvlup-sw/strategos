using System.Linq.Expressions;
using Strategos.Ontology.Npgsql.Internal;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Tests.Internal;

public class ExpressionTranslatorTests
{
    [Test]
    public async Task Translate_RootExpression_ReturnsNoWhere()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        var result = ExpressionTranslator.Translate(root);

        await Assert.That(result.WhereClause).IsNull();
        await Assert.That(result.Parameters).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Translate_EqualityFilter_GeneratesCorrectSql()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        Expression<Func<TestEntity, bool>> predicate = x => x.Name == "foo";
        var filter = new FilterExpression(root, predicate);

        var result = ExpressionTranslator.Translate(filter);

        await Assert.That(result.WhereClause).IsEqualTo("data->>'Name' = @p0");
        await Assert.That(result.Parameters).HasCount().EqualTo(1);
        await Assert.That(result.Parameters[0].Name).IsEqualTo("@p0");
        await Assert.That(result.Parameters[0].Value).IsEqualTo("foo");
    }

    [Test]
    public async Task Translate_NotEqualFilter_GeneratesCorrectSql()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        Expression<Func<TestEntity, bool>> predicate = x => x.Name != "bar";
        var filter = new FilterExpression(root, predicate);

        var result = ExpressionTranslator.Translate(filter);

        await Assert.That(result.WhereClause).IsEqualTo("data->>'Name' != @p0");
    }

    [Test]
    public async Task Translate_GreaterThanFilter_GeneratesCorrectSql()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        Expression<Func<TestEntity, bool>> predicate = x => x.Age > 30;
        var filter = new FilterExpression(root, predicate);

        var result = ExpressionTranslator.Translate(filter);

        await Assert.That(result.WhereClause).IsEqualTo("(data->>'Age')::numeric > @p0");
        await Assert.That(result.Parameters[0].Value).IsEqualTo(30);
    }

    [Test]
    public async Task Translate_ChainedFilters_CombinesWithAnd()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        Expression<Func<TestEntity, bool>> pred1 = x => x.Name == "foo";
        Expression<Func<TestEntity, bool>> pred2 = x => x.Age > 25;

        var filter1 = new FilterExpression(root, pred1);
        var filter2 = new FilterExpression(filter1, pred2);

        var result = ExpressionTranslator.Translate(filter2);

        await Assert.That(result.WhereClause).IsEqualTo("data->>'Name' = @p0 AND (data->>'Age')::numeric > @p1");
        await Assert.That(result.Parameters).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Translate_AndExpression_GeneratesAndClause()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        Expression<Func<TestEntity, bool>> predicate = x => x.Name == "foo" && x.Age > 25;
        var filter = new FilterExpression(root, predicate);

        var result = ExpressionTranslator.Translate(filter);

        await Assert.That(result.WhereClause).Contains("AND");
        await Assert.That(result.Parameters).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Translate_OrExpression_GeneratesOrClause()
    {
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        Expression<Func<TestEntity, bool>> predicate = x => x.Name == "foo" || x.Name == "bar";
        var filter = new FilterExpression(root, predicate);

        var result = ExpressionTranslator.Translate(filter);

        await Assert.That(result.WhereClause).Contains("OR");
        await Assert.That(result.Parameters).HasCount().EqualTo(2);
    }

    [Test]
    public async Task Translate_UnsupportedExpression_ThrowsNotSupportedException()
    {
        // TraverseLinkExpression is not supported for SQL translation
        var root = new RootExpression(typeof(TestEntity), nameof(TestEntity));
        var traverse = new TraverseLinkExpression(root, "link", typeof(TestEntity));

        await Assert.That(() => ExpressionTranslator.Translate(traverse))
            .Throws<NotSupportedException>();
    }

    public sealed class TestEntity
    {
        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }
    }
}
