// -----------------------------------------------------------------------
// <copyright file="IAgentIdentityProviderContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Identity.Abstractions.Tests.Fakes;

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Contract tests for <see cref="IAgentIdentityProvider"/> exercised via
/// <see cref="StubAgentIdentityProvider"/>. Anchors DR-3 and DR-8 row 4.
/// </summary>
[Property("Category", "Unit")]
public class IAgentIdentityProviderContractTests
{
    /// <summary>
    /// Happy path: deriving from a valid workflow and phase produces an AgentIdentity
    /// containing both inputs.
    /// </summary>
    [Test]
    public async Task StubAgentIdentityProvider_DeriveStepIdentity_ReturnsAgentIdentity_ContainingWorkflowAndPhase()
    {
        var sut = new StubAgentIdentityProvider();
        var workflow = new WorkflowIdentity("wf-001");

        var agent = sut.DeriveStepIdentity(workflow, "Drafting");

        await Assert.That(agent.Value).Contains("wf-001");
        await Assert.That(agent.Value).Contains("Drafting");
    }

    /// <summary>
    /// DR-3 boundary: a null workflow throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task StubAgentIdentityProvider_DeriveStepIdentity_NullWorkflow_ThrowsArgumentNullException()
    {
        var sut = new StubAgentIdentityProvider();

        await Assert.That(() => sut.DeriveStepIdentity(null!, "Drafting")).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// DR-3 boundary: a null phaseName throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task StubAgentIdentityProvider_DeriveStepIdentity_NullPhaseName_ThrowsArgumentNullException()
    {
        var sut = new StubAgentIdentityProvider();
        var workflow = new WorkflowIdentity("wf-001");

        await Assert.That(() => sut.DeriveStepIdentity(workflow, null!)).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// DR-3 boundary: an empty phaseName throws ArgumentException.
    /// </summary>
    [Test]
    public async Task StubAgentIdentityProvider_DeriveStepIdentity_EmptyPhaseName_ThrowsArgumentException()
    {
        var sut = new StubAgentIdentityProvider();
        var workflow = new WorkflowIdentity("wf-001");

        await Assert.That(() => sut.DeriveStepIdentity(workflow, string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>
    /// DR-3 boundary: ParseWorkflowHeader rejects null inputs.
    /// </summary>
    [Test]
    public async Task StubAgentIdentityProvider_ParseWorkflowHeader_NullValue_ThrowsArgumentNullException()
    {
        var sut = new StubAgentIdentityProvider();

        await Assert.That(() => sut.ParseWorkflowHeader(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Roundtrip: a valid header value parses to a WorkflowIdentity with the same Value.
    /// </summary>
    [Test]
    public async Task StubAgentIdentityProvider_ParseWorkflowHeader_ValidValue_RoundTrips()
    {
        var sut = new StubAgentIdentityProvider();

        var parsed = sut.ParseWorkflowHeader("wf-001");

        await Assert.That(parsed.Value).IsEqualTo("wf-001");
    }
}
