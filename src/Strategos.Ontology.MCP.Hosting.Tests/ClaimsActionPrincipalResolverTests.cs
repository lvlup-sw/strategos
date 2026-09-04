using System.Security.Claims;

using Strategos.Ontology.MCP.Hosting;

namespace Strategos.Ontology.MCP.Hosting.Tests;

public sealed class ClaimsActionPrincipalResolverTests
{
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

    private static ClaimsPrincipal CreateCaller(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));
}
