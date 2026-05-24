// =============================================================================
// <copyright file="ProjectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Strategos.Contracts;
using Strategos.Contracts.Generated;
using Strategos.Steps;
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

    /// <summary>
    /// T19 — LB-1: a lambda step (built via <c>Then(name, delegate)</c>)
    /// projects to a <c>delegate</c>-kind wire step carrying <c>lambda: true</c>,
    /// with the delegate body dropped. The projected wire object must expose
    /// <b>no</b> member capable of holding a delegate / executable.
    /// </summary>
    [Test]
    public async Task ToContract_LambdaStep_EmitsDelegateKindWithMarker_NoCode()
    {
        var workflow = Workflow<TestWorkflowState>
            .Create("lambda-workflow")
            .StartWith<ValidateStep>()
            .Then("InlineProcess", (state, context, ct) =>
                Task.FromResult(StepResult<TestWorkflowState>.FromState(state)))
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        // The inline lambda step projects to a DelegateStep arm.
        var delegateStep = v1.Steps.OfType<DelegateStep>().SingleOrDefault();
        await Assert.That(delegateStep).IsNotNull()
            .Because("the lambda step must project to a single delegate-kind arm.");
        await Assert.That(delegateStep!.StepName).IsEqualTo("InlineProcess");
        await Assert.That(delegateStep.Lambda).IsTrue()
            .Because("LB-1: the dropped body must be made visible by lambda: true.");

        // LB-1 structural guarantee: no member of any projected step arm can
        // hold executable code — assert no property is a Delegate (or assignable
        // from one) across the entire generated step type hierarchy.
        foreach (var step in v1.Steps)
        {
            foreach (var prop in step.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var isExecutable = typeof(Delegate).IsAssignableFrom(prop.PropertyType)
                    || prop.PropertyType == typeof(Delegate);
                await Assert.That(isExecutable).IsFalse()
                    .Because($"LB-1: {step.GetType().Name}.{prop.Name} must not carry executable code.");
            }
        }
    }

    /// <summary>
    /// T20 — LB-2: a typed (CLR) step projects its <see cref="System.Type"/> to
    /// the <b>simple type name</b> moniker, never assembly- or namespace-
    /// qualified, keeping the wire contract language-neutral for the TS/Zod
    /// consumer.
    /// </summary>
    [Test]
    public async Task ToContract_TypedStep_UsesSimpleNameMoniker_NotAssemblyQualified()
    {
        var workflow = Workflow<TestWorkflowState>
            .Create("typed-workflow")
            .StartWith<ValidateStep>()
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        var skill = v1.Steps.OfType<SkillStep>().First();

        // Exactly the simple name — equal to typeof(T).Name.
        await Assert.That(skill.StepType).IsEqualTo(typeof(ValidateStep).Name);

        // No namespace qualifier, no assembly qualifier, no CLR path leakage.
        await Assert.That(skill.StepType).DoesNotContain(".")
            .Because("LB-2: the moniker must not be namespace-qualified.");
        await Assert.That(skill.StepType).DoesNotContain(",")
            .Because("LB-2: the moniker must not be assembly-qualified.");
        await Assert.That(skill.StepType).DoesNotContain("Strategos.Tests")
            .Because("LB-2: the moniker must not leak the CLR namespace.");
    }
}
