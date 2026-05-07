using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class PreconditionPostconditionDescriptorTests
{
    [Test]
    public async Task PreconditionKind_HasExpectedValues()
    {
        await Assert.That(Enum.GetValues<PreconditionKind>()).Count().IsEqualTo(3);
        await Assert.That(PreconditionKind.PropertyPredicate).IsEqualTo((PreconditionKind)0);
        await Assert.That(PreconditionKind.LinkExists).IsEqualTo((PreconditionKind)1);
        await Assert.That(PreconditionKind.Custom).IsEqualTo((PreconditionKind)2);
    }

    [Test]
    public async Task PostconditionKind_HasExpectedValues()
    {
        await Assert.That(Enum.GetValues<PostconditionKind>()).Count().IsEqualTo(3);
        await Assert.That(PostconditionKind.ModifiesProperty).IsEqualTo((PostconditionKind)0);
        await Assert.That(PostconditionKind.CreatesLink).IsEqualTo((PostconditionKind)1);
        await Assert.That(PostconditionKind.EmitsEvent).IsEqualTo((PostconditionKind)2);
    }

    [Test]
    public async Task ActionPrecondition_PropertyPredicate_Record()
    {
        var precondition = new ActionPrecondition
        {
            Expression = "Status == Active",
            Description = "Status must be Active",
            Kind = PreconditionKind.PropertyPredicate,
        };

        await Assert.That(precondition.Expression).IsEqualTo("Status == Active");
        await Assert.That(precondition.Description).IsEqualTo("Status must be Active");
        await Assert.That(precondition.Kind).IsEqualTo(PreconditionKind.PropertyPredicate);
        await Assert.That(precondition.LinkName).IsNull();
    }

    [Test]
    public async Task ActionPrecondition_LinkExists_Record()
    {
        var precondition = new ActionPrecondition
        {
            Expression = "Link 'Strategy' exists",
            Description = "Requires link 'Strategy' to have at least one target",
            Kind = PreconditionKind.LinkExists,
            LinkName = "Strategy",
        };

        await Assert.That(precondition.Kind).IsEqualTo(PreconditionKind.LinkExists);
        await Assert.That(precondition.LinkName).IsEqualTo("Strategy");
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
        var descriptor = new ActionDescriptor("Test", "Test action");

        await Assert.That(descriptor.Preconditions.Count).IsEqualTo(0);
        await Assert.That(descriptor.Postconditions.Count).IsEqualTo(0);
    }
}
