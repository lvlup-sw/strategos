// -----------------------------------------------------------------------
// <copyright file="DR8EdgeCasesIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Identity.Abstractions.Tests.Fakes;

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Consolidated DR-8 traceability anchor. The DR-8 "Error handling and edge
/// cases" table in the design document has six rows; each is mapped below
/// to the test (or off-repo enforcement surface) that satisfies it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Strategos-side enforcement (tests live in this class):</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Row 1</b> — accessor read outside a handler returns null (no throw).
///     Test: <see cref="DR8_AccessorReadOutsideHandler_ReturnsNull_NoThrow"/>.
///   </description></item>
///   <item><description>
///     <b>Row 3</b> — identity records reject null/empty values at construction.
///     Test: <see cref="DR8_IdentityRecordConstructedWithNullValue_ThrowsArgumentException"/>.
///     (Additional null/empty/whitespace/non-ASCII coverage in
///     <see cref="WorkflowIdentityTests"/> and <see cref="AgentIdentityTests"/>.)
///   </description></item>
///   <item><description>
///     <b>Row 6</b> — non-ASCII / control-character header values are rejected by
///     the record constructor so the transport layer never sees an un-encodable byte.
///     Test: <see cref="DR8_HeaderValueWithNonAsciiCharacter_RecordConstructorRejects"/>.
///   </description></item>
/// </list>
/// <para>
/// <b>Basileus-side / Wolverine-side enforcement (no Strategos-side test):</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Row 2</b> — middleware generates a new identity when none is incoming.
///     This is the basileus <c>StrategosHeaderMiddleware</c> contract; the
///     reference implementation lives in lvlup-sw/basileus PR #184. See
///     <c>docs/coordination/2026-05-16-basileus-handoff.md</c> for the cross-repo
///     handoff and middleware-shape contract.
///   </description></item>
///   <item><description>
///     <b>Row 4</b> — provider returning null from <c>DeriveStepIdentity</c> is a
///     basileus contract surface. The Strategos-side enforcement is the stub
///     provider's own throw-on-null behavior verified in
///     <see cref="IAgentIdentityProviderContractTests"/> (T6).
///   </description></item>
///   <item><description>
///     <b>Row 5</b> — outgoing messages carry the workflow-identity header via
///     Wolverine's native envelope mechanism plus the
///     <c>PropagateIncomingHeaderToOutgoing</c> policy. Tested by Wolverine's
///     own test suite (out of scope for Strategos); documented in CHANGELOG
///     under the v2.7.0-preview.1 Migration subsection.
///   </description></item>
/// </list>
/// </remarks>
[Property("Category", "Integration")]
public class DR8EdgeCasesIntegrationTests
{
    /// <summary>
    /// DR-8 Row 1: accessor read outside a Wolverine handler context returns
    /// null on both properties and does NOT throw.
    /// </summary>
    [Test]
    public async Task DR8_AccessorReadOutsideHandler_ReturnsNull_NoThrow()
    {
        var accessor = new FakeAgentIdentityAccessor(envelopeHeaders: null);

        await Assert.That(accessor.CurrentWorkflow).IsNull();
        await Assert.That(accessor.CurrentAgent).IsNull();
    }

    /// <summary>
    /// DR-8 Row 3: both <see cref="WorkflowIdentity"/> and <see cref="AgentIdentity"/>
    /// reject null/empty values at construction.
    /// </summary>
    [Test]
    public async Task DR8_IdentityRecordConstructedWithNullValue_ThrowsArgumentException()
    {
        await Assert.That(() => new WorkflowIdentity(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => new AgentIdentity(null!)).Throws<ArgumentNullException>();

        await Assert.That(() => new WorkflowIdentity(string.Empty)).Throws<ArgumentException>();
        await Assert.That(() => new AgentIdentity(string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>
    /// DR-8 Row 6: non-ASCII header values are rejected by both record
    /// constructors so the transport layer never sees an un-encodable byte.
    /// </summary>
    [Test]
    public async Task DR8_HeaderValueWithNonAsciiCharacter_RecordConstructorRejects()
    {
        // U+00E9 — printable in UTF-8, but outside the 0x20..0x7E ASCII subset.
        await Assert.That(() => new WorkflowIdentity("workflow-é")).Throws<ArgumentException>();
        await Assert.That(() => new AgentIdentity("agent-é")).Throws<ArgumentException>();

        // Control character — outside 0x20..0x7E.
        await Assert.That(() => new WorkflowIdentity("workflow\n1")).Throws<ArgumentException>();
        await Assert.That(() => new AgentIdentity("agent\t1")).Throws<ArgumentException>();
    }
}
