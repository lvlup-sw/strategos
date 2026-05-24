// =============================================================================
// <copyright file="ProjectionExhaustivenessTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Strategos.Contracts;
using Strategos.Definitions;
using Strategos.Tests.Fixtures;
using BuilderStep = Strategos.Definitions.StepDefinition;
using Wire = Strategos.Contracts.Generated;

namespace Strategos.Tests.Contracts;

/// <summary>
/// Mechanical guard against <b>builder-IR → <c>ToContract()</c> projection
/// drift</b> (issue #53 follow-up). The hand-authored
/// <see cref="WorkflowDefinitionProjection"/> seam silently ignores any builder
/// member it does not explicitly map; the #53 fixture round-trip cannot catch
/// this because fixtures are generated <em>through</em> the projection (a closed
/// loop — an ignored field is absent on both sides and still validates).
/// </summary>
/// <remarks>
/// <para>
/// This suite reflects over every public init/get-settable property of the
/// builder IR (<see cref="WorkflowDefinition{TState}"/> and
/// <see cref="BuilderStep"/>) and asserts each is <b>either</b>:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>(a) demonstrably projected</b> — proven behaviorally: a workflow/step
///     is built with the member set to a non-default value, run through
///     <see cref="WorkflowDefinitionProjection.ToContract{TState}"/>, serialized
///     via <see cref="ContractsJson"/>, and the value is asserted present in the
///     emitted JSON; <b>or</b>
///   </description></item>
///   <item><description>
///     <b>(b) on the explicit, justified allow-list</b> — a CLR-only member the
///     projection deliberately drops (LB-1 / LB-2 / structural-elsewhere), each
///     carrying a one-line justification.
///   </description></item>
/// </list>
/// <para>
/// A future dev who grows the builder IR is forced to either wire the new field
/// into the projection (and prove it surfaces) or add it to the allow-list with
/// a justification — silence is no longer an option.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public class ProjectionExhaustivenessTests
{
    // -------------------------------------------------------------------------
    // EXPLICIT, DOCUMENTED EXCLUSION ALLOW-LIST
    //
    // A builder member appears here iff it is INTENTIONALLY not carried on the
    // wire. Every entry MUST carry a one-line justification. A future builder
    // field that is neither projected (proven below) nor listed here fails the
    // exhaustiveness test — forcing a deliberate wire-or-exclude decision.
    // -------------------------------------------------------------------------

    /// <summary>
    /// <see cref="BuilderStep"/> members deliberately dropped from the wire form.
    /// Key = property name; value = justification.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> StepExclusions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // LB-2: the live System.Type handle is a CLR-only concept; it is
            // replaced on the wire by the simple-name `stepType` moniker. The
            // Type itself never serializes.
            [nameof(BuilderStep.StepType)] =
                "LB-2: CLR System.Type handle; replaced on the wire by the simple-name stepType moniker.",

            // LB-1: the executable delegate body is never serialized; its loss is
            // made visible by the `lambda: true` marker on the delegate arm.
            [nameof(BuilderStep.LambdaDelegate)] =
                "LB-1: executable Delegate body is never carried on the wire; loss is marked by lambda:true.",

            // Computed projection of StepType.Name — not independent state. The
            // wire `stepType` moniker is produced directly from StepType, so this
            // get-only derived member has no separate wire carrier.
            [nameof(BuilderStep.StepTypeName)] =
                "Computed (StepType.Name); not independent state — the stepType moniker already carries it.",

            // Structural-elsewhere: loop membership is expressed on the wire by
            // the enclosing LoopDefinition.bodySteps list, not duplicated as a
            // per-step boolean. The step arms (skill/delegate) carry no loop flag
            // and never have; membership is recoverable from the loop structure.
            [nameof(BuilderStep.IsLoopBodyStep)] =
                "Structural-elsewhere: loop membership is expressed by LoopDefinition.bodySteps, not a per-step flag.",

            // Structural-elsewhere: the parent loop linkage is the LoopDefinition
            // that owns the step in its bodySteps; not duplicated per-step on the
            // wire arm.
            [nameof(BuilderStep.ParentLoopId)] =
                "Structural-elsewhere: parent-loop linkage is the owning LoopDefinition.bodySteps, not a per-step field.",
        };

    /// <summary>
    /// <see cref="WorkflowDefinition{TState}"/> members deliberately dropped from
    /// the wire form. Currently empty: every workflow-root member is projected.
    /// Key = property name; value = justification.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> WorkflowExclusions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // (intentionally empty — every WorkflowDefinition<TState> member is
            // projected by ToContract; see ProjectedWorkflowMembers proof below.)
        };

    /// <summary>
    /// T-EX1 — every public init/get-settable <see cref="BuilderStep"/> member is
    /// either demonstrably projected (proven behaviorally) or on the documented
    /// exclusion allow-list. Fails if a builder step member is neither.
    /// </summary>
    [Test]
    public async Task EveryStepMember_IsProjectedOrExplicitlyExcluded()
    {
        var projectedNames = await ProjectedStepMemberNamesAsync();

        foreach (var prop in SettableProperties(typeof(BuilderStep)))
        {
            var isProjected = projectedNames.Contains(prop.Name);
            var isExcluded = StepExclusions.ContainsKey(prop.Name);

            await Assert.That(isProjected || isExcluded).IsTrue()
                .Because(
                    $"builder member StepDefinition.{prop.Name} is neither projected " +
                    "into the wire output nor on the documented exclusion allow-list. " +
                    "Either wire it into WorkflowDefinitionProjection.ProjectStep (and " +
                    "prove it surfaces in the emitted JSON) or add it to StepExclusions " +
                    "with a one-line justification.");

            // A member must not be BOTH projected and excluded — that would mean
            // the allow-list lies about a field that is actually carried.
            await Assert.That(isProjected && isExcluded).IsFalse()
                .Because(
                    $"StepDefinition.{prop.Name} is on the exclusion allow-list yet its " +
                    "value surfaces in the projected wire output — the allow-list entry " +
                    "is stale and must be removed.");
        }
    }

    /// <summary>
    /// T-EX2 — every public init/get-settable
    /// <see cref="WorkflowDefinition{TState}"/> member is either demonstrably
    /// projected or on the documented exclusion allow-list.
    /// </summary>
    [Test]
    public async Task EveryWorkflowMember_IsProjectedOrExplicitlyExcluded()
    {
        var projectedNames = await ProjectedWorkflowMemberNamesAsync();

        foreach (var prop in SettableProperties(typeof(WorkflowDefinition<TestWorkflowState>)))
        {
            var isProjected = projectedNames.Contains(prop.Name);
            var isExcluded = WorkflowExclusions.ContainsKey(prop.Name);

            await Assert.That(isProjected || isExcluded).IsTrue()
                .Because(
                    $"builder member WorkflowDefinition<TState>.{prop.Name} is neither " +
                    "projected into the wire output nor on the documented exclusion " +
                    "allow-list. Either wire it into WorkflowDefinitionProjection.ToContract " +
                    "(and prove it surfaces) or add it to WorkflowExclusions with a " +
                    "justification.");

            await Assert.That(isProjected && isExcluded).IsFalse()
                .Because(
                    $"WorkflowDefinition<TState>.{prop.Name} is on the exclusion allow-list " +
                    "yet its value surfaces in the projected wire output — the allow-list " +
                    "entry is stale.");
        }
    }

    /// <summary>
    /// T-EX3 — the allow-lists may not name a member that does not exist on the
    /// builder IR. Guards against an entry rotting after a builder rename.
    /// </summary>
    [Test]
    public async Task ExclusionAllowLists_OnlyNameExistingMembers()
    {
        var stepMembers = SettableProperties(typeof(BuilderStep))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in StepExclusions.Keys)
        {
            await Assert.That(stepMembers.Contains(name)).IsTrue()
                .Because($"StepExclusions names '{name}', not a settable member of StepDefinition.");
        }

        var workflowMembers = SettableProperties(typeof(WorkflowDefinition<TestWorkflowState>))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in WorkflowExclusions.Keys)
        {
            await Assert.That(workflowMembers.Contains(name)).IsTrue()
                .Because($"WorkflowExclusions names '{name}', not a settable member of WorkflowDefinition<TState>.");
        }
    }

    /// <summary>
    /// T-EX4 — every exclusion carries a non-empty justification string, so the
    /// allow-list stays self-documenting.
    /// </summary>
    [Test]
    public async Task EveryExclusion_HasNonEmptyJustification()
    {
        foreach (var (name, reason) in StepExclusions)
        {
            await Assert.That(string.IsNullOrWhiteSpace(reason)).IsFalse()
                .Because($"StepExclusions['{name}'] must carry a justification.");
        }

        foreach (var (name, reason) in WorkflowExclusions)
        {
            await Assert.That(string.IsNullOrWhiteSpace(reason)).IsFalse()
                .Because($"WorkflowExclusions['{name}'] must carry a justification.");
        }
    }

    // -------------------------------------------------------------------------
    // Behavioral projection probes — each builds the member to a non-default
    // value, projects, serializes via ContractsJson, and records which builder
    // member names demonstrably surfaced in the emitted JSON.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a single step exercising every projectable StepDefinition member at
    /// a non-default value, projects + serializes it, and returns the set of
    /// builder member names whose value is present in the emitted JSON.
    /// </summary>
    private static async Task<IReadOnlySet<string>> ProjectedStepMemberNamesAsync()
    {
        var present = new HashSet<string>(StringComparer.Ordinal);

        // --- A typed (skill) step with every carried member at a sentinel value.
        var config = new StepConfigurationDefinition
        {
            ConfidenceThreshold = 0.42,
        };
        var typedStep = BuilderStep.Create(typeof(ValidateStep), instanceName: "ProbeInstance") with
        {
            StepId = "step-probe-id",
            StepName = "ProbeStepName",
            IsTerminal = true,
            Configuration = config,
        };

        var typedJson = ContractsJson.Serialize(ProjectStepViaReflection(typedStep));

        Record(present, nameof(BuilderStep.StepId), typedJson, "step-probe-id");
        Record(present, nameof(BuilderStep.StepName), typedJson, "ProbeStepName");
        Record(present, nameof(BuilderStep.InstanceName), typedJson, "ProbeInstance");
        Record(present, nameof(BuilderStep.IsTerminal), typedJson, "true");
        // Configuration surfaces structurally (its confidenceThreshold value).
        Record(present, nameof(BuilderStep.Configuration), typedJson, "0.42");

        // --- A lambda step proves the IsLambdaStep member surfaces (lambda:true).
        var lambdaStep = BuilderStep.CreateFromLambda(
            "ProbeLambda",
            (TestWorkflowState s, StepContext c, CancellationToken ct) =>
                Task.FromResult(StepResult<TestWorkflowState>.FromState(s)));

        var lambdaJson = ContractsJson.Serialize(ProjectStepViaReflection(lambdaStep));
        Record(present, nameof(BuilderStep.IsLambdaStep), lambdaJson, "\"lambda\": true");

        await Task.CompletedTask;
        return present;
    }

    /// <summary>
    /// Builds a workflow exercising every projectable WorkflowDefinition member at
    /// a non-default value, projects + serializes it, and returns the set of
    /// builder member names whose value is present in the emitted JSON.
    /// </summary>
    private static async Task<IReadOnlySet<string>> ProjectedWorkflowMemberNamesAsync()
    {
        var present = new HashSet<string>(StringComparer.Ordinal);

        var entry = BuilderStep.Create(typeof(ValidateStep)) with { StepId = "wf-entry" };
        var terminal = BuilderStep.Create(typeof(CompleteStep)) with { StepId = "wf-terminal", IsTerminal = true };

        var transition = new TransitionDefinition
        {
            TransitionId = "t-probe",
            FromStepId = "wf-entry",
            ToStepId = "wf-terminal",
        };
        var branchPoint = new BranchPointDefinition
        {
            BranchPointId = "bp-probe",
            FromStepId = "wf-entry",
            Paths = [],
        };
        var loop = new LoopDefinition
        {
            LoopId = "loop-probe",
            LoopName = "ProbeLoop",
            FromStepId = "wf-entry",
            MaxIterations = 7,
            BodySteps = [],
        };
        var failureHandler = new FailureHandlerDefinition
        {
            HandlerId = "fh-probe",
            Scope = FailureHandlerScope.Workflow,
            Steps = [],
        };
        var approval = new ApprovalDefinition
        {
            ApprovalPointId = "ap-probe",
            ApproverType = typeof(ValidateStep),
            Configuration = ApprovalConfiguration.Default,
            PrecedingStepId = "wf-entry",
        };
        var forkPoint = new ForkPointDefinition
        {
            ForkPointId = "fp-probe",
            FromStepId = "wf-entry",
            JoinStepId = "wf-terminal",
            Paths = [],
        };

        var workflow = WorkflowDefinition<TestWorkflowState>.Create("probe-workflow")
            .WithStep(entry)
            .WithStep(terminal)
            .WithEntryStep(entry)
            .WithTerminalStep(terminal)
            .WithTransitions([transition])
            .WithBranchPoints([branchPoint])
            .WithLoops([loop])
            .WithFailureHandlers([failureHandler])
            .WithApprovalPoints([approval])
            .WithForkPoints([forkPoint]);

        var v1 = workflow.ToContract();
        var json = ContractsJson.Serialize(v1);

        Record(present, nameof(workflow.Name), json, "probe-workflow");
        Record(present, "Steps", json, "wf-entry");
        Record(present, "Transitions", json, "t-probe");
        Record(present, "BranchPoints", json, "bp-probe");
        Record(present, "Loops", json, "loop-probe");
        Record(present, "FailureHandlers", json, "fh-probe");
        Record(present, "ApprovalPoints", json, "ap-probe");
        Record(present, "ForkPoints", json, "fp-probe");
        Record(present, "EntryStep", json, "wf-entry");
        Record(present, "TerminalStep", json, "wf-terminal");

        await Task.CompletedTask;
        return present;
    }

    /// <summary>
    /// Records that <paramref name="memberName"/> demonstrably surfaced if the
    /// sentinel <paramref name="needle"/> is present in <paramref name="json"/>.
    /// </summary>
    private static void Record(
        ISet<string> present, string memberName, string json, string needle)
    {
        if (json.Contains(needle, StringComparison.Ordinal))
        {
            present.Add(memberName);
        }
    }

    /// <summary>
    /// Invokes the private <c>ProjectStep</c> seam so a bare
    /// <see cref="BuilderStep"/> (outside a full workflow) can be projected for
    /// the behavioral probe. Returns the wire step arm.
    /// </summary>
    private static Wire.StepDefinition ProjectStepViaReflection(BuilderStep step)
    {
        var method = typeof(WorkflowDefinitionProjection).GetMethod(
            "ProjectStep", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "WorkflowDefinitionProjection.ProjectStep seam not found — the probe " +
                "must be updated to match the projection's private surface.");

        return (Wire.StepDefinition)method.Invoke(null, [step])!;
    }

    /// <summary>
    /// The public, externally-settable (init/set) data properties of a builder
    /// type — the surface a consumer can populate and therefore the surface the
    /// projection must account for. Excludes indexers and non-settable computed
    /// members are kept (StepTypeName) so the allow-list must justify them.
    /// </summary>
    private static IEnumerable<PropertyInfo> SettableProperties(Type type)
    {
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            // Include init/set-settable members (consumer-populatable state) AND
            // public get-only computed members (e.g. StepTypeName) — the latter
            // must still be accounted for (projected or justified), so a future
            // computed member is not silently ignored.
            yield return prop;
        }
    }
}
