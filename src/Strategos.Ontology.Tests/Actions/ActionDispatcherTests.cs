using Strategos.Ontology.Actions;

namespace Strategos.Ontology.Tests.Actions;

public class ActionDispatcherTests
{
    private static readonly ActionPrincipal Principal = new("User", "user-1");

    [Test]
    public async Task ActionContext_Create_HasDomainObjectTypeAndAction()
    {
        // Arrange & Act
        var context = new ActionContext(Principal, "CRM", "Contact", "c-1", "SendEmail");

        // Assert
        await Assert.That(context.Domain).IsEqualTo("CRM");
        await Assert.That(context.ObjectType).IsEqualTo("Contact");
        await Assert.That(context.ObjectId).IsEqualTo("c-1");
        await Assert.That(context.ActionName).IsEqualTo("SendEmail");
        await Assert.That(context.Principal).IsSameReferenceAs(Principal);
    }

    [Test]
    public async Task ActionResult_Success_IsSuccessTrue()
    {
        // Arrange & Act
        var result = new ActionResult(true, Result: "done");

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Result).IsEqualTo("done");
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    public async Task ActionResult_Failure_IsSuccessFalseWithError()
    {
        // Arrange & Act
        var result = new ActionResult(false, Error: "Something went wrong");

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Result).IsNull();
        await Assert.That(result.Error).IsEqualTo("Something went wrong");
    }

    [Test]
    public async Task IActionDispatcher_DispatchAsync_MethodSignatureExists()
    {
        // Arrange
        var dispatcher = Substitute.For<IActionDispatcher>();
        var context = new ActionContext(Principal, "CRM", "Contact", "c-1", "SendEmail");
        var request = new { To = "test@example.com" };
        dispatcher.DispatchAsync(context, request, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(true, Result: "sent")));

        // Act
        var result = await dispatcher.DispatchAsync(context, request, CancellationToken.None);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task ActionContext_Options_DefaultsToNull()
    {
        // Arrange & Act
        var context = new ActionContext(Principal, "CRM", "Contact", "c-1", "SendEmail");

        // Assert
        await Assert.That(context.Options).IsNull();
    }

    [Test]
    public async Task ActionContext_Options_CanEnforcePreconditions()
    {
        // Arrange & Act
        var options = new ActionDispatchOptions { EnforcePreconditions = true };
        var context = new ActionContext(Principal, "CRM", "Contact", "c-1", "SendEmail", options);

        // Assert
        await Assert.That(context.Options).IsNotNull();
        await Assert.That(context.Options!.EnforcePreconditions).IsTrue();
    }

    [Test]
    public async Task ActionContext_MissingPrincipal_Throws()
    {
        await Assert.That(() => new ActionContext(null!, "CRM", "Contact", "c-1", "SendEmail"))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ActionDispatchOptions_Default_DoesNotEnforcePreconditions()
    {
        // Arrange & Act
        var options = ActionDispatchOptions.Default;

        // Assert
        await Assert.That(options.EnforcePreconditions).IsFalse();
    }

    [Test]
    public async Task ActionDispatchOptions_WithInit_SetsEnforcePreconditions()
    {
        // Arrange & Act
        var options = new ActionDispatchOptions { EnforcePreconditions = true };

        // Assert
        await Assert.That(options.EnforcePreconditions).IsTrue();
    }
}
