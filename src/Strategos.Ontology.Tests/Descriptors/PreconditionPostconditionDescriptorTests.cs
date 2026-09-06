using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class PreconditionPostconditionDescriptorTests
{
    [Test]
    public async Task ActionPrecondition_PropertyPredicate_Record()
    {
        var predicate = ActionPredicate.Property(
            new PredicatePropertyReference("Status", PredicateScalarKind.String),
            PredicateComparisonOperator.Equal,
            PredicateLiteral.String("Active"));
        var precondition = new ActionPrecondition(predicate, "Status must be Active");

        await Assert.That(precondition.Expression).IsEqualTo("Status == \"Active\"");
        await Assert.That(precondition.Description).IsEqualTo("Status must be Active");
        await Assert.That(precondition.Predicate).IsEqualTo(predicate);
        await Assert.That(precondition.IsOpaque).IsFalse();
    }

    [Test]
    public async Task PostconditionKind_HasExpectedValues()
    {
        await Assert.That(Enum.GetValues<PostconditionKind>()).HasCount().EqualTo(3);
        await Assert.That(PostconditionKind.ModifiesProperty).IsEqualTo((PostconditionKind)0);
        await Assert.That(PostconditionKind.CreatesLink).IsEqualTo((PostconditionKind)1);
        await Assert.That(PostconditionKind.EmitsEvent).IsEqualTo((PostconditionKind)2);
    }

    [Test]
    public async Task ActionPrecondition_LinkExists_Record()
    {
        var predicate = ActionPredicate.LinkExists("Strategy");
        var precondition = new ActionPrecondition(
            predicate,
            "Requires link 'Strategy' to have at least one target");

        await Assert.That(precondition.Predicate).IsTypeOf<LinkExistsPredicate>();
        await Assert.That(((LinkExistsPredicate)precondition.Predicate).LinkName).IsEqualTo("Strategy");
    }

    [Test]
    public async Task ActionPostcondition_ModifiesProperty_Record()
    {
        var postcondition = new ActionPostcondition
        {
            Kind = PostconditionKind.ModifiesProperty,
            PropertyName = "Quantity",
        };

        await Assert.That(postcondition.Kind).IsEqualTo(PostconditionKind.ModifiesProperty);
        await Assert.That(postcondition.PropertyName).IsEqualTo("Quantity");
        await Assert.That(postcondition.LinkName).IsNull();
        await Assert.That(postcondition.EventTypeName).IsNull();
    }

    [Test]
    public async Task ActionPostcondition_CreatesLink_Record()
    {
        var postcondition = new ActionPostcondition
        {
            Kind = PostconditionKind.CreatesLink,
            LinkName = "Orders",
        };

        await Assert.That(postcondition.Kind).IsEqualTo(PostconditionKind.CreatesLink);
        await Assert.That(postcondition.LinkName).IsEqualTo("Orders");
    }

    [Test]
    public async Task ActionPostcondition_EmitsEvent_Record()
    {
        var postcondition = new ActionPostcondition
        {
            Kind = PostconditionKind.EmitsEvent,
            EventTypeName = "TradeExecuted",
        };

        await Assert.That(postcondition.Kind).IsEqualTo(PostconditionKind.EmitsEvent);
        await Assert.That(postcondition.EventTypeName).IsEqualTo("TradeExecuted");
    }

    [Test]
    public async Task ActionDescriptor_DefaultPreconditionsAndPostconditions_AreEmpty()
    {
        var descriptor = new ActionDescriptor(
            new ActionSubject("Test", "Thing"),
            "Test",
            "Test action");

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(0);
        await Assert.That(descriptor.Ensures.Count).IsEqualTo(0);
        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(0);
    }
}
