using Strategos.Ontology.Actions;

namespace Strategos.Ontology.Tests.Actions;

/// <summary>
/// Verifies the immutable ontology action-principal contract.
/// </summary>
public sealed class ActionPrincipalTests
{
    /// <summary>
    /// Verifies that valid principal coordinates are preserved.
    /// </summary>
    [Test]
    public async Task Constructor_ValidTypeAndId_PreservesValues()
    {
        var principal = new ActionPrincipal("ServiceAccount", "svc-42");

        await Assert.That(principal.PrincipalType).IsEqualTo("ServiceAccount");
        await Assert.That(principal.PrincipalId).IsEqualTo("svc-42");
    }

    /// <summary>
    /// Verifies that neither principal coordinate may be blank.
    /// </summary>
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

    /// <summary>
    /// Verifies that record cloning cannot remove a context principal.
    /// </summary>
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
