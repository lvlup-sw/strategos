// =============================================================================
// <copyright file="AgentStepBaseIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Tests.Integration;

/// <summary>
/// Real-chain integration tests (DR-9). Acceptance test for the full MEAI 10.5
/// pipeline: structured output, AIFunction tool invocation, MCP tool resolution,
/// and middleware ordering through the full ChatClientBuilder.
/// </summary>
[Property("Category", "Integration")]
public sealed class AgentStepBaseIntegrationTests
{
    [Test]
    public async Task MeaiPipeline_StructuredOutputWithToolAndMcp_RoundTripsThroughChain()
    {
        // Acceptance test for the full MEAI 10.5 pipeline. Stays RED until
        // T-019 + T-020 land. Pinned here as the DR-9 anchor so the TDD
        // discipline is enforced from the first task.
        //
        // The dependencies below do not exist yet — this file must FAIL TO COMPILE.

        // Arrange — references the types being built across tasks:
        var builder = new AgentStepBuilder<TestState, TestResult>();           // T-012..T-016
        var mcpSource = new InProcessMcpToolSource();                            // T-005, T-020
        _ = AgentDiagnostics.AGAG002;                                            // T-002

        // The full test body will be filled in T-019 and T-020.
        await Assert.That(builder).IsNotNull();
        await Assert.That(mcpSource).IsNotNull();
    }

    private sealed record TestState : Strategos.Abstractions.IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }

    private sealed record TestResult(string Value);

    private sealed class InProcessMcpToolSource : IMcpToolSource
    {
        public Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AIFunction>>(Array.Empty<AIFunction>());
    }
}
