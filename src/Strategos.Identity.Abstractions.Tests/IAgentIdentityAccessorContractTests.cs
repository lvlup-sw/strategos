// -----------------------------------------------------------------------
// <copyright file="IAgentIdentityAccessorContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Identity.Abstractions.Tests.Fakes;

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Contract tests for <see cref="IAgentIdentityAccessor"/> exercised via
/// <see cref="FakeAgentIdentityAccessor"/>. Anchors DR-5 and DR-8 row 1.
/// </summary>
[Property("Category", "Unit")]
public class IAgentIdentityAccessorContractTests
{
    /// <summary>
    /// DR-5: outside a Wolverine handler context, CurrentWorkflow returns null.
    /// </summary>
    [Test]
    public async Task FakeAgentIdentityAccessor_NoEnvelopeContext_CurrentWorkflowReturnsNull()
    {
        var sut = new FakeAgentIdentityAccessor(envelopeHeaders: null);

        await Assert.That(sut.CurrentWorkflow).IsNull();
    }

    /// <summary>
    /// DR-5: outside a Wolverine handler context, CurrentAgent returns null.
    /// </summary>
    [Test]
    public async Task FakeAgentIdentityAccessor_NoEnvelopeContext_CurrentAgentReturnsNull()
    {
        var sut = new FakeAgentIdentityAccessor(envelopeHeaders: null);

        await Assert.That(sut.CurrentAgent).IsNull();
    }

    /// <summary>
    /// Both headers present: both properties return populated records.
    /// </summary>
    [Test]
    public async Task FakeAgentIdentityAccessor_BothHeadersPresent_ReturnsParsedRecords()
    {
        var headers = new Dictionary<string, string>
        {
            [StrategosHeaders.WorkflowIdentity] = "wf-001",
            [StrategosHeaders.AgentIdentity] = "wf-001#Drafting",
        };
        var sut = new FakeAgentIdentityAccessor(headers);

        await Assert.That(sut.CurrentWorkflow).IsNotNull();
        await Assert.That(sut.CurrentWorkflow!.Value).IsEqualTo("wf-001");
        await Assert.That(sut.CurrentAgent).IsNotNull();
        await Assert.That(sut.CurrentAgent!.Value).IsEqualTo("wf-001#Drafting");
    }

    /// <summary>
    /// Workflow header only: CurrentAgent must remain null (each handler stamps
    /// its own agent identity).
    /// </summary>
    [Test]
    public async Task FakeAgentIdentityAccessor_OnlyWorkflowHeader_CurrentAgentReturnsNull()
    {
        var headers = new Dictionary<string, string>
        {
            [StrategosHeaders.WorkflowIdentity] = "wf-001",
        };
        var sut = new FakeAgentIdentityAccessor(headers);

        await Assert.That(sut.CurrentWorkflow).IsNotNull();
        await Assert.That(sut.CurrentAgent).IsNull();
    }

    /// <summary>
    /// DR-5: an invalid workflow header value must not throw — the accessor
    /// returns null so projections, debuggers, and inspection paths stay reliable.
    /// </summary>
    [Test]
    public async Task FakeAgentIdentityAccessor_WorkflowHeaderValueInvalid_ReturnsNullNoThrow()
    {
        var headers = new Dictionary<string, string>
        {
            // U+00E9 (é) violates the ASCII subset rule on construction.
            [StrategosHeaders.WorkflowIdentity] = "wf-é-001",
        };
        var sut = new FakeAgentIdentityAccessor(headers);

        await Assert.That(sut.CurrentWorkflow).IsNull();
    }

    /// <summary>
    /// DR-5 symmetric coverage: an invalid agent header value must not throw
    /// either — the accessor returns null on both properties uniformly. This
    /// mirrors <see cref="FakeAgentIdentityAccessor_WorkflowHeaderValueInvalid_ReturnsNullNoThrow"/>
    /// so the agent-side path has the same regression net as the workflow-side path.
    /// </summary>
    [Test]
    public async Task FakeAgentIdentityAccessor_AgentHeaderValueInvalid_ReturnsNullNoThrow()
    {
        var headers = new Dictionary<string, string>
        {
            [StrategosHeaders.WorkflowIdentity] = "wf-001",
            // Control character violates the printable-ASCII subset rule on construction.
            [StrategosHeaders.AgentIdentity] = "wf-001#bad\nphase",
        };
        var sut = new FakeAgentIdentityAccessor(headers);

        await Assert.That(sut.CurrentWorkflow).IsNotNull();
        await Assert.That(sut.CurrentAgent).IsNull();
    }
}
