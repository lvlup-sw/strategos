using Strategos.Ontology.Actions;

namespace Strategos.Ontology.Tests.Actions;

public sealed class ActionPrincipalTests
{
    [Test]
    public async Task Constructor_ValidTypeAndId_PreservesValues()
    {
        var principal = new ActionPrincipal("ServiceAccount", "svc-42");

        await Assert.That(principal.PrincipalType).IsEqualTo("ServiceAccount");
        await Assert.That(principal.PrincipalId).IsEqualTo("svc-42");
    }

    [Test]
    [Arguments(null, "id")]
    [Arguments("", "id")]
    [Arguments(" ", "id")]
    [Arguments("User", null)]
    [Arguments("User", "")]
    [Arguments("User", " ")]
    public async Task Constructor_MissingTypeOrId_Throws(string? principalType, string? principalId)
    {
        await Assert.That(() => new ActionPrincipal(principalType!, principalId!))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task ActionContext_RecordCloneCannotRemovePrincipal()
    {
        var context = new ActionContext(
            new ActionPrincipal("User", "user-1"),
            "Sales",
            "Order",
            "order-1",
            "Approve");

        await Assert.That(() => context with { Principal = null! })
            .Throws<ArgumentNullException>();
    }
}
