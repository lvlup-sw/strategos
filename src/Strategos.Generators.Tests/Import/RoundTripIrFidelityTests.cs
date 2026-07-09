// -----------------------------------------------------------------------
// <copyright file="RoundTripIrFidelityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Xml;

using Strategos.Abstractions;
using Strategos.Builders;
using Strategos.Contracts;
using Strategos.Definitions;
using Strategos.Generators.Import;
using Strategos.Steps;

namespace Strategos.Generators.Tests.Import;

// =============================================================================
// Task 019 (#100), DR-15 — the IR-fidelity half of the capstone round-trip gate.
//
// The partition gate (Strategos.Tests.RoundTripEquivalenceTests) proves each #53
// corpus fixture lands in exactly one bucket. This suite proves the OTHER DR-15
// obligation for the importable bucket: across the importable shape space the
// bridge's WorkflowModel matches the exported JSON FIELD-FOR-FIELD — steps,
// ordering, instance names, config values (retry / timeout), and fork edges.
//
// It exercises the SAME machinery as the corpus: the fluent Workflow<T> builder,
// WorkflowDefinitionProjection.ToContract(), the contracts canonical serializer,
// the generator's WireWorkflowReader, and WireToModelBridge. Because the #53
// WorkflowCorpus is internal to Strategos.Tests (and the WorkflowModel is internal
// to the generator, homed here per the task's InternalsVisibleTo guidance), the
// importable partition is rebuilt here from the real builder + projection over
// distinct-typed step shapes, so every step's identity in the model corresponds
// unambiguously to one wire moniker.
// =============================================================================

/// <summary>
/// Task 019 (#100), DR-15 — field-for-field JSON→<see cref="WorkflowModel"/> fidelity across the
/// importable partition. Each case is built with the fluent builder, exported through
/// <c>ToContract()</c> + the contracts canonical serializer, read back through the generator's
/// <see cref="WireWorkflowReader"/>, bridged via <see cref="WireToModelBridge"/>, and the resulting
/// model is compared field-for-field against the wire DTO.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class RoundTripIrFidelityTests
{
    /// <summary>
    /// A linear chain of distinct step types maps step-for-step and in document order: the model's
    /// step monikers, phase names, and (namespaced) type names correspond to the wire skill steps,
    /// and no construct (fork / loop / branch / approval / failure handler) is invented.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinearChain_StepsAndOrdering_MatchJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-linear")
                .StartWith<FidValidateStep>()
                .Then<FidProcessStep>()
                .Then<FidNotifyStep>()
                .Finally<FidCompleteStep>(),
            "rt-linear");

        await Assert.That(model.WorkflowName).IsEqualTo(dto.Name);

        var wireMonikers = TopLevelSkillMonikers(dto);
        await AssertOrderedEqual(
            wireMonikers,
            ["FidValidateStep", "FidProcessStep", "FidNotifyStep", "FidCompleteStep"],
            "the exported wire steps preserve document order.");

        // Steps: one model step per wire step, same monikers, same order.
        await AssertOrderedEqual(
            model.Steps!.Select(s => s.StepName),
            wireMonikers,
            "the model's steps must correspond to the wire skill steps in document order.");

        // Ordering: the phase-name list preserves document order.
        await AssertOrderedEqual(
            model.StepNames,
            wireMonikers,
            "the model's phase-name ordering must match the wire step order.");

        // LB-2: the model carries a namespaced descriptor whose leaf is the wire moniker.
        foreach (var step in model.Steps!)
        {
            await Assert.That(step.StepTypeName.EndsWith("." + step.StepName, StringComparison.Ordinal)).IsTrue()
                .Because($"the model's StepTypeName must be the namespaced form of the wire moniker '{step.StepName}'.");
        }

        // No construct is invented for a plain linear chain.
        await Assert.That(model.Forks).IsNull();
        await Assert.That(model.Loops).IsNull();
        await Assert.That(model.Branches).IsNull();
        await Assert.That(model.ApprovalPoints).IsNull();
        await Assert.That(model.FailureHandlers).IsNull();
    }

    /// <summary>
    /// Instance names carried on wire steps become the model's phase names (LB-2 identity): a
    /// named-step chain's <c>InstanceName</c> and derived phase names match the wire document.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NamedInstances_PhaseNames_MatchJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-named")
                .StartWith<FidValidateStep>("Entry")
                .Then<FidProcessStep>("Work")
                .Finally<FidCompleteStep>(),
            "rt-named");

        // Instance names round-trip onto the model steps.
        var wireInstanceNames = dto.Steps.Select(s => s.InstanceName);
        await AssertOrderedEqual(
            model.Steps!.Select(s => s.InstanceName),
            wireInstanceNames,
            "each wire step's instanceName must land on the model step in order.");

        // The effective phase names (instanceName ?? stepName) drive the ordered StepNames list.
        await AssertOrderedEqual(
            model.StepNames,
            ["Entry", "Work", "FidCompleteStep"],
            "the effective phase names must match the wire order.");
    }

    /// <summary>
    /// A step's retry policy round-trips value-for-value across ALL five sub-fields: the model step's
    /// <c>Retry</c> carries <c>MaxAttempts</c>, <c>InitialDelay</c> and <c>MaxDelay</c> (parsed back
    /// from the wire ISO-8601 durations), <c>BackoffMultiplier</c>, and <c>UseJitter</c> — not just
    /// <c>MaxAttempts</c> (L1: the delay-shaping sub-fields were previously untested, so a bridge
    /// regression dropping any of them passed the suite).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RetryConfig_MatchesJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-retry")
                .StartWith<FidValidateStep>()
                .Then<FidProcessStep>(step => step.WithRetry(4))
                .Finally<FidCompleteStep>(),
            "rt-retry");

        var wireRetry = FindSkill(dto, "FidProcessStep").Configuration?.Retry;
        await Assert.That(wireRetry).IsNotNull()
            .Because("the exported JSON must carry the retry configuration.");
        await Assert.That(wireRetry!.MaxAttempts).IsEqualTo(4);

        // The delay-shaping sub-fields are exported (defaults from RetryConfiguration.Create): assert
        // the wire actually carries each so the round-trip below is a real comparison, not vacuous.
        await Assert.That(wireRetry.InitialDelay).IsNotNull();
        await Assert.That(wireRetry.MaxDelay).IsNotNull();
        await Assert.That(wireRetry.BackoffMultiplier).IsNotNull();
        await Assert.That(wireRetry.UseJitter).IsNotNull();

        var modelStep = model.Steps!.Single(s => s.StepName == "FidProcessStep");
        await Assert.That(modelStep.Retry).IsNotNull()
            .Because("the bridge must carry the wire retry policy onto the model step.");
        await Assert.That(modelStep.Retry!.MaxAttempts).IsEqualTo(wireRetry.MaxAttempts)
            .Because("the model's retry MaxAttempts must match the wire value field-for-field.");
        await Assert.That(modelStep.Retry.InitialDelay).IsEqualTo(XmlConvert.ToTimeSpan(wireRetry.InitialDelay!))
            .Because("the model's InitialDelay must equal the wire ISO-8601 duration parsed back.");
        await Assert.That(modelStep.Retry.MaxDelay).IsEqualTo(XmlConvert.ToTimeSpan(wireRetry.MaxDelay!))
            .Because("the model's MaxDelay must equal the wire ISO-8601 duration parsed back.");
        await Assert.That(modelStep.Retry.BackoffMultiplier).IsEqualTo(wireRetry.BackoffMultiplier)
            .Because("the model's BackoffMultiplier must match the wire value field-for-field.");
        await Assert.That(modelStep.Retry.UseJitter).IsEqualTo(wireRetry.UseJitter ?? false)
            .Because("the model's UseJitter must match the wire value field-for-field.");
    }

    /// <summary>
    /// A step's timeout round-trips value-for-value: the model step's <c>Timeout</c> equals the
    /// wire ISO-8601 duration parsed back to a <see cref="TimeSpan"/>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TimeoutConfig_MatchesJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-timeout")
                .StartWith<FidValidateStep>()
                .Then<FidProcessStep>(step => step.WithTimeout(TimeSpan.FromSeconds(45)))
                .Finally<FidCompleteStep>(),
            "rt-timeout");

        var wireTimeout = FindSkill(dto, "FidProcessStep").Configuration?.Timeout;
        await Assert.That(wireTimeout).IsNotNull()
            .Because("the exported JSON must carry the timeout as an ISO-8601 duration.");

        var modelStep = model.Steps!.Single(s => s.StepName == "FidProcessStep");
        await Assert.That(modelStep.Timeout).IsNotNull();
        await Assert.That(modelStep.Timeout!.Timeout).IsEqualTo(XmlConvert.ToTimeSpan(wireTimeout!))
            .Because("the model's timeout must equal the wire ISO-8601 duration parsed back.");
        await Assert.That(modelStep.Timeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(45));
    }

    /// <summary>
    /// M3: a step's compensation (rollback) policy round-trips field-for-field. The model step's
    /// <c>Compensation</c> carries the resolved compensation step type (its namespaced descriptor,
    /// leaf = the wire simple-name moniker), the <c>RequiredOnFailure</c> default, and
    /// <c>IsRegisteredStep</c> = true (the moniker resolves against the test assembly); and the
    /// compensation step type is FOLDED into the model's step list (so it gets its worker command /
    /// handler / completed event / DI registration) while staying OFF the linear phase-name chain. A
    /// regression dropping the compensation fold or flipping the <c>RequiredOnFailure</c> default was
    /// previously undetectable across the whole suite.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CompensationConfig_MatchesJsonFieldForField_AndFoldsCompensationStep()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-comp")
                .StartWith<FidValidateStep>()
                .Then<FidProcessStep>(step => step.Compensate<FidCompensateStep>())
                .Finally<FidCompleteStep>(),
            "rt-comp");

        var wireComp = FindSkill(dto, "FidProcessStep").Configuration?.Compensation;
        await Assert.That(wireComp).IsNotNull()
            .Because("the exported JSON must carry the compensation configuration.");
        await Assert.That(wireComp!.CompensationStepType).IsEqualTo("FidCompensateStep")
            .Because("the wire compensation moniker is the compensation step's simple type name (LB-2).");

        var modelStep = model.Steps!.Single(s => s.StepName == "FidProcessStep");
        await Assert.That(modelStep.Compensation).IsNotNull()
            .Because("the bridge must carry the wire compensation policy onto the model step.");
        await Assert.That(
                modelStep.Compensation!.CompensationStepTypeName.EndsWith("." + wireComp.CompensationStepType!, StringComparison.Ordinal))
            .IsTrue()
            .Because("the model's compensation type name must be the namespaced form of the wire moniker.");
        await Assert.That(modelStep.Compensation.RequiredOnFailure).IsEqualTo(wireComp.RequiredOnFailure ?? true)
            .Because("the model's RequiredOnFailure must match the wire value (default true) field-for-field.");
        await Assert.That(modelStep.Compensation.IsRegisteredStep).IsTrue()
            .Because("the compensation moniker resolves to a real IWorkflowStep<FidState> in the test assembly.");

        // The compensation step type is folded into the model's step MODELS (for its worker command /
        // handler / DI registration) but NOT onto the linear phase-name chain (it is reached only via
        // the saga compensation handler, never the happy path).
        var foldedComp = model.Steps!.SingleOrDefault(s => s.StepName == "FidCompensateStep");
        await Assert.That(foldedComp).IsNotNull()
            .Because("FoldCompensationSteps must add the compensation step type to the model step list.");
        await Assert.That(
                foldedComp!.StepTypeName.EndsWith(".FidCompensateStep", StringComparison.Ordinal))
            .IsTrue()
            .Because("the folded compensation StepModel must carry the compensation step's descriptor.");
        await Assert.That(model.StepNames).DoesNotContain("FidCompensateStep")
            .Because("the compensation step stays off the linear phase-name chain (compensation-handler-only).");
    }

    /// <summary>
    /// A two-path fork/join maps edge-for-edge: the model fork's path step monikers, the join step,
    /// and the pre-fork step correspond to the wire fork point, and the fork path steps are woven
    /// into the model step list.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkEdges_MatchJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-fork")
                .StartWith<FidValidateStep>()
                .Fork(
                    p => p.Then<FidAutoStep>(),
                    p => p.Then<FidManualStep>())
                .Join<FidNotifyStep>()
                .Finally<FidCompleteStep>(),
            "rt-fork");

        await Assert.That(dto.ForkPoints.Count).IsEqualTo(1);
        await Assert.That(model.Forks).IsNotNull();
        await Assert.That(model.Forks!.Count).IsEqualTo(1);

        var wireFork = dto.ForkPoints[0];
        var fork = model.Forks![0];

        await Assert.That(fork.Paths.Count).IsEqualTo(wireFork.Paths.Count)
            .Because("the model fork must carry one path per wire fork path.");

        // Path step monikers correspond edge-for-edge, in fork-path order.
        var wirePathMonikers = wireFork.Paths
            .Select(p => ((SkillStep)p.Steps[0]).StepType!)
            .ToList();
        await AssertOrderedEqual(
            fork.Paths.Select(p => p.Steps[0].StepName),
            wirePathMonikers,
            "each model fork path's first step must match the wire fork path step moniker, in order.");
        await AssertOrderedEqual(
            wirePathMonikers,
            ["FidAutoStep", "FidManualStep"],
            "the exported fork paths preserve their declared order.");

        // The pre-fork and join steps resolve to their step names.
        await Assert.That(fork.PreviousStepName).IsEqualTo("FidValidateStep")
            .Because("the fork's fromStepId must resolve to the pre-fork step name.");
        await Assert.That(fork.JoinStepName).IsEqualTo("FidNotifyStep")
            .Because("the fork's joinStepId must resolve to the join step name.");

        // The generated fork identity is deterministic: {PascalName}-Fork{index}.
        await Assert.That(fork.ForkId).IsEqualTo("RtFork-Fork0");

        // Fork path steps are woven into the model step list (off the top-level phase chain).
        var modelStepNames = model.Steps!.Select(s => s.StepName).ToList();
        await Assert.That(modelStepNames).Contains("FidAutoStep");
        await Assert.That(modelStepNames).Contains("FidManualStep");
    }

    /// <summary>
    /// A three-path fork/join preserves fork arity and all path monikers field-for-field.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ThreePathForkEdges_MatchJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-fork3")
                .StartWith<FidValidateStep>()
                .Fork(
                    p => p.Then<FidAutoStep>(),
                    p => p.Then<FidManualStep>(),
                    p => p.Then<FidNotifyStep>())
                .Join<FidRefineStep>()
                .Finally<FidCompleteStep>(),
            "rt-fork3");

        var fork = model.Forks!.Single();
        await Assert.That(fork.Paths.Count).IsEqualTo(3);
        await Assert.That(fork.Paths.Count).IsEqualTo(dto.ForkPoints[0].Paths.Count);

        var wirePathMonikers = dto.ForkPoints[0].Paths.Select(p => ((SkillStep)p.Steps[0]).StepType!).ToList();
        await AssertOrderedEqual(
            fork.Paths.Select(p => p.Steps[0].StepName),
            wirePathMonikers,
            "the three fork paths map edge-for-edge in order.");
        await Assert.That(fork.JoinStepName).IsEqualTo("FidRefineStep");
    }

    /// <summary>
    /// An importable workflow declaring a workflow-scoped <c>OnFailure</c> handler imports its
    /// top-level step list field-for-field. The builder appends the handler's steps to the top-level
    /// step collection (so they are on the wire as ordinary <c>skill</c> steps), and the bridge maps
    /// every top-level step — so the handler step IS carried, as a top-level step. Only the
    /// failure-ROUTING construct (<c>FailureHandlers</c>) is not lowered by the import subset (a #100
    /// follow-on), which is why an <c>onFailure</c> workflow imports (bucket a) but is NOT claimed as
    /// a behaviorally-identical twin: the dropped routing turns the handler into a happy-path step.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OnFailure_TopLevelStepsIncludingHandler_MatchJsonFieldForField()
    {
        var (dto, model) = BridgeRoundTrip(
            Workflow<FidState>.Create("rt-onfailure")
                .StartWith<FidValidateStep>()
                .Then<FidProcessStep>()
                .OnFailure(f => f.Then<FidLogStep>())
                .Finally<FidCompleteStep>(),
            "rt-onfailure");

        // The export carried the failure-handler routing construct AND appended its step to the
        // top-level wire steps.
        await Assert.That(dto.FailureHandlers.Count).IsGreaterThan(0)
            .Because("the exported JSON must carry the workflow-scoped failure-handler routing.");

        // Field-for-field: the model's step monikers equal the wire's top-level skill monikers,
        // in order — the handler step is carried as a top-level step.
        var wireMonikers = TopLevelSkillMonikers(dto);
        await Assert.That(wireMonikers).Contains("FidLogStep")
            .Because("the builder appends the OnFailure handler step to the top-level wire steps.");
        await AssertOrderedEqual(
            model.Steps!.Select(s => s.StepName),
            wireMonikers,
            "every top-level wire step maps to a model step, field-for-field and in order.");

        // The failure-ROUTING construct is not lowered by the import subset (documented follow-on).
        await Assert.That(model.FailureHandlers).IsNull()
            .Because("the import subset does not lower the failure-routing construct onto the model.");
    }

    /// <summary>
    /// Breadth check across the importable shape space: linear chains of varying length all import
    /// with their model steps corresponding to the wire skill steps field-for-field.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportablePartition_LinearShapes_MatchJsonFieldForField()
    {
        for (var length = 1; length <= 8; length++)
        {
            var builder = Workflow<FidState>.Create($"rt-chain-{length}").StartWith<FidValidateStep>();
            for (var i = 0; i < length; i++)
            {
                builder = builder.Then<FidProcessStep>($"Work{i}");
            }

            var (dto, model) = BridgeRoundTrip(builder.Finally<FidCompleteStep>(), $"rt-chain-{length}");

            await Assert.That(model).IsNotNull()
                .Because($"chain of length {length} must import to a model.");
            await Assert.That(model.WorkflowName).IsEqualTo(dto.Name);

            // Every wire skill step's effective phase name appears in the model's ordered step names.
            var wirePhaseNames = dto.Steps
                .Select(s => string.IsNullOrEmpty(s.InstanceName) ? ((SkillStep)s).StepType! : s.InstanceName!)
                .ToList();
            await AssertOrderedEqual(
                model.StepNames,
                wirePhaseNames,
                $"chain of length {length}: model phase names must match the wire step order field-for-field.");
        }
    }

    /// <summary>
    /// Bridges a built workflow through export → serialize → read → bridge, returning the wire DTO
    /// and the resulting non-null <see cref="WorkflowModel"/>.
    /// </summary>
    private static (WorkflowDefinitionV1 Dto, WorkflowModel Model) BridgeRoundTrip(
        WorkflowDefinition<FidState> workflow,
        string name)
    {
        var json = ContractsJson.Serialize(workflow.ToContract());
        var dto = WireWorkflowReader.Read(json);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RoundTripFidelityBridgeAssembly",
            syntaxTrees: [],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = WireToModelBridge.Bridge(dto, compilation, name + ".workflow.json", CancellationToken.None);

        if (result.Model is null)
        {
            throw new InvalidOperationException(
                $"Expected '{name}' to bridge to a model, but it did not. Diagnostics: " +
                string.Join(", ", result.Diagnostics.Select(d => d.Id)));
        }

        return (dto, result.Model);
    }

    /// <summary>
    /// Asserts two string sequences are equal element-for-element IN ORDER. Collapses each sequence
    /// to a delimited string so the comparison is order-sensitive (TUnit's <c>IsEquivalentTo</c> is
    /// order-insensitive) and produces a readable diff on failure.
    /// </summary>
    private static async Task AssertOrderedEqual(
        IEnumerable<string?> actual,
        IEnumerable<string?> expected,
        string because)
    {
        static string Render(IEnumerable<string?> items) =>
            string.Join(" | ", items.Select(x => x ?? "<null>"));

        await Assert.That(Render(actual)).IsEqualTo(Render(expected)).Because(because);
    }

    private static List<string> TopLevelSkillMonikers(WorkflowDefinitionV1 dto) =>
        dto.Steps.OfType<SkillStep>().Select(s => s.StepType!).ToList();

    private static SkillStep FindSkill(WorkflowDefinitionV1 dto, string moniker) =>
        dto.Steps.OfType<SkillStep>().Single(s => s.StepType == moniker);

    private static List<MetadataReference> GetReferences()
    {
        var references = new List<MetadataReference>();

        var runtimePath = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var assembly in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = System.IO.Path.Combine(runtimePath, assembly);
            if (System.IO.File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        // The running test assembly (carrying the public Fid* step types below) is in the AppDomain,
        // so the bridge resolves the wire monikers against these real IWorkflowStep<FidState> types.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references.
                }
            }
        }

        return references;
    }
}

// ---------------------------------------------------------------------------
// Public, distinct-typed step shapes used to build the importable fidelity cases. They are built
// via the fluent Workflow<T> builder (so the exported wire monikers are their simple names) and
// resolved by the bridge against the running test assembly (so each model step corresponds to
// exactly one wire moniker — no instance-name/type-reuse ambiguity).
// ---------------------------------------------------------------------------

/// <summary>Workflow state for the round-trip IR-fidelity fixtures.</summary>
public sealed record FidState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }
}

/// <summary>Entry step for the fidelity fixtures.</summary>
public sealed class FidValidateStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A middle step for the fidelity fixtures.</summary>
public sealed class FidProcessStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A middle step for the fidelity fixtures.</summary>
public sealed class FidNotifyStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A terminal step for the fidelity fixtures.</summary>
public sealed class FidCompleteStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A fork-path step for the fidelity fixtures.</summary>
public sealed class FidAutoStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A fork-path step for the fidelity fixtures.</summary>
public sealed class FidManualStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A fork-join step for the fidelity fixtures.</summary>
public sealed class FidRefineStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A failure-handler step for the fidelity fixtures.</summary>
public sealed class FidLogStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}

/// <summary>A compensation (rollback) step for the fidelity fixtures.</summary>
public sealed class FidCompensateStep : IWorkflowStep<FidState>
{
    /// <inheritdoc />
    public Task<StepResult<FidState>> ExecuteAsync(FidState s, StepContext c, CancellationToken ct)
        => Task.FromResult(StepResult<FidState>.FromState(s));
}
