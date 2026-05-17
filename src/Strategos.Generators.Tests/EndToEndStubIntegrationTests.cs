// -----------------------------------------------------------------------
// <copyright file="EndToEndStubIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;
using Strategos.Identity.Abstractions;

namespace Strategos.Generators.Tests;

/// <summary>
/// T11: end-to-end seam verification. Two paths are exercised:
/// </summary>
/// <list type="number">
///   <item><description>The generated saga (a) carries the <c>IPhaseAwareSaga</c> base list
///   entry, (b) emits the <c>CurrentPhaseName</c> property returning <c>Phase.ToString()</c>,
///   and (c) the generated assembly does not pull in any Basileus reference.</description></item>
///   <item><description>The middleware seam works end-to-end: a fake middleware uses the
///   stub provider to derive an agent identity, stamps the envelope headers, and the
///   accessor reads them back.</description></item>
/// </list>
[Property("Category", "Integration")]
public class EndToEndStubIntegrationTests
{
    /// <summary>
    /// Stub-provider-only path: the generator emits the IPhaseAwareSaga interface
    /// directly, the abstractions namespace is imported, the CurrentPhaseName
    /// property returns Phase.ToString() literally, and no generated tree references
    /// any Basileus.* type.
    /// </summary>
    [Test]
    public async Task GeneratedSaga_WithStubProvider_CompilesAndExposesPhaseName_WithoutBasileusReference()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // (a) IPhaseAwareSaga is in the base list.
        await Assert.That(sagaSource).Contains(": Saga, IPhaseAwareSaga");

        // (b) CurrentPhaseName returns Phase.ToString().
        await Assert.That(sagaSource).Contains("public string CurrentPhaseName => Phase.ToString();");

        // (c) The generated saga uses the abstractions namespace, NOT Basileus.
        await Assert.That(sagaSource).Contains("using Strategos.Identity.Abstractions;");

        // (c, negation) — no Basileus token anywhere in any generated tree.
        foreach (var tree in result.GeneratedTrees)
        {
            var src = tree.GetText().ToString();
            await Assert.That(src).DoesNotContain("Basileus");
        }
    }

    /// <summary>
    /// Middleware seam: a fake middleware that stamps headers from the stub
    /// provider produces an envelope the accessor can read back without any
    /// generated-saga runtime instance.
    /// </summary>
    [Test]
    public async Task IPhaseAwareSaga_FakeMiddlewareStampsEnvelopeHeaders_StubProviderDerivesAgent()
    {
        // Stand-in for IMessageContext.Envelope.Headers.
        var envelope = new Dictionary<string, string>();

        var provider = new StubAgentIdentityProvider();
        var workflow = new WorkflowIdentity("wf-001");

        // Simulate what the basileus StrategosHeaderMiddleware does:
        // 1. Stamp the workflow identity on the outgoing envelope.
        envelope[StrategosHeaders.WorkflowIdentity] = workflow.Value;

        // 2. Read the saga's current phase via IPhaseAwareSaga.
        var saga = new StubPhaseAwareSaga(currentPhaseName: "Drafting");
        var phase = saga.CurrentPhaseName;

        // 3. Derive the per-step agent identity from (workflow, phase).
        var agent = provider.DeriveStepIdentity(workflow, phase);

        // 4. Stamp the agent identity on the outgoing envelope.
        envelope[StrategosHeaders.AgentIdentity] = agent.Value;

        // Downstream handler reads the envelope through the accessor.
        var accessor = new FakeMiddlewareSeamAccessor(envelope);

        await Assert.That(accessor.CurrentWorkflow).IsNotNull();
        await Assert.That(accessor.CurrentWorkflow!.Value).IsEqualTo("wf-001");
        await Assert.That(accessor.CurrentAgent).IsNotNull();
        await Assert.That(accessor.CurrentAgent!.Value).IsEqualTo("wf-001#Drafting");
    }

    /// <summary>
    /// Test-local IPhaseAwareSaga stand-in. Mirrors the generator emit shape
    /// (<c>CurrentPhaseName =&gt; Phase.ToString()</c>) but bypasses the
    /// Phase enum so this test does not need a full workflow context.
    /// </summary>
    private sealed class StubPhaseAwareSaga : IPhaseAwareSaga
    {
        public StubPhaseAwareSaga(string currentPhaseName)
        {
            this.CurrentPhaseName = currentPhaseName;
        }

        public string CurrentPhaseName { get; }
    }

    /// <summary>
    /// Local stub of an envelope-header-backed <see cref="IAgentIdentityAccessor"/>.
    /// Production reads via Wolverine <c>IMessageContext.Envelope.Headers</c>;
    /// this test uses an in-memory dictionary.
    /// </summary>
    private sealed class FakeMiddlewareSeamAccessor : IAgentIdentityAccessor
    {
        private readonly IDictionary<string, string> envelope;

        public FakeMiddlewareSeamAccessor(IDictionary<string, string> envelope)
        {
            this.envelope = envelope;
        }

        public WorkflowIdentity? CurrentWorkflow =>
            this.envelope.TryGetValue(StrategosHeaders.WorkflowIdentity, out var v) ? new WorkflowIdentity(v) : null;

        public AgentIdentity? CurrentAgent =>
            this.envelope.TryGetValue(StrategosHeaders.AgentIdentity, out var v) ? new AgentIdentity(v) : null;
    }

    /// <summary>
    /// Generators-tests-local copy of the provider stub kept inline so the
    /// production abstractions package stays minimal.
    /// </summary>
    private sealed class StubAgentIdentityProvider : IAgentIdentityProvider
    {
        public AgentIdentity DeriveStepIdentity(WorkflowIdentity workflow, string phaseName)
        {
            if (workflow is null)
            {
                throw new ArgumentNullException(nameof(workflow));
            }

            if (string.IsNullOrWhiteSpace(phaseName))
            {
                throw new ArgumentException("Phase name must be non-empty.", nameof(phaseName));
            }

            return new AgentIdentity($"{workflow.Value}#{phaseName}");
        }

        public WorkflowIdentity ParseWorkflowHeader(string headerValue)
            => new(headerValue ?? throw new ArgumentNullException(nameof(headerValue)));
    }
}
