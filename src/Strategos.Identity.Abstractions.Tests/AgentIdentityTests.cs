// -----------------------------------------------------------------------
// <copyright file="AgentIdentityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Behavior tests for <see cref="AgentIdentity"/>. Mirrors WorkflowIdentityTests
/// since both records share the same validation contract.
/// </summary>
[Property("Category", "Unit")]
public class AgentIdentityTests
{
    /// <summary>
    /// Verifies that the record stores the supplied value byte-for-byte.
    /// </summary>
    [Test]
    public async Task AgentIdentity_ConstructedWithValue_StoresValueExactly()
    {
        var identity = new AgentIdentity("spiffe://td/workflow/abc/step/Drafting");

        await Assert.That(identity.Value).IsEqualTo("spiffe://td/workflow/abc/step/Drafting");
    }

    /// <summary>
    /// A null value violates DR-2 boundary validation.
    /// </summary>
    [Test]
    public async Task AgentIdentity_NullValue_ThrowsArgumentNullException()
    {
        await Assert.That(() => new AgentIdentity(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// An empty value violates DR-2 boundary validation.
    /// </summary>
    [Test]
    public async Task AgentIdentity_EmptyValue_ThrowsArgumentException()
    {
        await Assert.That(() => new AgentIdentity(string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>
    /// Non-ASCII characters violate the header-safe ASCII subset enforced by DR-8 row 6.
    /// </summary>
    [Test]
    public async Task AgentIdentity_NonAsciiValue_ThrowsArgumentException()
    {
        await Assert.That(() => new AgentIdentity("agent-é")).Throws<ArgumentException>();
    }

    /// <summary>
    /// INV-6: sealed-by-default invariant must hold via reflection.
    /// </summary>
    [Test]
    public async Task AgentIdentity_IsSealedRecord_ViaReflection()
    {
        var t = typeof(AgentIdentity);

        await Assert.That(t.IsSealed).IsTrue();
    }
}
