// -----------------------------------------------------------------------
// <copyright file="SingleLoweringPathGuardTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// INV-1 architecture guard (task 017): the JSON import front-end feeds the EXISTING
/// <see cref="WorkflowModel"/> IR into the SAME saga emitters as the C#-authoring front-end — one
/// lowering path, zero forked emitter logic. The teeth of the guard are byte-level equivalence: a
/// workflow authored two ways (a C# <c>[Workflow]</c> definition and a <c>*.workflow.json</c>
/// import) generates BYTE-IDENTICAL saga / commands / events / phase / handlers, exercised for the
/// linear, confidence-gated, and fork emitter paths. If a future change forked the import path onto
/// its own emitter logic (or stubbed it), the two outputs would diverge and these tests go red.
/// </summary>
/// <remarks>
/// The two authoring forms are compiled and generated in SEPARATE compilations (never the same one)
/// so they can share the workflow name, namespace, and step CLR types without the generator's
/// deliberate one-step-type-per-workflow-definition CS0101 collision — the shared inputs are exactly
/// what makes byte-identity a meaningful proof.
/// </remarks>
[Property("Category", "WorkflowIr")]
public sealed class SingleLoweringPathGuardTests
{
    // ---------------------------------------------------------------------
    // Linear flow: StartWith<A>().Finally<B>()  ≡  a two-skill-step import.
    // ---------------------------------------------------------------------
    private const string LinearStepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace GuardNs;

        [WorkflowState]
        public sealed record GuardState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class GuardStepA : IWorkflowStep<GuardState>
        {
            public Task<StepResult<GuardState>> ExecuteAsync(GuardState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GuardState>.FromState(s));
        }

        public sealed class GuardStepB : IWorkflowStep<GuardState>
        {
            public Task<StepResult<GuardState>> ExecuteAsync(GuardState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GuardState>.FromState(s));
        }
        """;

    private const string LinearCSharpWorkflow = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;

        namespace GuardNs;

        [WorkflowState]
        public sealed record GuardState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class GuardStepA : IWorkflowStep<GuardState>
        {
            public Task<StepResult<GuardState>> ExecuteAsync(GuardState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GuardState>.FromState(s));
        }

        public sealed class GuardStepB : IWorkflowStep<GuardState>
        {
            public Task<StepResult<GuardState>> ExecuteAsync(GuardState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GuardState>.FromState(s));
        }

        [Workflow("guard-linear")]
        public static partial class GuardLinearWorkflow
        {
            public static WorkflowDefinition<GuardState> Definition => Workflow<GuardState>
                .Create("guard-linear")
                .StartWith<GuardStepA>()
                .Finally<GuardStepB>();
        }
        """;

    private const string LinearJson = """
        {
          "schemaVersion": "1.0",
          "name": "guard-linear",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "GuardStepA", "isTerminal": false, "stepType": "GuardStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "GuardStepB", "isTerminal": true, "stepType": "GuardStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "s1",
          "terminalStepId": "s2"
        }
        """;

    // ---------------------------------------------------------------------
    // Confidence gate: Then<Gated>(RequireConfidence(0.85).OnLowConfidence(Review))
    // ---------------------------------------------------------------------
    private const string ConfidenceStepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace GuardNs;

        [WorkflowState]
        public sealed record ConfState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class ConfPrepare : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.FromState(s));
        }

        public sealed class ConfClassify : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.WithConfidence(s, 0.5));
        }

        public sealed class ConfReview : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.FromState(s));
        }

        public sealed class ConfFinish : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.FromState(s));
        }
        """;

    private const string ConfidenceCSharpWorkflow = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;

        namespace GuardNs;

        [WorkflowState]
        public sealed record ConfState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class ConfPrepare : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.FromState(s));
        }

        public sealed class ConfClassify : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.WithConfidence(s, 0.5));
        }

        public sealed class ConfReview : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.FromState(s));
        }

        public sealed class ConfFinish : IWorkflowStep<ConfState>
        {
            public Task<StepResult<ConfState>> ExecuteAsync(ConfState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ConfState>.FromState(s));
        }

        [Workflow("guard-confidence")]
        public static partial class GuardConfidenceWorkflow
        {
            public static WorkflowDefinition<ConfState> Definition => Workflow<ConfState>
                .Create("guard-confidence")
                .StartWith<ConfPrepare>()
                .Then<ConfClassify>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<ConfReview>()))
                .Finally<ConfFinish>();
        }
        """;

    private const string ConfidenceJson = """
        {
          "schemaVersion": "1.0",
          "name": "guard-confidence",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "ConfPrepare", "isTerminal": false, "stepType": "ConfPrepare" },
            { "kind": "skill", "stepId": "s2", "stepName": "ConfClassify", "isTerminal": false, "stepType": "ConfClassify",
              "configuration": {
                "confidenceThreshold": 0.85,
                "onLowConfidence": {
                  "handlerId": "h1",
                  "isTerminal": true,
                  "handlerSteps": [ { "kind": "skill", "stepId": "h1s1", "stepName": "ConfReview", "isTerminal": false, "stepType": "ConfReview" } ]
                }
              } },
            { "kind": "skill", "stepId": "s3", "stepName": "ConfFinish", "isTerminal": true, "stepType": "ConfFinish" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;

    // ---------------------------------------------------------------------
    // Fork/Join: StartWith<Intake>().Fork(Assess | Review).Join<Aggregate>().Finally<Settle>()
    // ---------------------------------------------------------------------
    private const string ForkStepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace GuardNs;

        [WorkflowState]
        public sealed record ForkState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class ForkIntake : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkAssess : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkReview : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkAggregate : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkSettle : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }
        """;

    private const string ForkCSharpWorkflow = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;

        namespace GuardNs;

        [WorkflowState]
        public sealed record ForkState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class ForkIntake : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkAssess : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkReview : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkAggregate : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        public sealed class ForkSettle : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(ForkState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<ForkState>.FromState(s));
        }

        [Workflow("guard-fork")]
        public static partial class GuardForkWorkflow
        {
            public static WorkflowDefinition<ForkState> Definition => Workflow<ForkState>
                .Create("guard-fork")
                .StartWith<ForkIntake>()
                .Fork(
                    path => path.Then<ForkAssess>(),
                    path => path.Then<ForkReview>())
                .Join<ForkAggregate>()
                .Finally<ForkSettle>();
        }
        """;

    private const string ForkJson = """
        {
          "schemaVersion": "1.0",
          "name": "guard-fork",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "ForkIntake", "isTerminal": false, "stepType": "ForkIntake" },
            { "kind": "skill", "stepId": "s2", "stepName": "ForkAggregate", "isTerminal": false, "stepType": "ForkAggregate" },
            { "kind": "skill", "stepId": "s3", "stepName": "ForkSettle", "isTerminal": true, "stepType": "ForkSettle" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [
            {
              "forkPointId": "guard-fork-Fork0",
              "fromStepId": "s1",
              "joinStepId": "s2",
              "paths": [
                { "pathId": "p0", "pathIndex": 0, "steps": [ { "kind": "skill", "stepId": "fp0", "stepName": "ForkAssess", "isTerminal": false, "stepType": "ForkAssess" } ] },
                { "pathId": "p1", "pathIndex": 1, "steps": [ { "kind": "skill", "stepId": "fp1", "stepName": "ForkReview", "isTerminal": false, "stepType": "ForkReview" } ] }
              ]
            }
          ],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;

    // ---------------------------------------------------------------------
    // Gate tolerance (DR-3): a gate-bearing import ≡ its gate-free twin.
    // ---------------------------------------------------------------------
    private const string GateStepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace GuardNs;

        [WorkflowState]
        public sealed record GateState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class GatePrepare : IWorkflowStep<GateState>
        {
            public Task<StepResult<GateState>> ExecuteAsync(GateState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GateState>.FromState(s));
        }

        public sealed class GateCheck : IWorkflowStep<GateState>
        {
            public Task<StepResult<GateState>> ExecuteAsync(GateState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GateState>.FromState(s));
        }

        public sealed class GateFinish : IWorkflowStep<GateState>
        {
            public Task<StepResult<GateState>> ExecuteAsync(GateState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<GateState>.FromState(s));
        }
        """;

    private const string GateFreeJson = """
        {
          "schemaVersion": "1.0",
          "name": "guard-gate",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "GatePrepare", "isTerminal": false, "stepType": "GatePrepare" },
            { "kind": "skill", "stepId": "s2", "stepName": "GateCheck", "isTerminal": false, "stepType": "GateCheck" },
            { "kind": "skill", "stepId": "s3", "stepName": "GateFinish", "isTerminal": true, "stepType": "GateFinish" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [],
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;

    private const string GateBearingJson = """
        {
          "schemaVersion": "1.0",
          "name": "guard-gate",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "GatePrepare", "isTerminal": false, "stepType": "GatePrepare" },
            { "kind": "gate", "stepId": "s2", "stepName": "GateCheck", "isTerminal": false, "stepType": "GateCheck", "gateId": "g1" },
            { "kind": "skill", "stepId": "s3", "stepName": "GateFinish", "isTerminal": true, "stepType": "GateFinish" }
          ],
          "transitions": [],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [],
          "gates": [ { "class": "AntipatternDetection", "id": "g1" } ],
          "entryStepId": "s1",
          "terminalStepId": "s3"
        }
        """;

    // ---------------------------------------------------------------------
    // Diagnostic-fork edge (DR-10): a linear import carrying a diagnosticForks
    // edge attaches it to the model and flows it into the fork emitter.
    // ---------------------------------------------------------------------
    private const string DiagnosticForkJson = """
        {
          "schemaVersion": "1.0",
          "name": "guard-diag-fork",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "GuardStepA", "isTerminal": false, "stepType": "GuardStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "GuardStepB", "isTerminal": true, "stepType": "GuardStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [],
          "diagnosticForks": [
            {
              "anchorStepIds": [ "GuardStepA" ],
              "permittedTriggers": [ { "trigger": "ratification_failure", "requiredEvidenceFields": [ "stampId" ] } ],
              "maxForks": 2,
              "compensationSeed": "GuardStepB"
            }
          ],
          "entryStepId": "s1",
          "terminalStepId": "s2"
        }
        """;

    /// <summary>
    /// The generated artifacts whose byte-identity proves the single lowering path. The DI
    /// extension is deliberately excluded: a JSON import legitimately omits the one
    /// <c>_ = {Pascal}WorkflowDefinition.Definition;</c> line (it has no fluent definition class),
    /// which is asserted separately in <see cref="ImportExtension_MatchesCSharpTwin_ModuloFluentDefinitionLine"/>.
    /// </summary>
    private static readonly string[] LoweredArtifacts =
    [
        "Commands.g.cs",
        "Events.g.cs",
        "Phase.g.cs",
        "Handlers.g.cs",
        "Transitions.g.cs",
    ];

    /// <summary>
    /// The shared emitter sink exists: <see cref="WorkflowIncrementalGenerator.EmitWorkflowSources"/>
    /// is the single method both front-ends feed a model into. Its presence documents the invariant;
    /// the byte-identity tests below are what make a forked emitter path fail.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitWorkflowSources_IsTheSingleSharedEmitterSink()
    {
        var method = typeof(WorkflowIncrementalGenerator).GetMethod(
            "EmitWorkflowSources",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        await Assert.That(method).IsNotNull()
            .Because("both the C#-authoring and JSON-import pipelines must lower through one shared EmitWorkflowSources method.");
    }

    /// <summary>
    /// A linear JSON import lowers to the byte-identical saga as its C#-authored twin.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LinearImport_LowersToByteIdenticalSaga_AsCSharpTwin()
    {
        await AssertImportEqualsCSharpTwin(
            LinearCSharpWorkflow,
            LinearStepTypes,
            LinearJson,
            "guard-linear.workflow.json",
            "GuardLinearSaga.g.cs");
    }

    /// <summary>
    /// A confidence-gated JSON import lowers to the byte-identical saga (with the OnLowConfidence
    /// handler chain) as its C#-authored twin — the confidence emitter has one call path.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConfidenceImport_LowersToByteIdenticalSaga_AsCSharpTwin()
    {
        await AssertImportEqualsCSharpTwin(
            ConfidenceCSharpWorkflow,
            ConfidenceStepTypes,
            ConfidenceJson,
            "guard-confidence.workflow.json",
            "GuardConfidenceSaga.g.cs");
    }

    /// <summary>
    /// A fork/join JSON import lowers to the byte-identical saga as its C#-authored twin — the fork
    /// emitter has one call path.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkImport_LowersToByteIdenticalSaga_AsCSharpTwin()
    {
        await AssertImportEqualsCSharpTwin(
            ForkCSharpWorkflow,
            ForkStepTypes,
            ForkJson,
            "guard-fork.workflow.json",
            "GuardForkSaga.g.cs");
    }

    /// <summary>
    /// DR-3: a gate-bearing JSON import (a <c>gate</c> step with a <c>gateId</c> back-reference and a
    /// workflow <c>gates[]</c> declaration) lowers to BYTE-IDENTICAL saga / commands / events / phase
    /// as its gate-free twin — gates are consumer-plane data the saga never observes.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GateBearingImport_LowersIdentically_ToGateFreeTwin()
    {
        var gateFree = RunGenerator(GateStepTypes, ("guard-gate.workflow.json", GateFreeJson));
        var gateBearing = RunGenerator(GateStepTypes, ("guard-gate.workflow.json", GateBearingJson));

        var sagaFree = GetGenerated(gateFree, "GuardGateSaga.g.cs");
        var sagaBearing = GetGenerated(gateBearing, "GuardGateSaga.g.cs");

        await Assert.That(sagaBearing).IsNotEmpty()
            .Because("the gate-bearing import must lower a saga.");
        await Assert.That(sagaBearing).IsEqualTo(sagaFree)
            .Because("DR-3: a gate step + gates[] are inert consumer-plane data; the saga must be byte-identical to the gate-free twin.");

        foreach (var artifact in LoweredArtifacts)
        {
            var free = GetGenerated(gateFree, artifact);
            var bearing = GetGenerated(gateBearing, artifact);
            await Assert.That(bearing).IsEqualTo(free)
                .Because($"the gate-bearing import's {artifact} must be byte-identical to the gate-free twin's (gates are inert).");
        }
    }

    /// <summary>
    /// The imported workflow's DI extension registers the same step types + worker handlers as its
    /// C# twin's, differing ONLY by the fluent-definition-evaluation line the C# twin emits (a JSON
    /// import has no <c>{Pascal}WorkflowDefinition</c> class to reference, DR-12). Removing that one
    /// line from the C# twin's extension yields the byte-identical import extension.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportExtension_MatchesCSharpTwin_ModuloFluentDefinitionLine()
    {
        var csharp = RunGenerator(LinearCSharpWorkflow);
        var imported = RunGenerator(LinearStepTypes, ("guard-linear.workflow.json", LinearJson));

        var csharpExt = GetGenerated(csharp, "Extensions.g.cs");
        var importedExt = GetGenerated(imported, "Extensions.g.cs");

        await Assert.That(importedExt).IsNotEmpty()
            .Because("the import must still generate its DI extension.");
        await Assert.That(importedExt).DoesNotContain("WorkflowDefinition.Definition")
            .Because("a JSON import has no fluent definition class, so the extension must omit the definition-evaluation line.");
        await Assert.That(csharpExt).Contains("GuardLinearWorkflowDefinition.Definition")
            .Because("the C# twin's extension does force fluent-definition evaluation.");

        var csharpExtStripped = string.Join(
            "\n",
            csharpExt.Split('\n').Where(l =>
                !l.Contains("_ = GuardLinearWorkflowDefinition.Definition;")
                && !l.Contains("// Force evaluation of workflow definition to register loop conditions")));

        // After stripping only the fluent-definition-evaluation line + its comment, the two
        // extensions are byte-identical — the DI registration itself is one lowering path.
        await Assert.That(NormalizeBlankRuns(importedExt)).IsEqualTo(NormalizeBlankRuns(csharpExtStripped))
            .Because("the import's DI registration must be byte-identical to the C# twin's modulo the fluent-definition line.");
    }

    /// <summary>
    /// The fork emitter is genuinely reached through the import bridge (not stubbed): the fork import
    /// saga carries the parallel dispatch + join lowering.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkImport_ReachesForkEmitter_ThroughBridge()
    {
        var imported = RunGenerator(ForkStepTypes, ("guard-fork.workflow.json", ForkJson));
        var saga = GetGenerated(imported, "GuardForkSaga.g.cs");
        var commands = GetGenerated(imported, "GuardForkCommands.g.cs");

        await Assert.That(commands).Contains("DispatchFork_GuardFork_Fork0_Command")
            .Because("the imported fork workflow must lower the real fork dispatch command through the shared fork emitter.");
        await Assert.That(saga).Contains("CheckJoinReady_GuardFork_Fork0")
            .Because("the imported fork saga must carry the real join-readiness lowering (fork emitter reached via the bridge).");
    }

    /// <summary>
    /// DR-10: a linear import carrying a <c>diagnosticForks</c> edge attaches a
    /// <see cref="DiagnosticForkModel"/> to the model, which flows through the shared emitters and
    /// lowers the diagnostic-fork decision command — proving the bridge carries the edge to the
    /// (single) emitter path. The gate-free linear import (no edge) emits none of it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagnosticForkImport_AttachesEdge_AndLowersForkCommand()
    {
        var withEdge = RunGenerator(LinearStepTypes, ("guard-diag-fork.workflow.json", DiagnosticForkJson));
        var withoutEdge = RunGenerator(LinearStepTypes, ("guard-linear.workflow.json", LinearJson));

        var edgeCommands = GetGenerated(withEdge, "GuardDiagForkCommands.g.cs");
        var linearCommands = GetGenerated(withoutEdge, "GuardLinearCommands.g.cs");

        await Assert.That(edgeCommands).Contains("ForkGuardDiagForkCommand")
            .Because("the imported diagnostic-fork edge must attach to the model and lower the fork decision command through the shared emitter (DR-10).");
        await Assert.That(linearCommands).DoesNotContain("Command Fork")
            .Because("a linear import without a diagnostic-fork edge must not gain any fork-decision lowering (additive).");
    }

    /// <summary>
    /// Runs both authoring forms in SEPARATE compilations and asserts the saga (and the core lowered
    /// artifacts) are byte-identical.
    /// </summary>
    private static async Task AssertImportEqualsCSharpTwin(
        string csharpWorkflow,
        string stepTypesOnly,
        string json,
        string jsonPath,
        string sagaHint)
    {
        var csharp = RunGenerator(csharpWorkflow);
        var imported = RunGenerator(stepTypesOnly, (jsonPath, json));

        var csharpSaga = GetGenerated(csharp, sagaHint);
        var importedSaga = GetGenerated(imported, sagaHint);

        await Assert.That(csharpSaga).IsNotEmpty()
            .Because("the C#-authored twin must generate a saga to compare against.");
        await Assert.That(importedSaga).IsNotEmpty()
            .Because("the JSON import must lower a saga through the shared emitter.");
        await Assert.That(importedSaga).IsEqualTo(csharpSaga)
            .Because("the JSON import must lower to the BYTE-IDENTICAL saga as its C# twin (one lowering path, INV-1).");

        foreach (var artifact in LoweredArtifacts)
        {
            var csharpArtifact = GetGenerated(csharp, artifact);
            var importedArtifact = GetGenerated(imported, artifact);
            await Assert.That(importedArtifact).IsEqualTo(csharpArtifact)
                .Because($"the JSON import's {artifact} must be byte-identical to the C# twin's.");
        }
    }

    private static GeneratorDriverRunResult RunGenerator(string source, params (string Path, string Content)[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "GuardTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = additionalTexts
            .Select(t => (AdditionalText)new InMemoryAdditionalText(t.Path, t.Content))
            .ToArray();

        var driver = CSharpGeneratorDriver.Create(
            generators: [new WorkflowIncrementalGenerator().AsSourceGenerator()],
            additionalTexts: texts,
            parseOptions: null,
            optionsProvider: null);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    /// <summary>Collapses runs of consecutive blank lines to a single blank line.</summary>
    private static string NormalizeBlankRuns(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        var prevBlank = false;
        foreach (var line in lines)
        {
            var blank = string.IsNullOrWhiteSpace(line);
            if (blank && prevBlank)
            {
                continue;
            }

            sb.Append(line).Append('\n');
            prevBlank = blank;
        }

        return sb.ToString();
    }

    private static string GetGenerated(GeneratorDriverRunResult result, string hintSuffix) =>
        result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith(hintSuffix, StringComparison.Ordinal))
            ?.GetText()
            .ToString() ?? string.Empty;

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

        var abstractions = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(abstractions.Location))
        {
            references.Add(MetadataReference.CreateFromFile(abstractions.Location));
        }

        return references;
    }

    /// <summary>An in-memory <see cref="AdditionalText"/> for driving the generator over synthetic import files.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText text;

        public InMemoryAdditionalText(string path, string content)
        {
            this.Path = path;
            this.text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }
}
