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

    /// <summary>
    /// T21 — LB-2: the projection is <b>export-only</b> in 0.2.0. Reflection
    /// asserts there is no public rehydration API (no <c>FromContract</c> /
    /// deserialize-to-<c>WorkflowDefinition&lt;TState&gt;</c> member) anywhere on
    /// the projection surface — that space is reserved for a future V-next, not
    /// shipped now.
    /// </summary>
    [Test]
    public async Task Projection_ExposesNoRehydrationApi_In_0_2_0()
    {
        var projectionType = typeof(WorkflowDefinitionProjection);

        var publicMethods = projectionType.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

        foreach (var method in publicMethods)
        {
            // No method named like a rehydration / deserialize entry point.
            var name = method.Name;
            var looksLikeRehydration =
                name.Contains("FromContract", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Deserialize", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Rehydrate", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("ToWorkflow", StringComparison.Ordinal)
                || name.StartsWith("ToBuilder", StringComparison.Ordinal);
            await Assert.That(looksLikeRehydration).IsFalse()
                .Because($"LB-2: {name} would be a rehydration API; none ships in 0.2.0.");

            // No public method returns a builder WorkflowDefinition<T> (the
            // export direction returns the wire type only).
            var returnType = method.ReturnType;
            var returnsBuilderIr = returnType.IsGenericType
                && returnType.GetGenericTypeDefinition() == typeof(WorkflowDefinition<>);
            await Assert.That(returnsBuilderIr).IsFalse()
                .Because($"LB-2: {name} must not reconstruct the builder IR (export-only).");
        }
    }

    /// <summary>
    /// DR-17 — a fork-path step configured via the new
    /// <c>IForkPathBuilder.Then(configure)</c> overload carries its retry,
    /// timeout, and compensation configuration through the export-only projection
    /// to the wire contract (<c>ForkPoints[i].Paths[j].Steps[k].Configuration</c>).
    /// This is the declarative lowering path for per-branch step config; the
    /// saga (Wolverine+Marten) lowering is a separate generator concern.
    /// </summary>
    [Test]
    public async Task ToContract_ConfiguredForkPathStep_CarriesRetryTimeoutCompensate()
    {
        var workflow = Workflow<TestWorkflowState>
            .Create("fork-config-workflow")
            .StartWith<ValidateStep>()
            .Fork(
                path => path.Then<ProcessStep>(step => step
                    .WithRetry(3, TimeSpan.FromSeconds(5))
                    .WithTimeout(TimeSpan.FromMinutes(2))
                    .Compensate<RefundStep>()),
                path => path.Then<NotifyStep>())
            .Join<CompleteStep>()
            .Finally<NotifyAdminStep>();

        var v1 = workflow.ToContract();

        await Assert.That(v1.ForkPoints.Count).IsEqualTo(1);

        // The first branch's configured step projects to a SkillStep carrying config.
        var configuredStep = v1.ForkPoints[0].Paths[0].Steps.OfType<SkillStep>().First();
        await Assert.That(configuredStep.Configuration).IsNotNull()
            .Because("DR-17: fork-path step config must survive the export.");
        await Assert.That(configuredStep.Configuration!.Retry).IsNotNull();
        await Assert.That(configuredStep.Configuration!.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(configuredStep.Configuration!.Timeout).IsNotNull()
            .Because("DR-17: WithTimeout must project to the wire duration string.");
        await Assert.That(configuredStep.Configuration!.Compensation).IsNotNull();
        await Assert.That(configuredStep.Configuration!.Compensation!.CompensationStepType)
            .IsEqualTo(typeof(RefundStep).Name);

        // The unconfigured branch step carries no configuration.
        var plainStep = v1.ForkPoints[0].Paths[1].Steps.OfType<SkillStep>().First();
        await Assert.That(plainStep.Configuration).IsNull()
            .Because("an unconfigured fork-path step must not synthesize config.");
    }

    /// <summary>
    /// T21 hygiene — the #50 swap correction: the builder IR in
    /// <c>src/Strategos/Definitions/</c> is the build/execution authority and is
    /// <b>retained</b>, never deleted in favour of the wire records. Asserts the
    /// load-bearing builder definitions still exist as compiled types.
    /// </summary>
    [Test]
    public async Task BuilderDefinitions_AreRetained_NotDeletedForWireContract()
    {
        // The generic builder root and the CLR-typed / lambda-bearing step
        // definition cannot be generated wire records — they must remain.
        await Assert.That(typeof(WorkflowDefinition<>).IsClass).IsTrue();
        await Assert.That(typeof(Strategos.Definitions.StepDefinition)).IsNotNull();

        // StepDefinition retains the live CLR handle and the delegate the wire
        // contract deliberately cannot carry (proving the two IRs are distinct).
        var stepTypeProp = typeof(Strategos.Definitions.StepDefinition)
            .GetProperty(nameof(Strategos.Definitions.StepDefinition.StepType));
        await Assert.That(stepTypeProp!.PropertyType).IsEqualTo(typeof(Type))
            .Because("the builder StepDefinition keeps StepType as a live System.Type.");
        var lambdaProp = typeof(Strategos.Definitions.StepDefinition)
            .GetProperty(nameof(Strategos.Definitions.StepDefinition.LambdaDelegate));
        await Assert.That(typeof(Delegate).IsAssignableFrom(
                Nullable.GetUnderlyingType(lambdaProp!.PropertyType) ?? lambdaProp.PropertyType))
            .IsTrue()
            .Because("the builder StepDefinition keeps the executable Delegate the wire IR drops.");
    }
}
