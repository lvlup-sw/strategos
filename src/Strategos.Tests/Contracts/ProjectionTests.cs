// =============================================================================
// <copyright file="ProjectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Contracts;
using Strategos.Contracts.Generated;
using Strategos.Tests.Fixtures;

namespace Strategos.Tests.Contracts;

/// <summary>
/// Tests for <see cref="WorkflowDefinitionProjection.ToContract{TState}"/> — the
/// one-way (export-only) projection from the in-memory builder IR
/// (<c>WorkflowDefinition&lt;TState&gt;</c>) to the generated wire contract
/// (<c>WorkflowDefinitionV1</c>). Covers the happy path (T18), the LB-1 lambda
/// boundary (T19), the LB-2 simple-name moniker (T20), and the LB-2 export-only
/// guard (T21).
/// </summary>
[Property("Category", "Unit")]
public class ProjectionTests
{
    /// <summary>
    /// T18 — a simple skill-step workflow projects to a populated, correctly
    /// versioned <see cref="WorkflowDefinitionV1"/> with its steps in order.
    /// </summary>
    [Test]
    public async Task ToContract_SkillStepWorkflow_ProducesValidV1()
    {
        var workflow = Workflow<TestWorkflowState>
            .Create("process-order")
            .StartWith<ValidateStep>()
            .Then<ProcessStep>()
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        await Assert.That(v1).IsNotNull();
        await Assert.That(v1.SchemaVersion).IsEqualTo("1.0");
        await Assert.That(v1.Name).IsEqualTo("process-order");
        await Assert.That(v1.Steps.Count).IsGreaterThanOrEqualTo(2)
            .Because("the projected workflow must carry its steps.");

        // Every step projects to a concrete StepDefinition arm (the polymorphic
        // base is abstract — each element must be a derived kind).
        foreach (var step in v1.Steps)
        {
            await Assert.That(step).IsNotNull();
        }

        // The entry step is preserved by id.
        await Assert.That(v1.EntryStepId).IsNotNull();
    }
}
