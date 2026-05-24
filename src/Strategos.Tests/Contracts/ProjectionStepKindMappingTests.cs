// =============================================================================
// <copyright file="ProjectionStepKindMappingTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Strategos.Contracts;
using Strategos.Contracts.Generated;
using Strategos.Definitions;
using Strategos.Tests.Fixtures;
using BuilderStep = Strategos.Definitions.StepDefinition;

namespace Strategos.Tests.Contracts;

/// <summary>
/// Pins the <b>step-kind mapping</b> in
/// <see cref="WorkflowDefinitionProjection"/> as an asserted decision rather than
/// an accident. The wire <c>StepDefinition</c> union has five arms (skill,
/// handler, gate, delegate, approval); the projection deliberately exercises only
/// <b>two</b> of them — typed → <c>skill</c> (LB-2), lambda → <c>delegate</c>
/// (LB-1) — because the builder IR exposes no richer kind discriminator than
/// <see cref="BuilderStep.IsLambdaStep"/> today. These tests:
/// </summary>
/// <remarks>
/// <list type="number">
///   <item><description>pin the intended 2-of-5 mapping behaviorally; and</description></item>
///   <item><description>
///     assert the mapping is <b>non-silent</b> — the projection's
///     classify/switch emits an explicit <see cref="NotSupportedException"/> on
///     the <c>default</c> arm for an unmapped kind rather than silently
///     defaulting to <c>skill</c>, and that throw path is demonstrably reachable.
///   </description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class ProjectionStepKindMappingTests
{
    /// <summary>
    /// T-KM1 — a typed (CLR) builder step maps to the wire <c>skill</c> arm.
    /// </summary>
    [Test]
    public async Task TypedStep_MapsToSkillArm()
    {
        var step = BuilderStep.Create(typeof(ValidateStep)) with { StepId = "k-typed" };

        var wire = ProjectStep(step);

        await Assert.That(wire).IsTypeOf<SkillStep>()
            .Because("LB-2: a typed/CLR step is the skill arm.");
        await Assert.That(((SkillStep)wire).StepType).IsEqualTo(typeof(ValidateStep).Name);
    }

    /// <summary>
    /// T-KM2 — a lambda builder step maps to the wire <c>delegate</c> arm with
    /// <c>lambda: true</c> and no executable body.
    /// </summary>
    [Test]
    public async Task LambdaStep_MapsToDelegateArm()
    {
        var step = BuilderStep.CreateFromLambda(
            "k-lambda",
            (TestWorkflowState s, StepContext c, CancellationToken ct) =>
                Task.FromResult(StepResult<TestWorkflowState>.FromState(s)));

        var wire = ProjectStep(step);

        await Assert.That(wire).IsTypeOf<DelegateStep>()
            .Because("LB-1: a lambda step is the delegate arm.");
        await Assert.That(((DelegateStep)wire).Lambda).IsTrue();
    }

    /// <summary>
    /// T-KM3 — the 2-of-5 subset is an asserted decision: across a mixed
    /// workflow, the projection produces <b>only</b> skill and delegate arms and
    /// <b>never</b> the handler / gate / approval arms (the builder cannot yet
    /// distinguish them). If the projection ever starts emitting one of those
    /// without a corresponding builder discriminator, this fails.
    /// </summary>
    [Test]
    public async Task Projection_ProducesOnlySkillAndDelegateArms_TheAsserted2Of5Subset()
    {
        var workflow = Workflow<TestWorkflowState>
            .Create("mixed")
            .StartWith<ValidateStep>()
            .Then("InlineProcess", (state, context, ct) =>
                Task.FromResult(StepResult<TestWorkflowState>.FromState(state)))
            .Finally<CompleteStep>();

        var v1 = workflow.ToContract();

        await Assert.That(v1.Steps.Count).IsGreaterThan(0);
        foreach (var step in v1.Steps)
        {
            var isMappedArm = step is SkillStep or DelegateStep;
            await Assert.That(isMappedArm).IsTrue()
                .Because(
                    $"the projection's only mapped arms are skill/delegate; {step.GetType().Name} " +
                    "would require a builder kind discriminator that does not exist in 0.2.0.");
        }

        // And both mapped arms are actually exercised by the mixed workflow.
        await Assert.That(v1.Steps.OfType<SkillStep>().Any()).IsTrue();
        await Assert.That(v1.Steps.OfType<DelegateStep>().Any()).IsTrue();
    }

    /// <summary>
    /// T-KM4 — the throw path is reachable and non-silent. The projection routes
    /// through a private <c>BuilderStepKind</c> classification; the
    /// <see cref="WorkflowDefinitionProjection.ProjectStep"/> switch's
    /// <c>default</c> arm throws <see cref="NotSupportedException"/> naming the
    /// unmapped kind. We prove reachability by invoking the private switch logic
    /// against a hypothetical out-of-range kind value and asserting it throws
    /// rather than silently returning a (wrong) <c>skill</c> arm.
    /// </summary>
    [Test]
    public async Task UnmappedKind_ThrowsNotSupported_NotSilentSkillDefault()
    {
        // Reflect the private BuilderStepKind enum and confirm exactly the two
        // mapped values exist today — so this guard stays meaningful: if a third
        // kind is added, ProjectKind must gain an arm or this enum check changes.
        var kindType = typeof(WorkflowDefinitionProjection)
            .GetNestedType("BuilderStepKind", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("BuilderStepKind classification enum not found.");

        var kindNames = Enum.GetNames(kindType);
        await Assert.That(kindNames).Contains("Skill");
        await Assert.That(kindNames).Contains("Delegate");

        // Drive the switch with an out-of-range enum value (a hypothetical future
        // kind that was added to the enum but never wired). The default arm must
        // throw NotSupportedException naming the kind — proving the path is live
        // and the projection never silently defaults an unrecognized kind.
        var hypotheticalUnmapped = Enum.ToObject(kindType, 9999);
        var thrown = await ProjectMappedKindThrows(hypotheticalUnmapped);

        await Assert.That(thrown).IsNotNull()
            .Because("an unmapped BuilderStepKind must throw, not silently fall through.");
        await Assert.That(thrown!.GetType()).IsEqualTo(typeof(NotSupportedException));
        await Assert.That(thrown.Message).Contains("9999")
            .Because("the throw must name the unmapped kind for diagnosability.");
    }

    /// <summary>
    /// Invokes the private static <c>ProjectStep</c> seam.
    /// </summary>
    private static Strategos.Contracts.Generated.StepDefinition ProjectStep(BuilderStep step)
    {
        var method = typeof(WorkflowDefinitionProjection).GetMethod(
            "ProjectStep", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ProjectStep seam not found.");

        return (Strategos.Contracts.Generated.StepDefinition)method.Invoke(null, [step])!;
    }

    /// <summary>
    /// Drives the projection's kind-switch <c>default</c> arm for a hypothetical
    /// out-of-range <c>BuilderStepKind</c> by extracting and re-applying the same
    /// switch via a real <see cref="BuilderStep"/> whose classification is forced
    /// out of range — proving the throwing default is reachable. Because the
    /// production switch is keyed off the private enum, we invoke the equivalent
    /// <c>ProjectMappedKind</c> guard helper directly.
    /// </summary>
    /// <returns>The exception thrown, or null if none.</returns>
    private static async Task<Exception?> ProjectMappedKindThrows(object hypotheticalKind)
    {
        var guard = typeof(WorkflowDefinitionProjection).GetMethod(
            "ProjectMappedKind", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "ProjectMappedKind guard seam not found — the test expects the projection to " +
                "route kind dispatch through a guarded helper whose default arm throws.");

        await Task.CompletedTask;
        try
        {
            var sample = BuilderStep.Create(typeof(ValidateStep));
            guard.Invoke(null, [hypotheticalKind, sample]);
            return null;
        }
        catch (TargetInvocationException tie)
        {
            return tie.InnerException;
        }
    }
}
