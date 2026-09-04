using System.Security.Claims;

using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

/// <summary>
/// Verifies fail-closed claims binding for ontology action principals.
/// </summary>
public sealed class ClaimsActionPrincipalResolverTests
{
    /// <summary>
    /// Verifies that the required claims bind an authenticated caller.
    /// </summary>
    [Test]
    public async Task Resolve_AuthenticatedCallerWithRequiredClaims_BindsPrincipal()
    {
        var caller = CreateCaller(
            new Claim(ActionPrincipalClaimTypes.PrincipalType, "User"),
            new Claim(ClaimTypes.NameIdentifier, "user-1"));

        var principal = ClaimsActionPrincipalResolver.Instance.Resolve(caller);

        await Assert.That(principal).IsNotNull();
        await Assert.That(principal!.PrincipalType).IsEqualTo("User");
        await Assert.That(principal.PrincipalId).IsEqualTo("user-1");
    }

    /// <summary>
    /// Verifies that the standard subject claim can supply the principal ID.
    /// </summary>
    [Test]
    public async Task Resolve_SubjectClaim_BindsPrincipalId()
    {
        var caller = CreateCaller(
            new Claim(ActionPrincipalClaimTypes.PrincipalType, "Agent"),
            new Claim("sub", "agent-1"));

        var principal = ClaimsActionPrincipalResolver.Instance.Resolve(caller);

        await Assert.That(principal).IsNotNull();
        await Assert.That(principal!.PrincipalId).IsEqualTo("agent-1");
    }

    /// <summary>
    /// Verifies that unauthenticated or incomplete callers are refused.
    /// </summary>
    [Test]
    public async Task Resolve_UnauthenticatedOrIncompleteCaller_RefusesBinding()
    {
        var unauthenticated = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ActionPrincipalClaimTypes.PrincipalType, "User"), new Claim("sub", "user-1")]));
        var missingType = CreateCaller(new Claim("sub", "user-1"));
        var missingId = CreateCaller(new Claim(ActionPrincipalClaimTypes.PrincipalType, "User"));

        await Assert.That(ClaimsActionPrincipalResolver.Instance.Resolve(unauthenticated)).IsNull();
        await Assert.That(ClaimsActionPrincipalResolver.Instance.Resolve(missingType)).IsNull();
        await Assert.That(ClaimsActionPrincipalResolver.Instance.Resolve(missingId)).IsNull();
    }

    /// <summary>
    /// Verifies that claims from another unauthenticated identity cannot be
    /// borrowed by the authenticated primary identity.
    /// </summary>
    [Test]
    public async Task Resolve_ClaimsOnlyOnSecondaryUnauthenticatedIdentity_RefusesBinding()
    {
        var authenticated = new ClaimsIdentity(authenticationType: "test");
        var unauthenticated = new ClaimsIdentity(
        [
            new Claim(ActionPrincipalClaimTypes.PrincipalType, "User"),
            new Claim("sub", "user-1"),
        ]);
        var caller = new ClaimsPrincipal([authenticated, unauthenticated]);

        await Assert.That(ClaimsActionPrincipalResolver.Instance.Resolve(caller)).IsNull();
    }

    private static ClaimsPrincipal CreateCaller(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }
}
