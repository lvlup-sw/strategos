// -----------------------------------------------------------------------
// <copyright file="DerivedCompensationRuntimeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using System.Runtime.Loader;

using Strategos.Generators.Emitters;
using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging.Abstractions;

using TUnit.Assertions.Enums;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Source-shape tests for the typed, topology-derived compensation runtime.
/// </summary>
[Property("Category", "Unit")]
public class DerivedCompensationRuntimeTests
{
    /// <summary>Typed compensation activates the completed-prefix journal.</summary>
    [Test]
    public async Task Emit_TypedLinearProgram_JournalsAndReversesCompletedPrefix()
    {
        var model = CreateLinearModel();

        var saga = SagaEmitter.Emit(model);
        var commands = CommandsEmitter.Emit(model);
        var events = EventsEmitter.Emit(model);
        var handlers = WorkerHandlerEmitter.Emit(model);

        await Assert.That(saga).Contains("public List<CompensationJournalEntry> CompensationJournal");
        await Assert.That(saga).Contains("public List<ForwardDispatchClaim> ForwardDispatchClaims");
        await Assert.That(saga).Contains("TryRecordForwardDispatch(");
        await Assert.That(saga).Contains("CompensationJournalSchemaVersion = 1");
        await Assert.That(saga).Contains("if (CompensationJournalSchemaVersion != 1)");
        await Assert.That(saga).Contains("RollbackId = CreateCompensationRollbackId(forwardExecutionId)");
        await Assert.That(saga).Contains(".OrderByDescending(entry => entry.Sequence)");
        await Assert.That(saga).Contains("\"C\",\n            null,\n            null,\n            null,");
        await Assert.That(saga).Contains("Selected rollback scope contains a completed non-compensable occurrence");
        await Assert.That(saga).Contains("\"root/step:C#2\" => 2");
        await Assert.That(saga).Contains("Completion journal is missing history for a typed rollback program");
        await Assert.That(saga).Contains("HasCompleteCompensationJournalThrough(journalHighWater)");
        await Assert.That(saga).Contains("HasCompleteCompensationJournalThrough(CompensationJournalSequence)");
        await Assert.That(saga).Contains("(long)sequences.Count != highWater");
        await Assert.That(commands).Contains("CompensationJournalSequenceAtDispatch");
        await Assert.That(handlers).Contains(
            "CompensationJournalSequenceAtDispatch = cmd.CompensationJournalSequenceAtDispatch");
        await Assert.That(saga).DoesNotContain("CreateStableLegacyRollbackId");
        await Assert.That(CountOccurrences(saga, "if (string.IsNullOrEmpty(cmd.ForwardOccurrenceKey)")).IsEqualTo(1);
        await Assert.That(saga).DoesNotContain(
            "CompensationForkPathIndex = entry.ForkPathIndex,\n"
            + "                CompensationForkPathIndex = entry.ForkPathIndex,");
        await Assert.That(saga).Contains("ProcessOrderUndoBRollbackCompleted evt");
        await Assert.That(events).Contains("record ProcessOrderUndoBRollbackCompleted");
        await Assert.That(handlers).Contains("cmd.IsCompensation");
        await Assert.That(handlers).Contains("ProcessOrderRollbackFailed");
        await Assert.That(handlers).Contains("CorrelationId = (command.RollbackId ?? command.StepExecutionId)");
    }

    /// <summary>Configured inverse deadlines survive lowering into every durable timeout boundary.</summary>
    [Test]
    public async Task Emit_TypedProgram_PropagatesConfiguredAndDefaultInverseTimeoutsExactly()
    {
        var configuredTimeout = TimeSpan.FromSeconds(17);
        var model = CreateLinearModel(configuredTimeout);

        var saga = SagaEmitter.Emit(model);

        await Assert.That(saga).Contains(
            "\"root/step:A#0\" =>\n"
            + "                MatchesCompensationScopeTemplate(entry.ScopeKey, \"root\")\n"
            + "                && string.Equals(entry.ScopeKind, \"Root\", StringComparison.Ordinal)\n"
            + "                && entry.ScopeOrdinal == 0");
        await Assert.That(saga).Contains(
            $"entry.InverseTimeoutTicks == {configuredTimeout.Ticks}L")
            .Because("the topology validator must bind A to its configured inverse deadline");
        await Assert.That(saga).Contains(
            $"entry.InverseTimeoutTicks == {SagaCompensationComponentEmitter.DefaultTimeoutTicks}L")
            .Because("an inverse without a configured deadline must retain the documented default");
        await Assert.That(saga).Contains(
            "yield return new CompensationRollbackTimeout(WorkflowId, entry.RollbackId, entry.Sequence, entry.InverseTimeoutTicks);");
        await Assert.That(saga).Contains("if (timeout.TimeoutTicks != entry.InverseTimeoutTicks");
        await Assert.That(saga).Contains("|| entry.InverseTimeoutTicks <= 0");
    }

    /// <summary>A failed occurrence is scope metadata, never a fabricated completion.</summary>
    [Test]
    public async Task Emit_TypedLinearFailure_DoesNotSeedOrCompensateFailingOccurrence()
    {
        var saga = SagaEmitter.Emit(CreateLinearModel());
        var plannerStart = saga.IndexOf("private IEnumerable<object> DispatchCompensationEntry", StringComparison.Ordinal);
        var plannerEnd = saga.IndexOf("ProcessOrderUndoARollbackCompleted evt", plannerStart, StringComparison.Ordinal);
        var planner = saga.Substring(plannerStart, plannerEnd - plannerStart);

        await Assert.That(saga).DoesNotContain("Compatibility for manually published pre-#169 triggers");
        await Assert.That(saga).DoesNotContain("legacyRollbackId");
        await Assert.That(planner).Contains("\"UndoB\" => new ExecuteUndoBWorkerCommand");
        await Assert.That(planner).Contains("\"UndoA\" => new ExecuteUndoAWorkerCommand");
        await Assert.That(planner).DoesNotContain("UndoC");
    }

    /// <summary>Malformed inverse metadata cannot execute user code, recurse, or emit forward completion.</summary>
    [Test]
    public async Task Emit_TypedInverseWithMalformedMetadata_UsesRollbackFailureRouteOnly()
    {
        var handlers = WorkerHandlerEmitter.Emit(CreateLinearModel());

        await Assert.That(handlers).Contains("(cmd, ex, bus) => cmd.IsCompensation");
        await Assert.That(handlers).DoesNotContain("cmd.IsCompensation\n            && cmd.RollbackId");
        await Assert.That(handlers).Contains("cmd.RollbackId ?? cmd.StepExecutionId");
        await Assert.That(handlers).Contains("cmd.RollbackJournalSequence ?? -1L");
        await Assert.That(handlers).Contains("Reject malformed inverse metadata before user code can perform effects");
        await Assert.That(handlers).Contains("Inverse command has missing or inconsistent durable rollback metadata.");
    }

    /// <summary>An inverse type reused by OnFailure keeps its regular inverse error route.</summary>
    [Test]
    public async Task Emit_InverseTypeAlsoUsedByOnFailure_RoutesInverseErrorsWithoutRecursion()
    {
        var recoveryStep = StepModel.Create("UndoA", "TestNamespace.UndoA");
        var failureHandler = FailureHandlerModel.Create(
            "recover",
            FailureHandlerScope.Workflow,
            ["UndoA"],
            isTerminal: true,
            steps: [recoveryStep]);
        var model = CreateLinearModel() with
        {
            FailureHandlers = [failureHandler],
        };
        var handlers = WorkerHandlerEmitter.Emit(model);
        var regularStart = handlers.IndexOf("public sealed partial class UndoAHandler(", StringComparison.Ordinal);
        var failureStart = handlers.IndexOf(
            "public sealed partial class FailureHandler_recover_UndoAHandler(",
            regularStart,
            StringComparison.Ordinal);
        var regularHandler = handlers.Substring(regularStart, failureStart - regularStart);
        var failureHandlerWorker = handlers.Substring(failureStart);

        await Assert.That(regularHandler).Contains("public static void Configure(HandlerChain chain)");
        await Assert.That(regularHandler).Contains("cmd.IsCompensation");
        await Assert.That(regularHandler).Contains("ProcessOrderRollbackFailed");
        await Assert.That(failureHandlerWorker).DoesNotContain("TriggerProcessOrderFailureHandlerCommand");
        await Assert.That(failureHandlerWorker).DoesNotContain("CompensatingAction");
    }

    /// <summary>Inverse failure and timeout retain the saga for reconciliation.</summary>
    [Test]
    public async Task Emit_TypedProgram_InverseFailureAndUnknownOutcomeRetainSaga()
    {
        var saga = SagaEmitter.Emit(CreateLinearModel());
        var failureStart = saga.IndexOf("ProcessOrderRollbackFailed evt", StringComparison.Ordinal);
        var timeoutStart = saga.IndexOf("CompensationRollbackTimeout timeout", StringComparison.Ordinal);
        var failureRegion = saga.Substring(failureStart, timeoutStart - failureStart);
        var timeoutRegion = saga.Substring(timeoutStart);

        await Assert.That(failureRegion).Contains("entry.Status = \"Failed\"");
        await Assert.That(failureRegion).Contains("if (entry is null)");
        await Assert.That(failureRegion).Contains("Unmatched rollback failure");
        await Assert.That(failureRegion).Contains("saga retained");
        await Assert.That(failureRegion).Contains("string.Equals(entry.Status, \"RolledBack\", StringComparison.Ordinal)");
        await Assert.That(failureRegion).Contains("Do not call MarkCompleted");
        await Assert.That(failureRegion).DoesNotContain("MarkCompleted();");
        await Assert.That(timeoutRegion).Contains("entry.Status = \"OutcomeUnknown\"");
        await Assert.That(timeoutRegion).Contains("CompensationOutcomeUnknown = true");
        await Assert.That(timeoutRegion).DoesNotContain("MarkCompleted();");
    }

    /// <summary>Fork rollback waits for quiescence and serializes full-state lane inverses.</summary>
    [Test]
    public async Task Emit_TypedForkProgram_QuiescesAndSerializesLaneHeads()
    {
        var model = CreateForkModel();
        var saga = SagaEmitter.Emit(model);

        await Assert.That(saga).Contains("waiting for path quiescence");
        await Assert.That(saga).Contains("GroupBy(entry => entry.ScopeKey");
        await Assert.That(saga).Contains("foreach (var entry in pending.Take(1))");
        await Assert.That(saga).Contains("generic workflow state has no sound merge");
        await Assert.That(saga).Contains("ExtractForkScopeKey");
        await Assert.That(saga).Contains("HasContiguousForkDescendantHistories");
        await Assert.That(saga).Contains("IsContiguousScopeHistory");
        await Assert.That(saga).Contains("expectedCompletedCount: null");
        await Assert.That(saga).Contains("if (string.Equals(PendingCompensationForkId, \"fan-out\"");
        await Assert.That(saga).Contains("yield break;");
        await Assert.That(saga).Contains("BeginCompensationScope(PendingCompensationScopeKey, logger)");
        await Assert.That(saga).Contains("CompensationJournalSequenceAtDispatch = CompensationJournalSequence");

        var guardStart = saga.IndexOf(
            "if (string.Equals(PendingCompensationForkId, \"fan-out\"",
            StringComparison.Ordinal);
        var successorStart = saga.IndexOf("yield return new JoinFork_fan_out_Command", guardStart, StringComparison.Ordinal);
        var guardRegion = saga.Substring(guardStart, successorStart - guardStart);
        await Assert.That(guardRegion).Contains("yield break;")
            .Because("an in-flight lane completion must quiesce without dispatching its forward join successor");
    }

    /// <summary>An approval reached by an in-flight fork lane is not requested after rollback starts.</summary>
    [Test]
    public async Task Emit_ForkLaneApprovalCompletion_QuiescesBeforeApprovalSuccessor()
    {
        var model = CreateForkModel();
        var left = model.Forks![0].Paths[0].Steps[0];
        var approval = ApprovalModel.Create("Review", "TestNamespace.Reviewer", "Left");
        var context = new HandlerContext(
            StepIndex: 0,
            IsLastStep: false,
            IsTerminalStep: false,
            NextStepName: null,
            StepModel: left,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: approval,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: true,
            ForkPathKey: PathRoutingKey.ForFork("fan-out", 0, "Left"));
        var source = new System.Text.StringBuilder();

        new StepCompletedHandlerEmitter().EmitHandler(source, model, "Left", context);
        var emitted = source.ToString();
        var journalIndex = emitted.IndexOf("RecordCompensationCompletion(", StringComparison.Ordinal);
        var quiescenceIndex = emitted.IndexOf("PendingCompensationForkId", StringComparison.Ordinal);
        var approvalIndex = emitted.IndexOf("Requesting approval", StringComparison.Ordinal);

        await Assert.That(journalIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(quiescenceIndex).IsGreaterThan(journalIndex);
        await Assert.That(approvalIndex).IsGreaterThan(quiescenceIndex);
        await Assert.That(emitted).Contains("yield break;");
    }

    /// <summary>A completed fork lane cannot escape quiescence through low-confidence routing.</summary>
    [Test]
    public async Task Emit_ForkLaneConfidenceCompletion_QuiescesBeforeAlternateSuccessor()
    {
        var alternate = StepModel.Create("Review", "TestNamespace.Review");
        var left = TypedStep("Left", "UndoLeft", "left", "undo-left") with
        {
            Confidence = new ConfidenceModel(
                0.8,
                OnLowConfidenceHandlerStep: alternate,
                OnLowConfidenceHandlerChain: new LowConfidenceHandlerChainModel([alternate])),
        };
        var right = TypedStep("Right", "UndoRight", "right", "undo-right");
        var leftPath = ForkPathModel.Create(0, [left], false, false);
        var fork = ForkModel.Create(
            "fan-out",
            "Start",
            [leftPath, ForkPathModel.Create(1, [right], false, false)],
            "Join");
        var model = WorkflowModel.Create(
            "fork-confidence",
            "ForkConfidence",
            "TestNamespace",
            ["Start", "Left", "Right", "Join", "Finish", "Review"],
            "TestState",
            steps:
            [
                StepModel.Create("Start", "TestNamespace.Start"),
                left,
                right,
                StepModel.Create("Join", "TestNamespace.Join"),
                StepModel.Create("Finish", "TestNamespace.Finish"),
                StepModel.Create("UndoLeft", "TestNamespace.UndoLeft"),
                StepModel.Create("UndoRight", "TestNamespace.UndoRight"),
                alternate,
            ],
            forks: [fork],
            confidenceHandlerStepNames: ["Review"]);
        var source = new System.Text.StringBuilder();

        new ForkJoinHandlerEmitter().EmitPathCompletedHandler(source, model, "Left", fork, leftPath);
        var emitted = source.ToString();
        var journalIndex = emitted.IndexOf("RecordCompensationCompletion(", StringComparison.Ordinal);
        var quiescenceIndex = emitted.IndexOf("PendingCompensationForkId", StringComparison.Ordinal);
        var confidenceIndex = emitted.IndexOf("if (evt.Confidence", StringComparison.Ordinal);

        await Assert.That(journalIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(quiescenceIndex).IsGreaterThan(journalIndex);
        await Assert.That(confidenceIndex).IsGreaterThan(quiescenceIndex);
    }

    /// <summary>The authoritative generator fixture compiles with explicit LINQ use.</summary>
    [Test]
    public async Task Emit_TypedProgram_FullGeneratedOutputCompiles()
    {
        var source = CreateTypedCompilationWorkflow();
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "FulfillOrderSaga.g.cs");
        var nullableWarnings = GeneratorTestHelper.GetCompilationDiagnostics(source)
            .Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Warning
                && diagnostic.Id.StartsWith("CS86", StringComparison.Ordinal))
            .ToArray();

        await Assert.That(saga).Contains("using System.Linq;");
        await Assert.That(saga).Contains("CompensationJournalEntry");
        await Assert.That(saga).Contains("switch (forkId, pathIndex)");
        await Assert.That(saga).Contains("default:\n                break;");
        await Assert.That(saga).DoesNotContain("switch (forkId, pathIndex)\n        {\n        }");
        await Assert.That(nullableWarnings).IsEmpty();
    }

    /// <summary>Both persistence modes apply inverse results through their configured state path.</summary>
    [Test]
    public async Task Emit_RollbackCompletion_AppliesReducerOrEventStreamBeforeNextInverse()
    {
        var documentSaga = SagaEmitter.Emit(CreateLinearModel());
        var eventSourcedSaga = SagaEmitter.Emit(CreateLinearModel() with
        {
            PersistenceMode = PersistenceMode.EventSourced,
        });
        var documentRegion = RollbackCompletionRegion(documentSaga);
        var eventSourcedRegion = RollbackCompletionRegion(eventSourcedSaga);

        await Assert.That(documentRegion).Contains("State = TestStateReducer.Reduce(State, evt.UpdatedState);");
        await Assert.That(documentRegion).Contains("entry.RollbackState = State;");
        await Assert.That(documentRegion).Contains("next.RollbackState = State;");
        await Assert.That(documentRegion).DoesNotContain("entry.RollbackState = evt.UpdatedState;");
        await Assert.That(eventSourcedRegion).Contains("IDocumentSession session,");
        await Assert.That(eventSourcedRegion).Contains("session.Events.Append(WorkflowId, evt);");
        await Assert.That(eventSourcedRegion).Contains("State = State.ApplyEvent(evt);");
        await Assert.That(eventSourcedRegion).Contains("next.RollbackState = State;");
    }

    /// <summary>Event-sourced typed rollback fails closed because replay cannot prove the consumer fold.</summary>
    [Test]
    public async Task Emit_EventSourcedTypedProgram_ReportsAgwf045()
    {
        var source = CreateTypedCompilationWorkflow().Replace(
            "[Workflow(\"fulfill-order\")]",
            "[Workflow(\"fulfill-order\", Persistence = PersistenceMode.EventSourced)]",
            StringComparison.Ordinal);
        var result = GeneratorTestHelper.RunRejectedTopologyWithValidInput(source, "AGWF045");
        var diagnostic = result.Diagnostics.Single(candidate => candidate.Id == "AGWF045");

        await Assert.That(diagnostic.GetMessage()).Contains("EventSourced persistence");
        await Assert.That(diagnostic.GetMessage()).Contains("Marten replay");
    }

    /// <summary>A dynamic-only typed overload is invalid, never legacy-compatible.</summary>
    [Test]
    public async Task Emit_DynamicOnlyCompensation_FailsClosedInDerivedRuntime()
    {
        var dynamicStep = StepModel.Create(
            "Charge",
            "TestNamespace.Charge",
            compensation: new CompensationModel(
                "TestNamespace.Refund",
                InverseActionResolution: WorkflowActionReferenceResolution.DynamicOrInvalid),
            action: new WorkflowActionReferenceModel("orders", "Order", "charge"));
        var model = WorkflowModel.Create(
            "dynamic",
            "Dynamic",
            "TestNamespace",
            ["Charge", "Finish"],
            "TestState",
            steps:
            [
                dynamicStep,
                StepModel.Create("Finish", "TestNamespace.Finish"),
                StepModel.Create("Refund", "TestNamespace.Refund"),
            ]);

        var saga = SagaEmitter.Emit(model);

        await Assert.That(CompensationTopology.GetProgramKind(model)).IsEqualTo(CompensationProgramKind.Mixed);
        await Assert.That(saga).Contains("CompensationJournalEntry");
        await Assert.That(saga).Contains("mixed, dynamic, or unresolved");
        await Assert.That(saga).DoesNotContain("new ExecuteRefundWorkerCommand(WorkflowId, Guid.NewGuid(), State)");
    }

    /// <summary>
    /// Either possible representative of a collapsed typed/legacy conflict remains
    /// mixed and selects the fail-closed derived runtime.
    /// </summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Emit_CollapsedTypedLegacyConflict_IsMixedInBothOrders(
        bool typedRepresentative)
    {
        var compensation = typedRepresentative
            ? new CompensationModel(
                "TestNamespace.Refund",
                InverseAction: new WorkflowActionReferenceModel(
                    "orders",
                    "Order",
                    "refund"),
                InverseActionResolution: WorkflowActionReferenceResolution.Resolved)
            : new CompensationModel("TestNamespace.Refund");
        compensation = compensation with
        {
            HasConflictingDeclarations = true,
            HasTypedOrDynamicDeclaration = true,
        };
        var model = WorkflowModel.Create(
            "collapsed-mixed",
            "CollapsedMixed",
            "TestNamespace",
            ["Charge", "Finish"],
            "TestState",
            steps:
            [
                StepModel.Create(
                    "Charge",
                    "TestNamespace.Charge",
                    compensation: compensation,
                    action: new WorkflowActionReferenceModel("orders", "Order", "charge")),
                StepModel.Create("Finish", "TestNamespace.Finish"),
                StepModel.Create("Refund", "TestNamespace.Refund"),
            ]);

        var saga = SagaEmitter.Emit(model);

        await Assert.That(CompensationTopology.GetProgramKind(model))
            .IsEqualTo(CompensationProgramKind.Mixed);
        await Assert.That(CompensationTopology.UsesDerivedRuntime(model)).IsTrue();
        await Assert.That(saga).Contains("CompensationJournalEntry");
        await Assert.That(saga).Contains("mixed, dynamic, or unresolved");
        await Assert.That(saga).DoesNotContain(
            "new ExecuteRefundWorkerCommand(WorkflowId, Guid.NewGuid(), State)");
    }

    /// <summary>A statically proved empty-frame leaf journals an identity inverse.</summary>
    [Test]
    public async Task Emit_EmptyFrameIdentityLeaf_DoesNotBecomeMissingInverseFailure()
    {
        var identity = StepModel.Create(
            "Observe",
            "TestNamespace.Observe",
            action: new WorkflowActionReferenceModel("orders", "Order", "observe"));
        var model = WorkflowModel.Create(
            "identity",
            "Identity",
            "TestNamespace",
            ["A", "Observe", "C"],
            "TestState",
            steps:
            [
                TypedStep("A", "UndoA", "a", "undo-a"),
                identity,
                StepModel.Create("C", "TestNamespace.C"),
                StepModel.Create("UndoA", "TestNamespace.UndoA"),
            ]);

        var topology = CompensationTopology.Build(model);
        _ = topology.TryResolve("Observe", pathKey: null, out var occurrence);
        var saga = SagaEmitter.Emit(model);

        await Assert.That(occurrence.UsesIdentityInverse).IsTrue();
        await Assert.That(saga).Contains("UsesIdentityInverse = usesIdentityInverse");
        await Assert.That(saga).Contains("pending.Any(entry => !entry.UsesIdentityInverse");
        await Assert.That(saga).Contains("identityEntry.Status = \"RolledBack\"");
    }

    /// <summary>Outer rollback includes completed child entries; child failure excludes its parent.</summary>
    [Test]
    public async Task Emit_NestedLoopScope_UsesDescendantsOnlyWhenOuterScopeFails()
    {
        var model = CreateNestedScopeModel();
        var topology = CompensationTopology.Build(model);
        _ = topology.TryResolve("OuterC", pathKey: null, out var outerFailure);
        _ = topology.TryResolve("Inner_InnerB", pathKey: null, out var innerFailure);
        var innerEntries = topology.Occurrences
            .Where(occurrence => occurrence.Scope.Kind == CompensationScopeKind.LoopIteration)
            .OrderBy(occurrence => occurrence.Ordinal)
            .ToList();
        var saga = SagaEmitter.Emit(model);

        await Assert.That(outerFailure.Scope.TemplateKey).IsEqualTo("root");
        await Assert.That(innerFailure.Scope.TemplateKey).IsEqualTo("root/loop:Inner@{InnerIterationCount}");
        await Assert.That(innerEntries.Select(entry => entry.InverseStepName!).ToList())
            .IsEquivalentTo(["UndoInnerA", "UndoInnerB"]);
        await Assert.That(saga).Contains("IsWithinCompensationScope(entry.ScopeKey, scopeKey)");
        await Assert.That(saga).Contains("candidateScopeKey.StartsWith(selectedScopeKey + \"/\"");
        await Assert.That(saga).Contains(".OrderByDescending(entry => entry.Sequence)");
    }

    /// <summary>Deep loop identity uses parent links and rejects duplicate structural paths.</summary>
    [Test]
    public async Task BuildTopology_DeepLoopNamesDoNotUseUnderscoreDepthHeuristics()
    {
        var model = CreateDeepLoopModel(duplicateDeepLoop: false);
        var topology = CompensationTopology.Build(model);
        _ = topology.TryResolve(
            "Outer_With_Underscore_Inner_Part_Deep_Leaf",
            pathKey: null,
            out var occurrence);
        var duplicateTopology = CompensationTopology.Build(CreateDeepLoopModel(duplicateDeepLoop: true));

        await Assert.That(topology.IsClosed).IsTrue();
        await Assert.That(occurrence.Scope.Depth).IsEqualTo(3);
        await Assert.That(occurrence.Scope.TemplateKey).IsEqualTo(
            "root/loop:Outer_With_Underscore@{OuterWithUnderscoreIterationCount}"
            + "/loop:Inner_Part@{OuterWithUnderscoreInnerPartIterationCount}"
            + "/loop:Deep@{OuterWithUnderscoreInnerPartDeepIterationCount}");
        await Assert.That(duplicateTopology.IsClosed).IsFalse();
        await Assert.That(duplicateTopology.Issues.Any(issue => issue.Contains("identifies more than one loop", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A typed fork emits compilable topology-aware validation code.</summary>
    [Test]
    public async Task Emit_TypedForkProgram_FullGeneratedOutputCompiles()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(
            CreateTypedForkCompilationWorkflow(),
            "AGWF045");
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "ForkOrderSaga.g.cs");

        await Assert.That(saga).Contains("HasContiguousForkLaneHistory");
        await Assert.That(saga).Contains("HasContiguousForkDescendantHistories");
    }

    /// <summary>Every reducer-driven terminal shape mints authority before routing or completion.</summary>
    [Test]
    public async Task Emit_ReducerFailureShapes_MintExactPostCompletionCapabilityBeforeSuccessors()
    {
        var baseModel = CreateLinearModel() with
        {
            StateHasPhaseProperty = true,
        };
        var step = baseModel.Steps!.Single(candidate => candidate.StepName == "A");
        var emitter = new StepCompletedHandlerEmitter();

        var finalSource = new System.Text.StringBuilder();
        emitter.EmitHandler(
            finalSource,
            baseModel,
            "A",
            CreateHandlerContext(step, isLastStep: true));

        var approval = ApprovalModel.Create("Review", "TestNamespace.Reviewer", "A");
        var approvalSource = new System.Text.StringBuilder();
        emitter.EmitHandler(
            approvalSource,
            baseModel,
            "A",
            CreateHandlerContext(step, approval: approval));

        var alternate = StepModel.Create("Review", "TestNamespace.Review");
        var confidenceStep = step with
        {
            Confidence = new ConfidenceModel(
                0.8,
                "Review",
                alternate,
                new LowConfidenceHandlerChainModel([alternate])),
        };
        var confidenceModel = baseModel with
        {
            Steps = baseModel.Steps.Select(candidate =>
                candidate.StepName == "A" ? confidenceStep : candidate).Append(alternate).ToList(),
        };
        var confidenceSource = new System.Text.StringBuilder();
        emitter.EmitHandler(
            confidenceSource,
            confidenceModel,
            "A",
            CreateHandlerContext(confidenceStep, nextStepName: "B"));

        var branchCase = BranchCaseModel.Create("1", "Selected", ["B"], isTerminal: false);
        var branch = BranchModel.Create(
            "route",
            "A",
            "Stage",
            "int",
            isEnumDiscriminator: false,
            isMethodDiscriminator: false,
            cases: [branchCase]);
        var branchModel = baseModel with
        {
            Branches = [branch],
        };
        var branchSource = new System.Text.StringBuilder();
        new BranchHandlerEmitter().EmitRoutingHandler(branchSource, branchModel, "A", branch);

        var cases = new[]
        {
            (Source: finalSource.ToString(), Successor: "Phase = ProcessOrderPhase.Completed;"),
            (Source: approvalSource.ToString(), Successor: "yield return new RequestReviewApprovalEvent("),
            (Source: confidenceSource.ToString(), Successor: "if (evt.Confidence"),
            (Source: branchSource.ToString(), Successor: "yield return State.Stage switch"),
        };
        foreach (var item in cases)
        {
            var mint = item.Source.IndexOf("TryMintPostCompletionFailureClaim(", StringComparison.Ordinal);
            var exactIdentity = item.Source.IndexOf(
                "FailedForwardExecutionId = postCompletionFailureClaim.ForwardExecutionId",
                StringComparison.Ordinal);
            var successor = item.Source.IndexOf(item.Successor, StringComparison.Ordinal);

            await Assert.That(mint).IsGreaterThanOrEqualTo(0);
            await Assert.That(exactIdentity).IsGreaterThan(mint);
            await Assert.That(successor).IsGreaterThan(exactIdentity);
        }
    }

    /// <summary>Executing generated handlers rolls back B then A and folds inverse state.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_CFailureRollsBackCompletedPrefixInReverse()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());
        var sagaType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderSaga");
        var stateType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeState");
        var workflowId = Guid.NewGuid();
        var state0 = CreateState(stateType, workflowId, stage: 0, trace: "start");
        var startWorkflow = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.StartRuntimeOrderCommand"),
            workflowId,
            state0);
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var startA = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);

        var aWorker = Materialize(InvokeHandle(saga, startA, logger)).Single(message =>
            message.GetType().Name == "ExecuteAStepWorkerCommand");
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            workflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(saga, aCompleted, logger)).Single();
        var bWorker = Materialize(InvokeHandle(saga, startB, logger)).Single(message =>
            message.GetType().Name == "ExecuteBStepWorkerCommand");
        var bCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            workflowId,
            GetProperty<Guid>(bWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 2, trace: "A;B"),
            null,
            DateTimeOffset.UtcNow);
        var startC = Materialize(InvokeHandle(saga, bCompleted, logger)).Single();
        var cStartMessages = Materialize(InvokeHandle(saga, startC, logger));
        var cWorker = cStartMessages.Single(message => message.GetType().Name == "ExecuteCStepWorkerCommand");

        var trigger = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.TriggerRuntimeOrderFailureHandlerCommand"),
            workflowId,
            "CStep",
            "boom",
            "InvalidOperationException",
            null);
        CopyCompensationMetadata(cWorker, trigger);

        var firstRollbackMessages = Materialize(InvokeHandle(saga, trigger, logger));
        var undoB = firstRollbackMessages[0];
        var inverseOrder = new List<string> { undoB.GetType().Name };
        await Assert.That(undoB.GetType().Name).IsEqualTo("ExecuteUndoBStepWorkerCommand");
        await Assert.That(firstRollbackMessages.Any(message => message.GetType().Name.Contains("UndoC", StringComparison.Ordinal))).IsFalse();

        var bRollbackCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderUndoBStepRollbackCompleted"),
            workflowId,
            GetProperty<Guid>(undoB, "RollbackId"),
            GetProperty<long>(undoB, "RollbackJournalSequence"),
            CreateState(stateType, workflowId, stage: 1, trace: "B^-1"),
            DateTimeOffset.UtcNow);
        var secondRollbackMessages = Materialize(InvokeHandle(saga, bRollbackCompleted, logger));
        var undoA = secondRollbackMessages[0];
        inverseOrder.Add(undoA.GetType().Name);

        await Assert.That(inverseOrder).IsEquivalentTo(
            ["ExecuteUndoBStepWorkerCommand", "ExecuteUndoAStepWorkerCommand"],
            CollectionOrdering.Matching);
        await Assert.That(GetProperty<string>(GetProperty<object>(undoA, "State"), "Trace"))
            .IsEqualTo("B^-1")
            .Because("the reducer-applied result of B inverse must feed A inverse");

        var aRollbackCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderUndoAStepRollbackCompleted"),
            workflowId,
            GetProperty<Guid>(undoA, "RollbackId"),
            GetProperty<long>(undoA, "RollbackJournalSequence"),
            CreateState(stateType, workflowId, stage: 0, trace: "B^-1;A^-1"),
            DateTimeOffset.UtcNow);
        var terminalMessages = Materialize(InvokeHandle(saga, aRollbackCompleted, logger));
        var journal = Materialize(GetProperty<object>(saga, "CompensationJournal"));

        await Assert.That(terminalMessages).IsEmpty();
        await Assert.That(journal.Count).IsEqualTo(2);
        await Assert.That(journal.All(entry => GetProperty<string>(entry, "Status") == "RolledBack")).IsTrue();
        await Assert.That(GetProperty<string>(GetProperty<object>(saga, "State"), "Trace"))
            .IsEqualTo("B^-1;A^-1");
        await Assert.That(GetProperty<object>(saga, "ActiveCompensationScopeKey")).IsNull();
        await Assert.That(GetProperty<bool>(saga, "CompensationRollbackFinished")).IsTrue();

        var redeliveredTriggerMessages = Materialize(InvokeHandle(saga, trigger, logger));
        await Assert.That(redeliveredTriggerMessages).IsEmpty();
        await Assert.That(GetProperty<bool>(saga, "CompensationRollbackFinished")).IsTrue();
    }

    /// <summary>An inner branch failure unwinds only its completed branch prefix.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_InnerBranchFailureDoesNotUnwindOuterScope()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableNestedBranchWorkflow());
        var sagaType = RequiredType(assembly, "RuntimeNestedCompensationExecution.NestedOrderSaga");
        var stateType = RequiredType(assembly, "RuntimeNestedCompensationExecution.NestedState");
        var workflowId = Guid.NewGuid();
        var state0 = CreateState(stateType, workflowId, stage: 0, trace: "start");
        var startWorkflow = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.StartNestedOrderCommand"),
            workflowId,
            state0);
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var startOuterA = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);

        var outerAWorker = Materialize(InvokeHandle(saga, startOuterA, logger)).Single(message =>
            message.GetType().Name == "ExecuteOuterAStepWorkerCommand");
        var outerCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.OuterAStepCompleted"),
            workflowId,
            GetProperty<Guid>(outerAWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startInnerB = Materialize(InvokeHandle(saga, outerCompleted, logger)).Single();
        var innerBWorker = Materialize(InvokeHandle(saga, startInnerB, logger)).Single(message =>
            message.GetType().Name == "ExecuteInnerBStepWorkerCommand");
        var innerBCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.InnerBStepCompleted"),
            workflowId,
            GetProperty<Guid>(innerBWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 2, trace: "A;B"),
            null,
            DateTimeOffset.UtcNow);
        var startInnerC = Materialize(InvokeHandle(saga, innerBCompleted, logger)).Single();
        var innerCWorker = Materialize(InvokeHandle(saga, startInnerC, logger)).Single(message =>
            message.GetType().Name == "ExecuteInnerCStepWorkerCommand");
        var trigger = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.TriggerNestedOrderFailureHandlerCommand"),
            workflowId,
            "InnerCStep",
            "boom",
            "InvalidOperationException",
            null);
        CopyCompensationMetadata(innerCWorker, trigger);

        var firstRollbackMessages = Materialize(InvokeHandle(saga, trigger, logger));
        var undoInnerB = firstRollbackMessages[0];

        await Assert.That(undoInnerB.GetType().Name).IsEqualTo("ExecuteUndoInnerBStepWorkerCommand");
        await Assert.That(firstRollbackMessages.Any(message =>
            message.GetType().Name.Contains("UndoOuterA", StringComparison.Ordinal)
            || message.GetType().Name.Contains("UndoInnerC", StringComparison.Ordinal))).IsFalse();

        var innerBRollbackCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.NestedOrderUndoInnerBStepRollbackCompleted"),
            workflowId,
            GetProperty<Guid>(undoInnerB, "RollbackId"),
            GetProperty<long>(undoInnerB, "RollbackJournalSequence"),
            CreateState(stateType, workflowId, stage: 1, trace: "A;B^-1"),
            DateTimeOffset.UtcNow);
        var terminalMessages = Materialize(InvokeHandle(saga, innerBRollbackCompleted, logger));
        var journal = Materialize(GetProperty<object>(saga, "CompensationJournal"));
        var outerEntry = journal.Single(entry =>
            GetProperty<string>(entry, "ForwardStepName") == "OuterAStep");
        var innerEntry = journal.Single(entry =>
            GetProperty<string>(entry, "ForwardStepName") == "InnerBStep");

        await Assert.That(terminalMessages).IsEmpty();
        await Assert.That(journal.Count).IsEqualTo(2);
        await Assert.That(GetProperty<string>(outerEntry, "Status")).IsEqualTo("Completed")
            .Because("the enclosing root prefix must remain untouched by an inner-scope failure");
        await Assert.That(GetProperty<string>(innerEntry, "Status")).IsEqualTo("RolledBack");
        await Assert.That(GetProperty<string>(outerEntry, "ScopeKey")).IsEqualTo("root");
        await Assert.That(GetProperty<string>(innerEntry, "ScopeKey")).StartsWith("root/branch:");
        await Assert.That(GetProperty<object>(saga, "ActiveCompensationScopeKey")).IsNull();
    }

    /// <summary>A terminal approval rejection rolls back its branch prefix, not the enclosing root.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_BranchApprovalRejectionPreservesEnclosingScope()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableNestedBranchApprovalWorkflow());
        var sagaType = RequiredType(assembly, "RuntimeNestedCompensationExecution.NestedOrderSaga");
        var stateType = RequiredType(assembly, "RuntimeNestedCompensationExecution.NestedState");
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.StartNestedOrderCommand"),
            workflowId,
            CreateState(stateType, workflowId, stage: 0, trace: "start"));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var startOuterA = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);

        var outerAWorker = Materialize(InvokeHandle(saga, startOuterA, logger)).Single(message =>
            message.GetType().Name == "ExecuteOuterAStepWorkerCommand");
        var outerCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.OuterAStepCompleted"),
            workflowId,
            GetProperty<Guid>(outerAWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startInnerB = Materialize(InvokeHandle(saga, outerCompleted, logger)).Single();
        var innerBWorker = Materialize(InvokeHandle(saga, startInnerB, logger)).Single(message =>
            message.GetType().Name == "ExecuteInnerBStepWorkerCommand");
        var innerBCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.InnerBStepCompleted"),
            workflowId,
            GetProperty<Guid>(innerBWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 2, trace: "A;B"),
            null,
            DateTimeOffset.UtcNow);
        var approvalRequest = Materialize(InvokeHandle(saga, innerBCompleted, logger)).Single();
        var resumeType = RequiredType(
            assembly,
            "RuntimeNestedCompensationExecution.ResumeReviewerApprovalCommand");
        var decisionType = resumeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single().GetParameters()[1].ParameterType;
        var resume = CreateMessage(
            resumeType,
            workflowId,
            Enum.Parse(decisionType, "Rejected"),
            null,
            null);

        var trigger = InvokeHandle(saga, resume)!;
        var pendingClaim = Materialize(GetProperty<object>(
            saga,
            "PendingPostCompletionFailureClaims")).Single();
        var rollbackMessages = Materialize(InvokeHandle(saga, trigger, logger));
        var journal = Materialize(GetProperty<object>(saga, "CompensationJournal"));
        var outerEntry = journal.Single(entry =>
            GetProperty<string>(entry, "ForwardStepName") == "OuterAStep");
        var innerEntry = journal.Single(entry =>
            GetProperty<string>(entry, "ForwardStepName") == "InnerBStep");

        await Assert.That(approvalRequest.GetType().Name).IsEqualTo("RequestReviewerApprovalEvent");
        await Assert.That(GetProperty<string>(trigger, "FailedStepName")).IsEqualTo("InnerBStep");
        await Assert.That(GetProperty<string>(trigger, "ForwardOccurrenceKey")).StartsWith("root/branch:");
        await Assert.That(GetProperty<string>(trigger, "CompensationScopeKey")).StartsWith("root/branch:");
        await Assert.That(GetProperty<string>(trigger, "CompensationScopeKind")).IsEqualTo("BranchPath");
        await Assert.That(GetProperty<bool>(trigger, "FailureOccurredAfterForwardCompletion")).IsTrue();
        await Assert.That(GetProperty<Guid?>(trigger, "FailedForwardExecutionId"))
            .IsEqualTo(GetProperty<Guid>(pendingClaim, "ForwardExecutionId"));
        await Assert.That(GetProperty<string>(pendingClaim, "FailureKind")).IsEqualTo("ApprovalRejected");
        await Assert.That(rollbackMessages.Any(message =>
            message.GetType().Name == "ExecuteUndoInnerBStepWorkerCommand")).IsTrue();
        await Assert.That(rollbackMessages.Any(message =>
            message.GetType().Name == "ExecuteUndoOuterAStepWorkerCommand")).IsFalse();
        await Assert.That(GetProperty<string>(outerEntry, "Status")).IsEqualTo("Completed");
        await Assert.That(GetProperty<string>(innerEntry, "Status")).IsEqualTo("InProgress");
        await Assert.That(GetProperty<string>(saga, "ActiveCompensationScopeKey")).StartsWith("root/branch:");
    }

    /// <summary>A trigger cannot forge a broader scope than its compiled failed occurrence owns.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_RejectsTriggerMetadataThatContradictsTopology()
    {
        var fixture = CreateLinearFailureFixture();
        SetProperty(fixture.Trigger, "CompensationScopeKey", "root/branch:forged");

        var messages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(messages).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .Contains("contradicts the compiled compensation topology");
        await Assert.That(journal.All(entry => GetProperty<string>(entry, "Status") == "Completed")).IsTrue();
    }

    /// <summary>Persisted journal metadata is data, not authority over inverse dispatch.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_RejectsJournalThatContradictsTopology()
    {
        var fixture = CreateLinearFailureFixture();
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));
        var bEntry = journal.Single(entry => GetProperty<string>(entry, "ForwardStepName") == "BStep");
        SetProperty(bEntry, "InverseStepName", "UndoAStep");

        var messages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));

        await Assert.That(messages).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .Contains("changed or became corrupt");
        await Assert.That(GetProperty<string>(bEntry, "Status")).IsEqualTo("Completed");
    }

    /// <summary>An inverse failure is terminal even when the original trigger is redelivered.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_InverseFailureRemainsTerminalUnderTriggerRedelivery()
    {
        var fixture = CreateLinearFailureFixture();
        var rollbackMessages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var undoB = rollbackMessages[0];
        var failure = CreateMessage(
            RequiredType(fixture.Assembly, "RuntimeCompensationExecution.RuntimeOrderRollbackFailed"),
            fixture.WorkflowId,
            GetProperty<Guid>(undoB, "RollbackId"),
            GetProperty<long>(undoB, "RollbackJournalSequence"),
            "InvalidOperationException",
            "inverse failed",
            null,
            DateTimeOffset.UtcNow);

        _ = InvokeHandle(fixture.Saga, failure, fixture.Logger);
        var redeliveryMessages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));
        var bEntry = journal.Single(entry => GetProperty<string>(entry, "ForwardStepName") == "BStep");

        await Assert.That(redeliveryMessages).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(bEntry, "Status")).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .IsEqualTo("inverse failed");
    }

    /// <summary>An uncorrelated success cannot mutate state or let rollback continue.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_UnmatchedRollbackCompletionFailsClosedImmediately()
    {
        var fixture = CreateLinearFailureFixture();
        var rollbackMessages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var undoB = rollbackMessages[0];
        var unmatched = CreateMessage(
            RequiredType(
                fixture.Assembly,
                "RuntimeCompensationExecution.RuntimeOrderUndoBStepRollbackCompleted"),
            fixture.WorkflowId,
            Guid.NewGuid(),
            GetProperty<long>(undoB, "RollbackJournalSequence"),
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 999, trace: "forged"),
            DateTimeOffset.UtcNow);

        var unmatchedMessages = Materialize(InvokeHandle(fixture.Saga, unmatched, fixture.Logger));
        var correct = CreateMessage(
            RequiredType(
                fixture.Assembly,
                "RuntimeCompensationExecution.RuntimeOrderUndoBStepRollbackCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(undoB, "RollbackId"),
            GetProperty<long>(undoB, "RollbackJournalSequence"),
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 1, trace: "B^-1"),
            DateTimeOffset.UtcNow);
        var lateCorrectMessages = Materialize(InvokeHandle(fixture.Saga, correct, fixture.Logger));

        await Assert.That(unmatchedMessages).IsEmpty();
        await Assert.That(lateCorrectMessages).IsEmpty();
        await Assert.That(GetProperty<bool>(fixture.Saga, "CompensationOutcomeUnknown")).IsTrue();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(GetProperty<object>(fixture.Saga, "State"), "Trace"))
            .IsEqualTo("A;B");
    }

    /// <summary>Loop scope instances accept only canonical nonnegative Int32 counter tokens.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ScopeTemplateRejectsNoncanonicalOrOverflowedCounters()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());
        var sagaType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderSaga");
        var matcher = sagaType.GetMethod(
            "MatchesCompensationScopeTemplate",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        const string template = "root/loop:Inner@{InnerIterationCount}";

        var inclusiveBounds = new[] { int.MaxValue };
        var zero = (bool)matcher.Invoke(null, ["root/loop:Inner@0", template, inclusiveBounds])!;
        var max = (bool)matcher.Invoke(null, ["root/loop:Inner@2147483647", template, inclusiveBounds])!;
        var leadingZero = (bool)matcher.Invoke(null, ["root/loop:Inner@01", template, inclusiveBounds])!;
        var overflow = (bool)matcher.Invoke(null, ["root/loop:Inner@2147483648", template, inclusiveBounds])!;

        await Assert.That(zero).IsTrue();
        await Assert.That(max).IsTrue();
        await Assert.That(leadingZero).IsFalse();
        await Assert.That(overflow).IsFalse();
    }

    /// <summary>A forward deadline is a rollback ingress, and its losing completion cannot resurrect state.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ForwardTimeoutRollsBackAndRejectsLateCompletion()
    {
        var fixture = CreateLinearFailureFixture();

        var timeoutMessages = Materialize(InvokeHandle(fixture.Saga, fixture.Timeout, fixture.Logger));
        var undoB = timeoutMessages.Single(message => message.GetType().Name == "ExecuteUndoBStepWorkerCommand");
        var lateCompletion = CreateMessage(
            RequiredType(fixture.Assembly, "RuntimeCompensationExecution.CStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(fixture.FailedWorker, "StepExecutionId"),
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 999, trace: "late C"),
            null,
            DateTimeOffset.UtcNow);

        _ = InvokeHandle(fixture.Saga, lateCompletion, fixture.Logger);
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Compensating");
        await Assert.That(GetProperty<string>(fixture.Saga, "FailedCompensationOccurrenceKey"))
            .IsEqualTo("root/step:CStep#2");
        await Assert.That(GetProperty<string>(GetProperty<object>(fixture.Saga, "State"), "Trace"))
            .IsEqualTo("A;B");
        await Assert.That(journal.Count).IsEqualTo(2);
        await Assert.That(GetProperty<Guid>(undoB, "RollbackId")).IsNotEqualTo(Guid.Empty);
    }

    /// <summary>A redelivered start command cannot escape an active typed rollback.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_StaleForwardStartDoesNotDispatchDuringRollback()
    {
        var fixture = CreateLinearFailureFixture();
        var rollbackMessages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var staleStart = CreateMessage(
            RequiredType(fixture.Assembly, "RuntimeCompensationExecution.StartCStepCommand"),
            fixture.WorkflowId);

        var staleMessages = Materialize(InvokeHandle(fixture.Saga, staleStart, fixture.Logger));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(rollbackMessages.Single(message =>
            message.GetType().Name == "ExecuteUndoBStepWorkerCommand").GetType().Name)
            .IsEqualTo("ExecuteUndoBStepWorkerCommand");
        await Assert.That(staleMessages).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Compensating");
        await Assert.That(GetProperty<string>(GetProperty<object>(fixture.Saga, "State"), "Trace"))
            .IsEqualTo("A;B");
        await Assert.That(journal).HasCount().EqualTo(2);
        await Assert.That(journal.Count(entry => GetProperty<string>(entry, "Status") == "InProgress"))
            .IsEqualTo(1);
    }

    /// <summary>A validation guard preserves its audit event and rolls back the completed prefix.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ValidationFailureEntersDerivedRollback()
    {
        var fixture = CreateLinearPrefixFixture(CreateExecutableTypedValidationWorkflow());

        var validationMessages = Materialize(InvokeHandle(fixture.Saga, fixture.NextAfterB, fixture.Logger));
        var auditEvent = validationMessages.Single(message =>
            message.GetType().Name == "RuntimeOrderValidationFailed");
        var trigger = validationMessages.Single(message =>
            message.GetType().Name == "TriggerRuntimeOrderFailureHandlerCommand");
        var rollbackMessages = Materialize(InvokeHandle(fixture.Saga, trigger, fixture.Logger));

        await Assert.That(GetProperty<string>(auditEvent, "StepName")).IsEqualTo("CStep");
        await Assert.That(GetProperty<string>(trigger, "FailedStepName")).IsEqualTo("CStep");
        await Assert.That(GetProperty<string>(trigger, "ForwardOccurrenceKey"))
            .IsEqualTo("root/step:CStep#2");
        await Assert.That(GetProperty<string>(trigger, "CompensationScopeKey")).IsEqualTo("root");
        await Assert.That(GetProperty<long>(trigger, "CompensationJournalSequenceAtDispatch"))
            .IsEqualTo(2L);
        await Assert.That(GetProperty<bool>(trigger, "FailureOccurredAfterForwardCompletion")).IsFalse();
        await Assert.That(GetProperty<Guid?>(trigger, "FailedForwardExecutionId"))
            .IsNotNull()
            .And.IsNotEqualTo(Guid.Empty);
        await Assert.That(rollbackMessages.Single(message =>
            message.GetType().Name == "ExecuteUndoBStepWorkerCommand").GetType().Name)
            .IsEqualTo("ExecuteUndoBStepWorkerCommand");
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Compensating");
    }

    /// <summary>A fork-lane timeout is correlated by lane identity, never the saga-wide phase.</summary>
    [Test]
    public async Task Emit_TypedForkTimeout_DoesNotUseGlobalPhaseAsRaceAuthority()
    {
        var saga = SagaEmitter.Emit(CreateForkModel(leftTimeout: true));
        var handlerStart = saga.IndexOf("Handle(\n        LeftTimeout t,", StringComparison.Ordinal);
        var handlerEnd = saga.IndexOf("private bool HasCompletedLeftAfterTimeoutDispatch", handlerStart, StringComparison.Ordinal);
        var handler = saga.Substring(handlerStart, handlerEnd - handlerStart);

        await Assert.That(handlerStart).IsGreaterThanOrEqualTo(0);
        await Assert.That(handlerEnd).IsGreaterThan(handlerStart);
        await Assert.That(handler).DoesNotContain("Phase !=")
            .Because("a sibling fork lane may legitimately change the saga-wide phase");
        await Assert.That(handler).Contains("HasCompletedLeftAfterTimeoutDispatch(t)");
        await Assert.That(handler).Contains("MatchesCurrentLeftTimeoutTopology(t)");
        await Assert.That(saga).Contains("ResolveLeftTimeoutStepName(t.ForwardOccurrenceKey)");
        await Assert.That(saga).Contains("CompensationLaneKey = \"path:0\"");
        await Assert.That(saga).Contains("FailedForwardExecutionId = t.ForwardExecutionId");
    }

    /// <summary>A queued sibling start quiesces a pending fork without dispatching external work.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_DelayedForkStartQuiescesAndWakesRollback()
    {
        var fixture = CreateTypedForkRuntimeFixture();
        var leftWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.LeftStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteLeftStepWorkerCommand");
        var leftTrigger = CreateForkFailureTrigger(
            fixture.Assembly,
            fixture.WorkflowId,
            leftWorker,
            "LeftStep");

        _ = Materialize(InvokeHandle(fixture.Saga, leftTrigger, fixture.Logger));
        var failedLaneMessages = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.LeftStart,
            fixture.Logger));

        await Assert.That(failedLaneMessages).IsEmpty();
        await Assert.That(GetForkPathProperty(fixture.Saga, 0, "Status").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<object>(fixture.Saga, "PendingCompensationScopeKey")).IsNotNull();

        var delayedMessages = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.RightStart,
            fixture.Logger));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(delayedMessages.Any(message =>
            message.GetType().Name == "ExecuteRightStepWorkerCommand")).IsFalse();
        await Assert.That(GetForkPathProperty(fixture.Saga, 0, "Status").ToString()).IsEqualTo("Failed");
        await Assert.That(GetForkPathProperty(fixture.Saga, 1, "Status").ToString()).IsEqualTo("Success");
        await Assert.That((bool)GetForkPathProperty(fixture.Saga, 1, "CompensationQuiesced")).IsTrue();
        await Assert.That(journal).HasCount().EqualTo(1);
        await Assert.That(GetProperty<int>(GetProperty<object>(fixture.Saga, "State"), "Value"))
            .IsEqualTo(1);
        await Assert.That(GetProperty<object>(fixture.Saga, "PendingCompensationScopeKey")).IsNull();
        await Assert.That(GetProperty<bool>(fixture.Saga, "CompensationRollbackFinished")).IsTrue();
    }

    /// <summary>Two failed lanes cannot let a late completion from the first lane mutate state.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_TwoFailedForkLanesRejectFirstLaneLateCompletion()
    {
        var fixture = CreateTypedForkRuntimeFixture();
        var leftWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.LeftStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteLeftStepWorkerCommand");
        var rightWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.RightStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteRightStepWorkerCommand");
        var leftTrigger = CreateForkFailureTrigger(
            fixture.Assembly,
            fixture.WorkflowId,
            leftWorker,
            "LeftStep");
        var rightTrigger = CreateForkFailureTrigger(
            fixture.Assembly,
            fixture.WorkflowId,
            rightWorker,
            "RightStep");

        _ = Materialize(InvokeHandle(fixture.Saga, leftTrigger, fixture.Logger));
        _ = Materialize(InvokeHandle(fixture.Saga, rightTrigger, fixture.Logger));
        var lateLeft = CreateMessage(
            RequiredType(
                fixture.Assembly,
                "TypedForkCompensationCompilation.LeftStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(leftWorker, "StepExecutionId"),
            CreateForkState(fixture.StateType, fixture.WorkflowId, value: 999),
            null,
            DateTimeOffset.UtcNow);
        var lateMessages = Materialize(InvokeHandle(fixture.Saga, lateLeft, fixture.Logger));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(lateMessages).IsEmpty();
        await Assert.That(GetForkPathProperty(fixture.Saga, 0, "Status").ToString()).IsEqualTo("Failed");
        await Assert.That(GetForkPathProperty(fixture.Saga, 1, "Status").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<int>(GetProperty<object>(fixture.Saga, "State"), "Value"))
            .IsEqualTo(1);
        await Assert.That(journal).HasCount().EqualTo(1);
        await Assert.That(journal.Any(entry =>
            GetProperty<string>(entry, "ForwardStepName") == "LeftStep")).IsFalse();
    }

    /// <summary>A loop timeout must bind to the current concrete iteration scope.</summary>
    [Test]
    public async Task Emit_TypedLoopTimeout_RejectsAStaleIterationScope()
    {
        var model = CreateNestedScopeModel();
        var timedStep = model.Loops![0].BodySteps[1] with
        {
            Timeout = new TimeoutModel(TimeSpan.FromSeconds(5)),
        };
        var loop = model.Loops[0] with
        {
            BodySteps = [model.Loops[0].BodySteps[0], timedStep],
        };
        model = model with
        {
            Loops = [loop],
            Steps = model.Steps!
                .Select(step => step.StepName == timedStep.StepName ? timedStep : step)
                .ToList(),
        };

        var saga = SagaEmitter.Emit(model);

        await Assert.That(saga).Contains("MatchesCurrentInner_InnerBTimeoutTopology(t)");
        await Assert.That(saga).Contains(
            "string.Equals(timeout.CompensationScopeKey, ResolveCompensationScopeInstance(\"root/loop:Inner@{InnerIterationCount}\"), StringComparison.Ordinal)");
        await Assert.That(saga).DoesNotContain("if (Phase != NestedOrderPhase.Inner_InnerB)");
    }

    /// <summary>A direct terminal approval timeout uses the completed approval boundary.</summary>
    [Test]
    public async Task Emit_TypedTerminalApprovalTimeout_SeedsInclusiveRollbackBoundary()
    {
        var model = CreateLinearModel();
        var approval = ApprovalModel.Create(
            "Review",
            "TestNamespace.Reviewer",
            "B",
            isEscalationTerminal: true);
        var source = new System.Text.StringBuilder();

        new SagaApprovalHandlersEmitter().EmitTimeoutHandler(source, model, approval);
        var emitted = source.ToString();

        await Assert.That(emitted).Contains("new TriggerProcessOrderFailureHandlerCommand(");
        await Assert.That(emitted).Contains("            \"B\",");
        await Assert.That(emitted).Contains("ForwardOccurrenceKey = \"root/step:B#1\"");
        await Assert.That(emitted).Contains("CompensationScopeKey = ResolveCompensationScopeInstance(\"root\")");
        await Assert.That(emitted).Contains("FailureOccurredAfterForwardCompletion = true");
        await Assert.That(emitted).DoesNotContain("MarkCompleted();");
    }

    /// <summary>A typed diagnostic fork emits every field required by topology validation.</summary>
    [Test]
    public async Task Emit_TypedDiagnosticFork_SeedsTopologyCompleteRollbackTrigger()
    {
        var model = CreateLinearModel() with
        {
            DiagnosticForks =
            [
                DiagnosticForkModel.Create(
                    ["C"],
                    [PermittedForkTriggerModel.Create("OperatorExplicit", ["reason"])],
                    "B",
                    1),
            ],
        };

        var saga = SagaEmitter.Emit(model);

        await Assert.That(saga).Contains("ForwardOccurrenceKey = \"root/step:B#1\"");
        await Assert.That(saga).Contains("CompensationScopeKey = ResolveCompensationScopeInstance(\"root\")");
        await Assert.That(saga).Contains("CompensationScopeKind = \"Root\"");
        await Assert.That(saga).Contains("CompensationLaneKey = null");
        await Assert.That(saga).Contains("CompensationForkId = null");
        await Assert.That(saga).Contains("CompensationForkPathIndex = null");
        await Assert.That(saga).Contains("CompensationJournalSequenceAtDispatch = CompensationJournalSequence");
    }

    /// <summary>A reducer-driven failure rolls back its inclusive boundary before OnFailure starts.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ReducerFailureRollsBackBeforeStartingFailureHandler()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureWorkflow(),
            "AGWF045");
        var fixture = CreateLinearInitialFixture(assembly);
        var aWorker = DispatchFirstWorker(fixture.Saga, fixture.FirstStart, fixture.Logger);
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 1,
                trace: "A",
                phase: "AStep"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(fixture.Saga, aCompleted, fixture.Logger)).Single();
        var bWorker = Materialize(InvokeHandle(fixture.Saga, startB, fixture.Logger)).Single(message =>
            message.GetType().Name == "ExecuteBStepWorkerCommand");
        var bCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(bWorker, "StepExecutionId"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 2,
                trace: "A;B",
                phase: "Failed"),
            null,
            DateTimeOffset.UtcNow);

        var boundaryMessages = Materialize(InvokeHandle(fixture.Saga, bCompleted, fixture.Logger));
        var trigger = boundaryMessages.Single();
        var pendingClaim = Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims")).Single();

        await Assert.That(trigger.GetType().Name).IsEqualTo("TriggerRuntimeOrderFailureHandlerCommand");
        await Assert.That(boundaryMessages.Any(message => message.GetType().Name.StartsWith(
            "StartFailureHandler_",
            StringComparison.Ordinal))).IsFalse();
        await Assert.That(GetProperty<string>(trigger, "ForwardOccurrenceKey"))
            .IsEqualTo("root/step:BStep#1");
        await Assert.That(GetProperty<bool>(trigger, "FailureOccurredAfterForwardCompletion")).IsTrue();
        await Assert.That(GetProperty<Guid?>(trigger, "FailedForwardExecutionId"))
            .IsEqualTo(GetProperty<Guid>(pendingClaim, "ForwardExecutionId"));
        await Assert.That(GetProperty<long>(trigger, "CompensationJournalSequenceAtDispatch"))
            .IsEqualTo(GetProperty<long>(pendingClaim, "JournalSequenceAtDispatch"));
        await Assert.That(GetProperty<string>(pendingClaim, "FailureKind"))
            .IsEqualTo("StateTransitionFailure");

        var firstRollbackMessages = Materialize(InvokeHandle(fixture.Saga, trigger, fixture.Logger));
        var undoB = firstRollbackMessages.Single(message =>
            message.GetType().Name == "ExecuteUndoBStepWorkerCommand");
        await Assert.That(undoB.GetType().Name).IsEqualTo("ExecuteUndoBStepWorkerCommand");
        await Assert.That(firstRollbackMessages.Any(message => message.GetType().Name.StartsWith(
            "StartFailureHandler_",
            StringComparison.Ordinal))).IsFalse();

        var bRollbackCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderUndoBStepRollbackCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(undoB, "RollbackId"),
            GetProperty<long>(undoB, "RollbackJournalSequence"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 1,
                trace: "B^-1",
                phase: "Failed"),
            DateTimeOffset.UtcNow);
        var secondRollbackMessages = Materialize(InvokeHandle(
            fixture.Saga,
            bRollbackCompleted,
            fixture.Logger));
        var undoA = secondRollbackMessages.Single(message =>
            message.GetType().Name == "ExecuteUndoAStepWorkerCommand");
        await Assert.That(undoA.GetType().Name).IsEqualTo("ExecuteUndoAStepWorkerCommand");
        await Assert.That(secondRollbackMessages.Any(message => message.GetType().Name.StartsWith(
            "StartFailureHandler_",
            StringComparison.Ordinal))).IsFalse();

        var aRollbackCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderUndoAStepRollbackCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(undoA, "RollbackId"),
            GetProperty<long>(undoA, "RollbackJournalSequence"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 0,
                trace: "B^-1;A^-1",
                phase: "Failed"),
            DateTimeOffset.UtcNow);
        var terminalMessages = Materialize(InvokeHandle(
            fixture.Saga,
            aRollbackCompleted,
            fixture.Logger));
        var failureHandlerStarts = terminalMessages.Where(message => message.GetType().Name.StartsWith(
            "StartFailureHandler_",
            StringComparison.Ordinal)).ToList();
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(terminalMessages).HasCount().EqualTo(1);
        await Assert.That(failureHandlerStarts).HasCount().EqualTo(1);
        await Assert.That(failureHandlerStarts[0].GetType().Name).Contains("NotifyFailureStep");
        await Assert.That(journal.Select(entry => GetProperty<string>(entry, "ForwardStepName")).ToList())
            .IsEquivalentTo(["AStep", "BStep"], CollectionOrdering.Matching);
        await Assert.That(journal.All(entry => GetProperty<string>(entry, "Status") == "RolledBack")).IsTrue();
    }

    /// <summary>Reducer failure at a fork dispatch boundary cannot start any lane.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ForkDispatchReducerFailureEmitsOnlyAuthenticatedRollback()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureForkWorkflow(),
            "AGWF045");
        var sagaType = RequiredType(
            assembly,
            "TypedForkCompensationCompilation.ForkOrderSaga");
        var stateType = RequiredType(
            assembly,
            "TypedForkCompensationCompilation.ForkState");
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(
                assembly,
                "TypedForkCompensationCompilation.StartForkOrderCommand"),
            workflowId,
            CreateForkState(stateType, workflowId, value: 0));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var start = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);
        var worker = Materialize(InvokeHandle(saga, start, logger)).Single(message =>
            message.GetType().Name == "ExecuteStartStepWorkerCommand");
        var failedCompletion = CreateMessage(
            RequiredType(
                assembly,
                "TypedForkCompensationCompilation.StartStepCompleted"),
            workflowId,
            GetProperty<Guid>(worker, "StepExecutionId"),
            CreateForkState(stateType, workflowId, value: 1, reducerFailed: true),
            null,
            DateTimeOffset.UtcNow);

        var messages = Materialize(InvokeHandle(saga, failedCompletion, logger));

        await Assert.That(messages).HasCount().EqualTo(1);
        await Assert.That(messages.Single().GetType().Name)
            .IsEqualTo("TriggerForkOrderFailureHandlerCommand");
        await Assert.That(messages.Any(message => message.GetType().Name.StartsWith(
            "StartLeftStep",
            StringComparison.Ordinal))).IsFalse();
        await Assert.That(messages.Any(message => message.GetType().Name.StartsWith(
            "StartRightStep",
            StringComparison.Ordinal))).IsFalse();
        await Assert.That(GetForkPathProperty(saga, 0, "Status").ToString()).IsEqualTo("Pending");
        await Assert.That(GetForkPathProperty(saga, 1, "Status").ToString()).IsEqualTo("Pending");
        await Assert.That(Materialize(GetProperty<object>(
            saga,
            "PendingPostCompletionFailureClaims"))).HasCount().EqualTo(1);
    }

    /// <summary>Reducer failure at a fork-lane completion cannot mark success or dispatch Join.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ForkPathReducerFailureEmitsOnlyAuthenticatedRollback()
    {
        var fixture = CreateTypedForkRuntimeFixture(CreateExecutableReducerFailureForkWorkflow());
        var leftWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.LeftStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteLeftStepWorkerCommand");
        var failedCompletion = CreateMessage(
            RequiredType(
                fixture.Assembly,
                "TypedForkCompensationCompilation.LeftStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(leftWorker, "StepExecutionId"),
            CreateForkState(
                fixture.StateType,
                fixture.WorkflowId,
                value: 2,
                reducerFailed: true),
            null,
            DateTimeOffset.UtcNow);

        var messages = Materialize(InvokeHandle(
            fixture.Saga,
            failedCompletion,
            fixture.Logger));

        await Assert.That(messages).HasCount().EqualTo(1);
        await Assert.That(messages.Single().GetType().Name)
            .IsEqualTo("TriggerForkOrderFailureHandlerCommand");
        await Assert.That(messages.Any(message => message.GetType().Name.StartsWith(
            "JoinFork_",
            StringComparison.Ordinal))).IsFalse();
        await Assert.That(GetForkPathProperty(fixture.Saga, 0, "Status").ToString())
            .IsEqualTo("InProgress");
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims"))).HasCount().EqualTo(1);
    }

    /// <summary>
    /// Redelivering a completed prior loop iteration is an execution-id idempotent no-op even
    /// after the mutable loop counter has advanced to the next concrete scope instance.
    /// </summary>
    [Test]
    public async Task Execute_GeneratedSaga_PriorLoopIterationCompletionRedeliveryIsIdempotent()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureLoopWorkflow(),
            "AGWF045");
        var sagaType = RequiredType(
            assembly,
            "RuntimeLoopCompensationExecution.LoopOrderSaga");
        var stateType = RequiredType(
            assembly,
            "RuntimeLoopCompensationExecution.LoopState");
        _ = RequiredType(
            assembly,
            "RuntimeLoopCompensationExecution.LoopOrderWorkflowDefinition")
            .GetProperty("Definition", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null);
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeLoopCompensationExecution.StartLoopOrderCommand"),
            workflowId,
            CreateLoopState(stateType, workflowId, value: 0));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var entryStart = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);
        var entryWorker = Materialize(InvokeHandle(saga, entryStart, logger)).Single(message =>
            message.GetType().Name == "ExecuteEntryStepWorkerCommand");
        var entryCompletion = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeLoopCompensationExecution.EntryStepCompleted"),
            workflowId,
            GetProperty<Guid>(entryWorker, "StepExecutionId"),
            CreateLoopState(stateType, workflowId, value: 1),
            null,
            DateTimeOffset.UtcNow);
        var firstBodyStart = Materialize(InvokeHandle(saga, entryCompletion, logger)).Single(message =>
            message.GetType().Name.Contains("BodyStepCommand", StringComparison.Ordinal));
        var firstBodyWorker = Materialize(InvokeHandle(saga, firstBodyStart, logger)).Single(message =>
            message.GetType().Name.Contains("BodyStepWorkerCommand", StringComparison.Ordinal));
        var bodyCompletionType = RequiredType(
            assembly,
            "RuntimeLoopCompensationExecution.BodyStepCompleted");
        var firstBodyCompletion = CreateMessage(
            bodyCompletionType,
            workflowId,
            GetProperty<Guid>(firstBodyWorker, "StepExecutionId"),
            CreateLoopState(stateType, workflowId, value: 2),
            null,
            DateTimeOffset.UtcNow);
        var secondBodyStart = Materialize(InvokeHandle(saga, firstBodyCompletion, logger)).Single(message =>
            message.GetType().Name.Contains("BodyStepCommand", StringComparison.Ordinal));
        var secondBodyWorker = Materialize(InvokeHandle(saga, secondBodyStart, logger)).Single(message =>
            message.GetType().Name.Contains("BodyStepWorkerCommand", StringComparison.Ordinal));
        var stateBeforeRedelivery = GetProperty<object>(saga, "State");
        var phaseBeforeRedelivery = GetProperty<object>(saga, "Phase");
        var journalSequenceBeforeRedelivery = GetProperty<long>(saga, "CompensationJournalSequence");
        var journalCountBeforeRedelivery = Materialize(GetProperty<object>(
            saga,
            "CompensationJournal")).Count;
        var claimsBeforeRedelivery = Materialize(GetProperty<object>(saga, "ForwardDispatchClaims"));

        var messages = Materialize(InvokeHandle(saga, firstBodyCompletion, logger));
        var claimsAfterRedelivery = Materialize(GetProperty<object>(saga, "ForwardDispatchClaims"));

        await Assert.That(messages).IsEmpty();
        await Assert.That(ReferenceEquals(GetProperty<object>(saga, "State"), stateBeforeRedelivery)).IsTrue();
        await Assert.That(GetProperty<object>(saga, "Phase")).IsEqualTo(phaseBeforeRedelivery);
        await Assert.That(GetProperty<int>(saga, "RetryIterationCount")).IsEqualTo(1);
        await Assert.That(GetProperty<object>(saga, "CompensationFailureMessage")).IsNull();
        await Assert.That(GetProperty<object>(saga, "FailedStepName")).IsNull();
        await Assert.That(GetProperty<object>(saga, "FailureTimestamp")).IsNull();
        await Assert.That(GetProperty<long>(saga, "CompensationJournalSequence"))
            .IsEqualTo(journalSequenceBeforeRedelivery);
        await Assert.That(Materialize(GetProperty<object>(saga, "CompensationJournal")))
            .HasCount().EqualTo(journalCountBeforeRedelivery);
        await Assert.That(claimsAfterRedelivery).HasCount().EqualTo(claimsBeforeRedelivery.Count);
        await Assert.That(GetProperty<Guid>(claimsAfterRedelivery.Single(), "ForwardExecutionId"))
            .IsEqualTo(GetProperty<Guid>(secondBodyWorker, "StepExecutionId"));
    }

    /// <summary>Reducer failure at a loop completion cannot advance or exit the loop.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_LoopReducerFailureEmitsOnlyAuthenticatedRollback()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureLoopWorkflow(),
            "AGWF045");
        var sagaType = RequiredType(
            assembly,
            "RuntimeLoopCompensationExecution.LoopOrderSaga");
        var stateType = RequiredType(
            assembly,
            "RuntimeLoopCompensationExecution.LoopState");
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeLoopCompensationExecution.StartLoopOrderCommand"),
            workflowId,
            CreateLoopState(stateType, workflowId, value: 0));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var startEntry = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);
        var entryWorker = Materialize(InvokeHandle(saga, startEntry, logger)).Single(message =>
            message.GetType().Name == "ExecuteEntryStepWorkerCommand");
        var entryCompletion = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeLoopCompensationExecution.EntryStepCompleted"),
            workflowId,
            GetProperty<Guid>(entryWorker, "StepExecutionId"),
            CreateLoopState(stateType, workflowId, value: 1),
            null,
            DateTimeOffset.UtcNow);
        var startBody = Materialize(InvokeHandle(saga, entryCompletion, logger)).Single(message =>
            message.GetType().Name.Contains("BodyStepCommand", StringComparison.Ordinal));
        var bodyWorker = Materialize(InvokeHandle(saga, startBody, logger)).Single(message =>
            message.GetType().Name.Contains("BodyStepWorkerCommand", StringComparison.Ordinal));
        var bodyCompletionType = assembly.GetTypes().Single(type =>
            type.Namespace == "RuntimeLoopCompensationExecution"
            && type.Name == "BodyStepCompleted");
        var failedCompletion = CreateMessage(
            bodyCompletionType,
            workflowId,
            GetProperty<Guid>(bodyWorker, "StepExecutionId"),
            CreateLoopState(stateType, workflowId, value: 2, reducerFailed: true),
            null,
            DateTimeOffset.UtcNow);

        var messages = Materialize(InvokeHandle(saga, failedCompletion, logger));

        await Assert.That(messages).HasCount().EqualTo(1);
        await Assert.That(messages.Single().GetType().Name)
            .IsEqualTo("TriggerLoopOrderFailureHandlerCommand");
        await Assert.That(messages.Any(message => message.GetType().Name.StartsWith(
            "StartRetry_",
            StringComparison.Ordinal))).IsFalse();
        await Assert.That(messages.Any(message => message.GetType().Name == "StartAfterStepCommand"))
            .IsFalse();
        await Assert.That(GetProperty<int>(saga, "RetryIterationCount")).IsEqualTo(0);
        await Assert.That(Materialize(GetProperty<object>(
            saga,
            "PendingPostCompletionFailureClaims"))).HasCount().EqualTo(1);
    }

    /// <summary>A post-completion trigger cannot manufacture authority from public journal metadata.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ForgedPostCompletionTriggerLacksSagaMintedCapability()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());
        var fixture = CreateLinearInitialFixture(assembly);
        var aWorker = DispatchFirstWorker(fixture.Saga, fixture.FirstStart, fixture.Logger);
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        _ = Materialize(InvokeHandle(fixture.Saga, aCompleted, fixture.Logger));
        var journalEntry = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal")).Single();
        var trigger = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.TriggerRuntimeOrderFailureHandlerCommand"),
            fixture.WorkflowId,
            "AStep",
            "forged post-completion failure",
            "StateTransitionFailure",
            null);
        SetProperty(trigger, "ForwardOccurrenceKey", GetProperty<string>(journalEntry, "OccurrenceKey"));
        SetProperty(trigger, "CompensationScopeKey", GetProperty<string>(journalEntry, "ScopeKey"));
        SetProperty(trigger, "CompensationScopeKind", GetProperty<string>(journalEntry, "ScopeKind"));
        SetProperty(trigger, "CompensationLaneKey", GetProperty<object>(journalEntry, "LaneKey"));
        SetProperty(trigger, "CompensationForkId", GetProperty<object>(journalEntry, "ForkId"));
        SetProperty(trigger, "CompensationForkPathIndex", GetProperty<object>(journalEntry, "ForkPathIndex"));
        SetProperty(trigger, "CompensationJournalSequenceAtDispatch", 1L);
        SetProperty(trigger, "FailedForwardExecutionId", GetProperty<Guid>(journalEntry, "ForwardExecutionId"));
        SetProperty(trigger, "FailureOccurredAfterForwardCompletion", true);

        var messages = Materialize(InvokeHandle(fixture.Saga, trigger, fixture.Logger));

        await Assert.That(messages).IsEmpty();
        await Assert.That(Materialize(GetProperty<object>(fixture.Saga, "PendingPostCompletionFailureClaims"))).IsEmpty();
        await Assert.That(Materialize(GetProperty<object>(fixture.Saga, "ConsumedFailureTriggerClaims"))).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "ActiveCompensationScopeKey")).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "FailedStepName")).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "FailureTimestamp")).IsNull();
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .Contains("lacks its exact saga-minted capability");
    }

    /// <summary>Consumed authority is idempotent only for the exact failure boundary.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ConsumedFailureClaimRejectsAlteredFailureKind()
    {
        var fixture = CreateLinearFailureFixture();
        var firstMessages = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var acceptedTimestamp = GetProperty<DateTimeOffset?>(fixture.Saga, "FailureTimestamp");
        var exactRedelivery = Materialize(InvokeHandle(fixture.Saga, fixture.Trigger, fixture.Logger));
        var altered = CreateMessage(
            RequiredType(fixture.Assembly, "RuntimeCompensationExecution.TriggerRuntimeOrderFailureHandlerCommand"),
            fixture.WorkflowId,
            "CStep",
            "altered",
            "TimeoutException",
            null);
        CopyFailureTriggerMetadata(fixture.Trigger, altered);

        var alteredMessages = Materialize(InvokeHandle(fixture.Saga, altered, fixture.Logger));
        var consumed = Materialize(GetProperty<object>(fixture.Saga, "ConsumedFailureTriggerClaims"));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));

        await Assert.That(firstMessages.Any(message =>
            message.GetType().Name == "ExecuteUndoBStepWorkerCommand")).IsTrue();
        await Assert.That(exactRedelivery).IsEmpty();
        await Assert.That(alteredMessages).IsEmpty();
        await Assert.That(consumed).HasCount().EqualTo(1);
        await Assert.That(GetProperty<string>(consumed.Single(), "FailureKind"))
            .IsEqualTo("InvalidOperationException");
        await Assert.That(journal.Count(entry => GetProperty<string>(entry, "Status") == "InProgress"))
            .IsEqualTo(1);
        await Assert.That(GetProperty<DateTimeOffset?>(fixture.Saga, "FailureTimestamp"))
            .IsEqualTo(acceptedTimestamp);
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .Contains("does not match an active durable forward dispatch");
    }

    /// <summary>Malformed, high-water-corrupt, and cross-ledger claims all fail closed.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_CorruptFailureClaimLedgersFailClosed()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureWorkflow(),
            "AGWF045");

        var missing = CreateReducerFailureBoundaryFixture(assembly);
        SetProperty(missing.Saga, "PendingPostCompletionFailureClaims", null);
        var missingMessages = Materialize(InvokeHandle(missing.Saga, missing.Trigger, missing.Logger));

        await Assert.That(missingMessages).IsEmpty();
        await Assert.That(GetProperty<string>(missing.Saga, "CompensationFailureMessage"))
            .Contains("failure-authority ledger changed or became corrupt");
        await Assert.That(GetProperty<object>(missing.Saga, "FailedStepName")).IsNull();

        var highWater = CreateReducerFailureBoundaryFixture(assembly);
        var highWaterClaim = Materialize(GetProperty<object>(
            highWater.Saga,
            "PendingPostCompletionFailureClaims")).Single();
        SetProperty(highWaterClaim, "JournalSequenceAtDispatch", long.MaxValue);
        var highWaterMessages = Materialize(InvokeHandle(highWater.Saga, highWater.Trigger, highWater.Logger));

        await Assert.That(highWaterMessages).IsEmpty();
        await Assert.That(GetProperty<string>(highWater.Saga, "CompensationFailureMessage"))
            .Contains("failure-authority ledger changed or became corrupt");
        await Assert.That(GetProperty<object>(highWater.Saga, "FailedStepName")).IsNull();

        var crossLedger = CreateReducerFailureBoundaryFixture(assembly);
        var pending = GetProperty<object>(crossLedger.Saga, "PendingPostCompletionFailureClaims");
        var pendingClaim = Materialize(pending).Single();
        var consumed = GetProperty<object>(crossLedger.Saga, "ConsumedFailureTriggerClaims");
        AddToCollection(consumed, pendingClaim);
        var crossLedgerMessages = Materialize(InvokeHandle(
            crossLedger.Saga,
            crossLedger.Trigger,
            crossLedger.Logger));

        await Assert.That(crossLedgerMessages).IsEmpty();
        await Assert.That(GetProperty<string>(crossLedger.Saga, "CompensationFailureMessage"))
            .Contains("failure-authority ledger changed or became corrupt");
        await Assert.That(GetProperty<object>(crossLedger.Saga, "FailedStepName")).IsNull();
    }

    /// <summary>A later failing fork lane retains exact replay authority after rollback becomes active.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_SecondForkLaneFailureRedeliveryIsIdempotentDuringActiveRollback()
    {
        var fixture = CreateTypedForkRuntimeFixture(CreateTypedThreeLaneForkCompilationWorkflow());
        var leftWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.LeftStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteLeftStepWorkerCommand");
        var rightWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.RightStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteRightStepWorkerCommand");
        var thirdWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.ThirdStart!,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteThirdStepWorkerCommand");
        var leftTrigger = CreateForkFailureTrigger(
            fixture.Assembly,
            fixture.WorkflowId,
            leftWorker,
            "LeftStep");
        var rightTrigger = CreateForkFailureTrigger(
            fixture.Assembly,
            fixture.WorkflowId,
            rightWorker,
            "RightStep");

        _ = Materialize(InvokeHandle(fixture.Saga, leftTrigger, fixture.Logger));
        _ = Materialize(InvokeHandle(fixture.Saga, rightTrigger, fixture.Logger));
        var thirdCompleted = CreateMessage(
            RequiredType(fixture.Assembly, "TypedForkCompensationCompilation.ThirdStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(thirdWorker, "StepExecutionId"),
            CreateForkState(fixture.StateType, fixture.WorkflowId, value: 3),
            null,
            DateTimeOffset.UtcNow);
        var rollbackMessages = Materialize(InvokeHandle(fixture.Saga, thirdCompleted, fixture.Logger));
        var activeScope = GetProperty<string>(fixture.Saga, "ActiveCompensationScopeKey");
        var redeliveryMessages = Materialize(InvokeHandle(fixture.Saga, rightTrigger, fixture.Logger));
        var consumedClaims = Materialize(GetProperty<object>(fixture.Saga, "ConsumedFailureTriggerClaims"));

        await Assert.That(rollbackMessages.Any(message =>
            message.GetType().Name == "ExecuteUndoThirdStepWorkerCommand")).IsTrue();
        await Assert.That(activeScope).StartsWith("root/fork:");
        await Assert.That(redeliveryMessages).IsEmpty();
        await Assert.That(GetProperty<string>(fixture.Saga, "ActiveCompensationScopeKey"))
            .IsEqualTo(activeScope);
        await Assert.That(GetProperty<object>(fixture.Saga, "CompensationFailureMessage")).IsNull();
        await Assert.That(consumedClaims).HasCount().EqualTo(2);
        await Assert.That(consumedClaims.Select(claim => GetProperty<string>(claim, "OccurrenceKey")).ToList())
            .IsEquivalentTo(
                [
                    GetProperty<string>(leftTrigger, "ForwardOccurrenceKey"),
                    GetProperty<string>(rightTrigger, "ForwardOccurrenceKey"),
                ],
                CollectionOrdering.Any);
    }

    /// <summary>A diagnostic fork cannot mint or mutate across pending and active rollback.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_DiagnosticForkDuringRollbackIsAMonotonicNoOp()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureDiagnosticForkWorkflow(),
            "AGWF042",
            "AGWF045");
        var fixture = CreateReducerFailureBoundaryFixture(assembly);
        var forkCommand = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeCompensationExecution.ForkRuntimeOrderCommand"),
            fixture.WorkflowId,
            "BStep",
            "operator_explicit",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reason"] = "operator requested review",
            });
        var pendingClaim = Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims")).Single();
        var phaseBeforePendingFork = GetProperty<object>(fixture.Saga, "Phase").ToString();

        var pendingMessages = Materialize(InvokeHandle(
            fixture.Saga,
            forkCommand,
            fixture.Logger));

        await Assert.That(pendingMessages).IsEmpty();
        await Assert.That(GetProperty<int>(fixture.Saga, "DiagnosticForkCount_BStep")).IsEqualTo(0);
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims"))).HasCount().EqualTo(1);
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims")).Single()).IsSameReferenceAs(pendingClaim);
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString())
            .IsEqualTo(phaseBeforePendingFork);

        var rollbackMessages = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.Trigger,
            fixture.Logger));
        var undoB = rollbackMessages.Single(message =>
            message.GetType().Name == "ExecuteUndoBStepWorkerCommand");
        var activeScope = GetProperty<string>(fixture.Saga, "ActiveCompensationScopeKey");

        var activeMessages = Materialize(InvokeHandle(
            fixture.Saga,
            forkCommand,
            fixture.Logger));

        await Assert.That(activeMessages).IsEmpty();
        await Assert.That(GetProperty<int>(fixture.Saga, "DiagnosticForkCount_BStep")).IsEqualTo(0);
        await Assert.That(GetProperty<string>(fixture.Saga, "ActiveCompensationScopeKey"))
            .IsEqualTo(activeScope);
        await Assert.That(GetProperty<object>(fixture.Saga, "CompensationFailureMessage")).IsNull();

        var rollbackCompleted = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeCompensationExecution.RuntimeOrderUndoBStepRollbackCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(undoB, "RollbackId"),
            GetProperty<long>(undoB, "RollbackJournalSequence"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 1,
                trace: "B^-1",
                phase: "Failed"),
            DateTimeOffset.UtcNow);
        var continued = Materialize(InvokeHandle(
            fixture.Saga,
            rollbackCompleted,
            fixture.Logger));

        await Assert.That(continued.Any(message =>
            message.GetType().Name == "ExecuteUndoAStepWorkerCommand")).IsTrue();
    }

    /// <summary>A diagnostic fork cannot race a still-authorized forward occurrence.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_DiagnosticForkDuringForwardDispatchIsAMonotonicNoOp()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableReducerFailureDiagnosticForkWorkflow(),
            "AGWF042",
            "AGWF045");
        var fixture = CreateLinearInitialFixture(assembly);
        var aWorker = DispatchFirstWorker(fixture.Saga, fixture.FirstStart, fixture.Logger);
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 1,
                trace: "A",
                phase: "AStep"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(fixture.Saga, aCompleted, fixture.Logger)).Single();
        var bWorker = Materialize(InvokeHandle(fixture.Saga, startB, fixture.Logger)).Single(message =>
            message.GetType().Name == "ExecuteBStepWorkerCommand");
        var bCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(bWorker, "StepExecutionId"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 2,
                trace: "A;B",
                phase: "BStep"),
            null,
            DateTimeOffset.UtcNow);
        var startC = Materialize(InvokeHandle(fixture.Saga, bCompleted, fixture.Logger)).Single();
        _ = Materialize(InvokeHandle(fixture.Saga, startC, fixture.Logger)).Single(message =>
            message.GetType().Name == "ExecuteCStepWorkerCommand");
        var activeDispatchClaim = Materialize(GetProperty<object>(
            fixture.Saga,
            "ForwardDispatchClaims")).Single();
        var phaseBeforeFork = GetProperty<object>(fixture.Saga, "Phase").ToString();
        var forkCommand = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeCompensationExecution.ForkRuntimeOrderCommand"),
            fixture.WorkflowId,
            "BStep",
            "operator_explicit",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reason"] = "operator requested review",
            });

        var messages = Materialize(InvokeHandle(
            fixture.Saga,
            forkCommand,
            fixture.Logger));

        await Assert.That(messages).IsEmpty();
        await Assert.That(GetProperty<int>(fixture.Saga, "DiagnosticForkCount_BStep")).IsEqualTo(0);
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "ForwardDispatchClaims")).Single()).IsSameReferenceAs(activeDispatchClaim);
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims"))).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString())
            .IsEqualTo(phaseBeforeFork);
        await Assert.That(GetProperty<object>(fixture.Saga, "CompensationFailureMessage")).IsNull();
    }

    /// <summary>Late approval control messages cannot reopen a checkpoint during rollback.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_StaleApprovalControlDuringRollbackDoesNotMutateOrEscalate()
    {
        var assembly = CompileGeneratedAssembly(
            CreateExecutableApprovalLifecycleWorkflow(),
            "AGWF045");
        var fixture = CreateLinearInitialFixture(assembly);
        var aWorker = DispatchFirstWorker(fixture.Saga, fixture.FirstStart, fixture.Logger);
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var approvalRequest = Materialize(InvokeHandle(
            fixture.Saga,
            aCompleted,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "RequestReviewerApprovalEvent");
        var resumeType = RequiredType(
            assembly,
            "RuntimeCompensationExecution.ResumeReviewerApprovalCommand");
        var decisionType = resumeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single().GetParameters()[1].ParameterType;
        var rejected = CreateMessage(
            resumeType,
            fixture.WorkflowId,
            Enum.Parse(decisionType, "Rejected"),
            null,
            null);
        var approvedRedelivery = CreateMessage(
            resumeType,
            fixture.WorkflowId,
            Enum.Parse(decisionType, "Approved"),
            null,
            null);
        const string liveRequestId = "live-request";
        SetProperty(fixture.Saga, "PendingApprovalRequestId", liveRequestId);
        var trigger = InvokeHandle(fixture.Saga, rejected)!;
        var pendingPhase = GetProperty<object>(fixture.Saga, "Phase").ToString();
        var setPending = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeCompensationExecution.SetReviewerPendingApprovalCommand"),
            fixture.WorkflowId,
            "stale-request");
        var timeout = CreateMessage(
            RequiredType(
                assembly,
                "RuntimeCompensationExecution.ReviewerApprovalTimeoutCommand"),
            fixture.WorkflowId,
            liveRequestId);

        var duplicateResumeResult = InvokeHandle(fixture.Saga, approvedRedelivery);
        _ = InvokeHandle(fixture.Saga, setPending);
        var pendingTimeoutResult = InvokeHandle(fixture.Saga, timeout);

        await Assert.That(approvalRequest.GetType().Name).IsEqualTo("RequestReviewerApprovalEvent");
        await Assert.That(duplicateResumeResult).IsNull();
        await Assert.That(pendingTimeoutResult).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString())
            .IsEqualTo(pendingPhase);
        await Assert.That(GetProperty<object>(fixture.Saga, "PendingApprovalRequestId")).IsNull();
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims"))).HasCount().EqualTo(1);

        _ = Materialize(InvokeHandle(fixture.Saga, trigger, fixture.Logger));
        SetProperty(fixture.Saga, "PendingApprovalRequestId", liveRequestId);
        var activePhase = GetProperty<object>(fixture.Saga, "Phase").ToString();
        var activeScope = GetProperty<string>(fixture.Saga, "ActiveCompensationScopeKey");

        var activeResumeResult = InvokeHandle(fixture.Saga, approvedRedelivery);
        _ = InvokeHandle(fixture.Saga, setPending);
        var timeoutResult = InvokeHandle(fixture.Saga, timeout);

        await Assert.That(activeResumeResult).IsNull();
        await Assert.That(GetProperty<string>(fixture.Saga, "PendingApprovalRequestId"))
            .IsEqualTo(liveRequestId);
        await Assert.That(timeoutResult).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString())
            .IsEqualTo(activePhase);
        await Assert.That(GetProperty<string>(fixture.Saga, "ActiveCompensationScopeKey"))
            .IsEqualTo(activeScope);
        await Assert.That(Materialize(GetProperty<object>(
            fixture.Saga,
            "PendingPostCompletionFailureClaims"))).IsEmpty();
    }

    /// <summary>A queued or duplicate fork join cannot escape into forward work during rollback.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_StaleJoinDuringForkRollbackDoesNotStartJoinStep()
    {
        var fixture = CreateTypedForkRuntimeFixture();
        var leftWorker = Materialize(InvokeHandle(
            fixture.Saga,
            fixture.LeftStart,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteLeftStepWorkerCommand");
        var trigger = CreateForkFailureTrigger(
            fixture.Assembly,
            fixture.WorkflowId,
            leftWorker,
            "LeftStep");
        _ = Materialize(InvokeHandle(fixture.Saga, trigger, fixture.Logger));
        var phaseBeforeJoin = GetProperty<object>(fixture.Saga, "Phase").ToString();
        var pendingFork = GetProperty<string>(fixture.Saga, "PendingCompensationForkId");
        var join = CreateMessage(
            fixture.Assembly.GetTypes().Single(type =>
                type.Namespace == "TypedForkCompensationCompilation"
                && type.Name.StartsWith("JoinFork_", StringComparison.Ordinal)
                && type.Name.EndsWith("_Command", StringComparison.Ordinal)),
            fixture.WorkflowId);

        var joinResult = InvokeHandle(fixture.Saga, join, fixture.Logger);

        await Assert.That(joinResult).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString())
            .IsEqualTo(phaseBeforeJoin);
        await Assert.That(GetProperty<string>(fixture.Saga, "PendingCompensationForkId"))
            .IsEqualTo(pendingFork);
        await Assert.That(GetProperty<object>(fixture.Saga, "CompensationFailureMessage")).IsNull();
    }

    /// <summary>A topology-shaped trigger cannot invent authority for work that was never dispatched.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_UndispatchedOccurrenceTriggerFailsClosedWithoutAuditMutation()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());
        var fixture = CreateLinearInitialFixture(assembly);
        var trigger = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.TriggerRuntimeOrderFailureHandlerCommand"),
            fixture.WorkflowId,
            "AStep",
            "forged",
            "InvalidOperationException",
            null);
        SetProperty(trigger, "ForwardOccurrenceKey", "root/step:AStep#0");
        SetProperty(trigger, "CompensationScopeKey", "root");
        SetProperty(trigger, "CompensationScopeKind", "Root");
        SetProperty(trigger, "CompensationJournalSequenceAtDispatch", 0L);
        SetProperty(trigger, "FailedForwardExecutionId", Guid.NewGuid());

        var messages = Materialize(InvokeHandle(fixture.Saga, trigger, fixture.Logger));
        var journal = Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal"));
        var claims = Materialize(GetProperty<object>(fixture.Saga, "ForwardDispatchClaims"));

        await Assert.That(messages).IsEmpty();
        await Assert.That(journal).IsEmpty();
        await Assert.That(claims).IsEmpty();
        await Assert.That(GetProperty<object>(fixture.Saga, "ActiveCompensationScopeKey")).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "FailedStepName")).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "FailureTimestamp")).IsNull();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .Contains("does not match an active durable forward dispatch");
    }

    /// <summary>Only the exact durable dispatch identity may complete an occurrence.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_ForwardCompletionRequiresExactNonEmptyDispatchIdentity()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());

        var exact = CreateLinearInitialFixture(assembly);
        var exactWorker = DispatchFirstWorker(exact.Saga, exact.FirstStart, exact.Logger);
        var exactCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            exact.WorkflowId,
            GetProperty<Guid>(exactWorker, "StepExecutionId"),
            CreateState(exact.StateType, exact.WorkflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var firstDelivery = Materialize(InvokeHandle(exact.Saga, exactCompletion, exact.Logger));
        var redelivery = Materialize(InvokeHandle(exact.Saga, exactCompletion, exact.Logger));

        await Assert.That(firstDelivery.Single().GetType().Name).IsEqualTo("StartBStepCommand");
        await Assert.That(redelivery).IsEmpty();
        await Assert.That(Materialize(GetProperty<object>(exact.Saga, "CompensationJournal")))
            .HasCount().EqualTo(1);
        await Assert.That(GetProperty<string>(GetProperty<object>(exact.Saga, "State"), "Trace"))
            .IsEqualTo("A");

        var conflictingCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            exact.WorkflowId,
            Guid.NewGuid(),
            CreateState(exact.StateType, exact.WorkflowId, stage: 999, trace: "forged"),
            null,
            DateTimeOffset.UtcNow);
        var conflictingMessages = Materialize(InvokeHandle(
            exact.Saga,
            conflictingCompletion,
            exact.Logger));

        await Assert.That(conflictingMessages).IsEmpty();
        await Assert.That(GetProperty<string>(GetProperty<object>(exact.Saga, "State"), "Trace"))
            .IsEqualTo("A");
        await Assert.That(GetProperty<object>(exact.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(exact.Saga, "CompensationFailureMessage"))
            .Contains("contradicts the durable compensation journal");

        var different = CreateLinearInitialFixture(assembly);
        _ = DispatchFirstWorker(different.Saga, different.FirstStart, different.Logger);
        var differentCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            different.WorkflowId,
            Guid.NewGuid(),
            CreateState(different.StateType, different.WorkflowId, stage: 999, trace: "forged"),
            null,
            DateTimeOffset.UtcNow);
        var differentMessages = Materialize(InvokeHandle(
            different.Saga,
            differentCompletion,
            different.Logger));

        await Assert.That(differentMessages).IsEmpty();
        await Assert.That(Materialize(GetProperty<object>(different.Saga, "CompensationJournal"))).IsEmpty();
        await Assert.That(GetProperty<string>(GetProperty<object>(different.Saga, "State"), "Trace"))
            .IsEqualTo("start");
        await Assert.That(GetProperty<object>(different.Saga, "Phase").ToString()).IsEqualTo("Failed");

        var empty = CreateLinearInitialFixture(assembly);
        _ = DispatchFirstWorker(empty.Saga, empty.FirstStart, empty.Logger);
        var emptyCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            empty.WorkflowId,
            Guid.Empty,
            CreateState(empty.StateType, empty.WorkflowId, stage: 999, trace: "forged"),
            null,
            DateTimeOffset.UtcNow);
        var emptyMessages = Materialize(InvokeHandle(empty.Saga, emptyCompletion, empty.Logger));

        await Assert.That(emptyMessages).IsEmpty();
        await Assert.That(Materialize(GetProperty<object>(empty.Saga, "CompensationJournal"))).IsEmpty();
        await Assert.That(GetProperty<string>(GetProperty<object>(empty.Saga, "State"), "Trace"))
            .IsEqualTo("start");
        await Assert.That(GetProperty<object>(empty.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(empty.Saga, "CompensationFailureMessage"))
            .Contains("empty execution identity");
    }

    /// <summary>
    /// A completed execution identity cannot be replayed through a different compiled occurrence,
    /// even though an exact execution-id replay of the original occurrence is idempotent.
    /// </summary>
    [Test]
    public async Task Execute_GeneratedSaga_CompletedExecutionIdRejectsDifferentStableOccurrence()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());
        var fixture = CreateLinearInitialFixture(assembly);
        var aWorker = DispatchFirstWorker(fixture.Saga, fixture.FirstStart, fixture.Logger);
        var aExecutionId = GetProperty<Guid>(aWorker, "StepExecutionId");
        var aCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            fixture.WorkflowId,
            aExecutionId,
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(
            fixture.Saga,
            aCompletion,
            fixture.Logger)).Single();
        var bWorker = Materialize(InvokeHandle(
            fixture.Saga,
            startB,
            fixture.Logger)).Single(message =>
                message.GetType().Name == "ExecuteBStepWorkerCommand");
        var stateBeforeForgery = GetProperty<object>(fixture.Saga, "State");
        var forgedBCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            fixture.WorkflowId,
            aExecutionId,
            CreateState(fixture.StateType, fixture.WorkflowId, stage: 999, trace: "forged"),
            null,
            DateTimeOffset.UtcNow);

        var messages = Materialize(InvokeHandle(
            fixture.Saga,
            forgedBCompletion,
            fixture.Logger));
        var claims = Materialize(GetProperty<object>(fixture.Saga, "ForwardDispatchClaims"));

        await Assert.That(messages).IsEmpty();
        await Assert.That(ReferenceEquals(GetProperty<object>(fixture.Saga, "State"), stateBeforeForgery)).IsTrue();
        await Assert.That(GetProperty<object>(fixture.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(fixture.Saga, "CompensationFailureMessage"))
            .Contains("contradicts the durable compensation journal");
        await Assert.That(Materialize(GetProperty<object>(fixture.Saga, "CompensationJournal")))
            .HasCount().EqualTo(1);
        await Assert.That(claims).HasCount().EqualTo(1);
        await Assert.That(GetProperty<Guid>(claims.Single(), "ForwardExecutionId"))
            .IsEqualTo(GetProperty<Guid>(bWorker, "StepExecutionId"));
    }

    /// <summary>The rollback correlation transform is a stable permutation across its cycle boundary.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_RollbackIdentityMappingIsStableDistinctAndInjectiveAtBoundary()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());
        var sagaType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderSaga");
        var transform = sagaType.GetMethod(
            "CreateCompensationRollbackId",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var maximumBytes = Enumerable.Repeat(byte.MaxValue, 16).ToArray();
        var beforeMaximumBytes = maximumBytes.ToArray();
        beforeMaximumBytes[0]--;
        var maximum = new Guid(maximumBytes);
        var beforeMaximum = new Guid(beforeMaximumBytes);
        var oneBytes = new byte[16];
        oneBytes[0] = 1;
        var firstNonEmpty = new Guid(oneBytes);

        var mappedBeforeMaximum = (Guid)transform.Invoke(null, [beforeMaximum])!;
        var mappedMaximum = (Guid)transform.Invoke(null, [maximum])!;
        var repeatedMaximum = (Guid)transform.Invoke(null, [maximum])!;
        var mappedFirstNonEmpty = (Guid)transform.Invoke(null, [firstNonEmpty])!;

        await Assert.That(mappedBeforeMaximum).IsEqualTo(maximum);
        await Assert.That(mappedMaximum).IsEqualTo(firstNonEmpty);
        await Assert.That(repeatedMaximum).IsEqualTo(mappedMaximum);
        await Assert.That(mappedBeforeMaximum).IsNotEqualTo(mappedMaximum);
        await Assert.That(mappedMaximum).IsNotEqualTo(mappedFirstNonEmpty);
        await Assert.That(mappedMaximum).IsNotEqualTo(Guid.Empty);
        await Assert.That(mappedMaximum).IsNotEqualTo(maximum);
    }

    /// <summary>Missing or corrupt dispatch claims fail closed before a result can mutate saga state.</summary>
    [Test]
    public async Task Execute_GeneratedSaga_NullOrCorruptForwardDispatchClaimsFailClosed()
    {
        var assembly = CompileGeneratedAssembly(CreateExecutableTypedWorkflow());

        var missing = CreateLinearInitialFixture(assembly);
        var missingWorker = DispatchFirstWorker(missing.Saga, missing.FirstStart, missing.Logger);
        SetProperty(missing.Saga, "ForwardDispatchClaims", null);
        var missingCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            missing.WorkflowId,
            GetProperty<Guid>(missingWorker, "StepExecutionId"),
            CreateState(missing.StateType, missing.WorkflowId, stage: 999, trace: "forged"),
            null,
            DateTimeOffset.UtcNow);
        var missingMessages = Materialize(InvokeHandle(missing.Saga, missingCompletion, missing.Logger));

        await Assert.That(missingMessages).IsEmpty();
        await Assert.That(GetProperty<string>(GetProperty<object>(missing.Saga, "State"), "Trace"))
            .IsEqualTo("start");
        await Assert.That(GetProperty<object>(missing.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(missing.Saga, "CompensationFailureMessage"))
            .Contains("changed or became corrupt before a forward result");

        var corrupt = CreateLinearInitialFixture(assembly);
        var corruptWorker = DispatchFirstWorker(corrupt.Saga, corrupt.FirstStart, corrupt.Logger);
        var claims = Materialize(GetProperty<object>(corrupt.Saga, "ForwardDispatchClaims"));
        SetProperty(claims.Single(), "ForwardExecutionId", Guid.Empty);
        var corruptCompletion = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            corrupt.WorkflowId,
            GetProperty<Guid>(corruptWorker, "StepExecutionId"),
            CreateState(corrupt.StateType, corrupt.WorkflowId, stage: 999, trace: "forged"),
            null,
            DateTimeOffset.UtcNow);
        var corruptMessages = Materialize(InvokeHandle(corrupt.Saga, corruptCompletion, corrupt.Logger));

        await Assert.That(corruptMessages).IsEmpty();
        await Assert.That(Materialize(GetProperty<object>(corrupt.Saga, "CompensationJournal"))).IsEmpty();
        await Assert.That(GetProperty<string>(GetProperty<object>(corrupt.Saga, "State"), "Trace"))
            .IsEqualTo("start");
        await Assert.That(GetProperty<object>(corrupt.Saga, "Phase").ToString()).IsEqualTo("Failed");
        await Assert.That(GetProperty<string>(corrupt.Saga, "CompensationFailureMessage"))
            .Contains("changed or became corrupt before a forward result");
    }

    /// <summary>Legacy compensation keeps the #135 runtime compatibility path.</summary>
    [Test]
    public async Task Emit_LegacyProgram_DoesNotActivateDerivedJournal()
    {
        var legacyStep = StepModel.Create(
            "Charge",
            "TestNamespace.Charge",
            compensation: new CompensationModel("TestNamespace.Refund"));
        var model = WorkflowModel.Create(
            "legacy",
            "Legacy",
            "TestNamespace",
            ["Charge", "Finish"],
            "TestState",
            steps:
            [
                legacyStep,
                StepModel.Create("Finish", "TestNamespace.Finish"),
                StepModel.Create("Refund", "TestNamespace.Refund"),
            ]);

        var saga = SagaEmitter.Emit(model);

        await Assert.That(CompensationTopology.GetProgramKind(model)).IsEqualTo(CompensationProgramKind.Legacy);
        await Assert.That(saga).Contains("new ExecuteRefundWorkerCommand(WorkflowId, Guid.NewGuid(), State)");
        await Assert.That(saga).DoesNotContain("CompensationJournalEntry");
        await Assert.That(saga).DoesNotContain("RecordCompensationCompletion(");
    }

    /// <summary>Legacy compensation output remains a complete compilable program.</summary>
    [Test]
    public async Task Emit_LegacyProgram_FullGeneratedOutputCompiles()
    {
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(CreateLegacyCompilationWorkflow());
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "LegacyOrderSaga.g.cs");

        await Assert.That(saga).DoesNotContain("RecordCompensationCompletion(");
        await Assert.That(saga).DoesNotContain("ResolveCompensationScopeInstance(");
    }

    private static WorkflowModel CreateLinearModel(TimeSpan? firstInverseTimeout = null)
    {
        var first = TypedStep("A", "UndoA", "a", "undo-a");
        if (firstInverseTimeout is not null)
        {
            first = first with
            {
                Compensation = first.Compensation! with { Timeout = firstInverseTimeout },
            };
        }

        var steps = new List<StepModel>
        {
            first,
            TypedStep("B", "UndoB", "b", "undo-b"),
            StepModel.Create("C", "TestNamespace.C"),
            StepModel.Create("UndoA", "TestNamespace.UndoA"),
            StepModel.Create("UndoB", "TestNamespace.UndoB"),
        };

        return WorkflowModel.Create(
            "process-order",
            "ProcessOrder",
            "TestNamespace",
            ["A", "B", "C"],
            "TestState",
            steps: steps);
    }

    private static WorkflowModel CreateForkModel(bool leftTimeout = false)
    {
        var left = TypedStep("Left", "UndoLeft", "left", "undo-left") with
        {
            Timeout = leftTimeout ? new TimeoutModel(TimeSpan.FromSeconds(5)) : null,
        };
        var right = TypedStep("Right", "UndoRight", "right", "undo-right");
        var fork = ForkModel.Create(
            "fan-out",
            "Start",
            [
                ForkPathModel.Create(0, [left], false, false),
                ForkPathModel.Create(1, [right], false, false),
            ],
            "Join");

        return WorkflowModel.Create(
            "fork-order",
            "ForkOrder",
            "TestNamespace",
            ["Start", "Left", "Right", "Join", "Finish"],
            "TestState",
            steps:
            [
                StepModel.Create("Start", "TestNamespace.Start"),
                left,
                right,
                StepModel.Create("Join", "TestNamespace.Join"),
                StepModel.Create("Finish", "TestNamespace.Finish"),
                StepModel.Create("UndoLeft", "TestNamespace.UndoLeft"),
                StepModel.Create("UndoRight", "TestNamespace.UndoRight"),
            ],
            forks: [fork]);
    }

    private static WorkflowModel CreateNestedScopeModel()
    {
        var outerA = TypedStep("OuterA", "UndoOuterA", "outer-a", "undo-outer-a");
        var innerA = TypedStep("InnerA", "UndoInnerA", "inner-a", "undo-inner-a") with
        {
            LoopName = "Inner",
        };
        var innerB = TypedStep("InnerB", "UndoInnerB", "inner-b", "undo-inner-b") with
        {
            LoopName = "Inner",
        };
        var loop = LoopModel.Create(
            "Inner",
            "inner-condition",
            3,
            [innerA, innerB],
            continuationStepName: "OuterC");

        return WorkflowModel.Create(
            "nested-order",
            "NestedOrder",
            "TestNamespace",
            ["OuterA", "Inner_InnerA", "Inner_InnerB", "OuterC"],
            "TestState",
            steps:
            [
                outerA,
                innerA,
                innerB,
                StepModel.Create("OuterC", "TestNamespace.OuterC"),
                StepModel.Create("UndoOuterA", "TestNamespace.UndoOuterA"),
                StepModel.Create("UndoInnerA", "TestNamespace.UndoInnerA"),
                StepModel.Create("UndoInnerB", "TestNamespace.UndoInnerB"),
            ],
            loops: [loop]);
    }

    private static WorkflowModel CreateDeepLoopModel(bool duplicateDeepLoop)
    {
        const string outerPrefix = "Outer_With_Underscore";
        const string innerPrefix = "Outer_With_Underscore_Inner_Part";
        const string deepPrefix = "Outer_With_Underscore_Inner_Part_Deep";
        var outerLeaf = TypedStep("OuterLeaf", "UndoOuterLeaf", "outer", "undo-outer") with
        {
            LoopName = outerPrefix,
        };
        var innerLeaf = TypedStep("InnerLeaf", "UndoInnerLeaf", "inner", "undo-inner") with
        {
            LoopName = innerPrefix,
        };
        var deepLeaf = TypedStep("Leaf", "UndoLeaf", "deep", "undo-deep") with
        {
            LoopName = deepPrefix,
        };
        var outer = LoopModel.Create(
            outerPrefix,
            "outer-condition",
            2,
            [outerLeaf]);
        var inner = LoopModel.Create(
            "Inner_Part",
            "inner-condition",
            2,
            [innerLeaf],
            parentLoopName: outerPrefix);
        var deep = LoopModel.Create(
            "Deep",
            "deep-condition",
            2,
            [deepLeaf],
            parentLoopName: innerPrefix);
        var loops = duplicateDeepLoop
            ? new List<LoopModel> { outer, inner, deep, deep }
            : [outer, inner, deep];

        return WorkflowModel.Create(
            "deep-loops",
            "DeepLoops",
            "TestNamespace",
            [outerLeaf.PhaseName, innerLeaf.PhaseName, deepLeaf.PhaseName],
            "TestState",
            steps:
            [
                outerLeaf,
                innerLeaf,
                deepLeaf,
                StepModel.Create("UndoOuterLeaf", "TestNamespace.UndoOuterLeaf"),
                StepModel.Create("UndoInnerLeaf", "TestNamespace.UndoInnerLeaf"),
                StepModel.Create("UndoLeaf", "TestNamespace.UndoLeaf"),
            ],
            loops: loops);
    }

    private static StepModel TypedStep(
        string stepName,
        string inverseStepName,
        string actionName,
        string inverseActionName)
    {
        return StepModel.Create(
            stepName,
            $"TestNamespace.{stepName}",
            compensation: new CompensationModel(
                $"TestNamespace.{inverseStepName}",
                InverseAction: new WorkflowActionReferenceModel("orders", "Order", inverseActionName),
                InverseActionResolution: WorkflowActionReferenceResolution.Resolved),
            action: new WorkflowActionReferenceModel("orders", "Order", actionName),
            actionResolution: WorkflowActionReferenceResolution.Resolved);
    }

    private static HandlerContext CreateHandlerContext(
        StepModel step,
        bool isLastStep = false,
        string? nextStepName = null,
        ApprovalModel? approval = null) =>
        new(
            StepIndex: 0,
            IsLastStep: isLastStep,
            IsTerminalStep: false,
            NextStepName: nextStepName,
            StepModel: step,
            LoopsAtStep: null,
            BranchAtStep: null,
            ApprovalAtStep: approval,
            ForkAtStep: null,
            ForkPathEnding: null,
            JoinForkAtStep: null,
            IsForkPathStep: false);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string RollbackCompletionRegion(
        string saga,
        string marker = "ProcessOrderUndoARollbackCompleted evt")
    {
        var start = saga.IndexOf(marker, StringComparison.Ordinal);
        var end = saga.IndexOf("RollbackFailed evt", start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException("Generated saga did not contain the expected rollback handler region.");
        }

        return saga.Substring(start, end - start);
    }

    private static Assembly CompileGeneratedAssembly(
        string source,
        params string[] allowedDiagnosticIds)
    {
        var allowedDiagnostics = allowedDiagnosticIds.ToHashSet(StringComparer.Ordinal);
        var references = new List<MetadataReference>();
        var paths = new HashSet<string>(StringComparer.Ordinal);

        void AddReference(string path)
        {
            if (!string.IsNullOrEmpty(path) && paths.Add(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            AddReference(path);
        }

        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string platformAssemblies)
        {
            foreach (var path in platformAssemblies.Split(Path.PathSeparator))
            {
                AddReference(path);
            }
        }

        foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!loadedAssembly.IsDynamic && !string.IsNullOrEmpty(loadedAssembly.Location))
            {
                AddReference(loadedAssembly.Location);
            }
        }

        var compilation = CSharpCompilation.Create(
            "RuntimeRollback_" + Guid.NewGuid().ToString("N"),
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("global using System.Collections.Generic;"),
            ],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new WorkflowIncrementalGenerator().AsSourceGenerator(),
            new StateReducerIncrementalGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var driverDiagnostics);
        var errors = driverDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && !allowedDiagnostics.Contains(diagnostic.Id))
            .ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Executable generated rollback fixture failed compilation: "
                + string.Join(" | ", errors.Select(error => error.Id + ": " + error.GetMessage())));
        }

        using var assemblyStream = new MemoryStream();
        var emitResult = outputCompilation.Emit(assemblyStream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                "Executable generated rollback fixture failed emission: "
                + string.Join(" | ", emitResult.Diagnostics.Select(error => error.Id + ": " + error.GetMessage())));
        }

        assemblyStream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
    }

    private static Type RequiredType(Assembly assembly, string fullName) =>
        assembly.GetType(fullName, throwOnError: true)!;

    private static object CreateNullLogger(Type sagaType)
    {
        var loggerType = typeof(NullLogger<>).MakeGenericType(sagaType);
        return loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? Activator.CreateInstance(loggerType)!;
    }

    private static object CreateMessage(Type messageType, params object?[] arguments) =>
        messageType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(constructor => constructor.GetParameters().Length == arguments.Length)
            .Invoke(arguments);

    private static object CreateState(
        Type stateType,
        Guid workflowId,
        int stage,
        string trace,
        string? phase = null)
    {
        var state = Activator.CreateInstance(stateType)!;
        stateType.GetProperty("WorkflowId")!.SetValue(state, workflowId);
        stateType.GetProperty("Stage")!.SetValue(state, stage);
        stateType.GetProperty("Trace")!.SetValue(state, trace);
        if (phase is not null)
        {
            var phaseProperty = stateType.GetProperty("Phase")!;
            var phaseValue = phaseProperty.PropertyType.IsEnum
                ? Enum.Parse(phaseProperty.PropertyType, phase)
                : Activator.CreateInstance(
                    phaseProperty.PropertyType,
                    [string.Equals(phase, "Failed", StringComparison.Ordinal)]);
            phaseProperty.SetValue(state, phaseValue);
        }

        return state;
    }

    private static object CreateForkState(
        Type stateType,
        Guid workflowId,
        int value,
        bool reducerFailed = false)
    {
        var state = Activator.CreateInstance(stateType)!;
        stateType.GetProperty("WorkflowId")!.SetValue(state, workflowId);
        stateType.GetProperty("Value")!.SetValue(state, value);
        if (reducerFailed && stateType.GetProperty("Phase") is { } phaseProperty)
        {
            phaseProperty.SetValue(
                state,
                Activator.CreateInstance(phaseProperty.PropertyType, [true]));
        }

        return state;
    }

    private static object CreateLoopState(
        Type stateType,
        Guid workflowId,
        int value,
        bool reducerFailed = false)
    {
        var state = Activator.CreateInstance(stateType)!;
        stateType.GetProperty("WorkflowId")!.SetValue(state, workflowId);
        stateType.GetProperty("Value")!.SetValue(state, value);
        stateType.GetProperty("Done")!.SetValue(state, false);
        if (stateType.GetProperty("Phase") is { } phaseProperty)
        {
            phaseProperty.SetValue(
                state,
                Activator.CreateInstance(phaseProperty.PropertyType, [reducerFailed]));
        }

        return state;
    }

    private static object InvokeHandle(object saga, object message, object logger)
    {
        var method = saga.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate =>
            {
                var parameters = candidate.GetParameters();
                return string.Equals(candidate.Name, "Handle", StringComparison.Ordinal)
                    && parameters.Length == 2
                    && parameters[0].ParameterType == message.GetType();
            });
        return method.Invoke(saga, [message, logger])!;
    }

    private static object? InvokeHandle(object saga, object message)
    {
        var method = saga.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate =>
            {
                var parameters = candidate.GetParameters();
                return string.Equals(candidate.Name, "Handle", StringComparison.Ordinal)
                    && parameters.Length == 1
                    && parameters[0].ParameterType == message.GetType();
            });
        return method.Invoke(saga, [message]);
    }

    private static void CopyProperty(object source, object target, string propertyName)
    {
        var value = source.GetType().GetProperty(propertyName)!.GetValue(source);
        target.GetType().GetProperty(propertyName)!.SetValue(target, value);
    }

    private static void CopyCompensationMetadata(object worker, object trigger)
    {
        CopyProperty(worker, trigger, "ForwardOccurrenceKey");
        CopyProperty(worker, trigger, "CompensationScopeKey");
        CopyProperty(worker, trigger, "CompensationScopeKind");
        CopyProperty(worker, trigger, "CompensationLaneKey");
        CopyProperty(worker, trigger, "CompensationForkId");
        CopyProperty(worker, trigger, "CompensationForkPathIndex");
        CopyProperty(worker, trigger, "CompensationJournalSequenceAtDispatch");
        SetProperty(
            trigger,
            "FailedForwardExecutionId",
            GetProperty<Guid>(worker, "StepExecutionId"));
    }

    private static void CopyFailureTriggerMetadata(object source, object target)
    {
        CopyProperty(source, target, "ForwardOccurrenceKey");
        CopyProperty(source, target, "CompensationScopeKey");
        CopyProperty(source, target, "CompensationScopeKind");
        CopyProperty(source, target, "CompensationLaneKey");
        CopyProperty(source, target, "CompensationForkId");
        CopyProperty(source, target, "CompensationForkPathIndex");
        CopyProperty(source, target, "CompensationJournalSequenceAtDispatch");
        CopyProperty(source, target, "FailedForwardExecutionId");
        CopyProperty(source, target, "FailureOccurredAfterForwardCompletion");
    }

    private static void AddToCollection(object collection, object value) =>
        collection.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)!.Invoke(
            collection,
            [value]);

    private static object CreateForkFailureTrigger(
        Assembly assembly,
        Guid workflowId,
        object worker,
        string stepName)
    {
        var trigger = CreateMessage(
            RequiredType(
                assembly,
                "TypedForkCompensationCompilation.TriggerForkOrderFailureHandlerCommand"),
            workflowId,
            stepName,
            "boom",
            "InvalidOperationException",
            null);
        CopyCompensationMetadata(worker, trigger);
        return trigger;
    }

    private static object GetForkPathProperty(object saga, int pathIndex, string suffix)
    {
        var property = saga.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate => candidate.Name.EndsWith(
                $"_Path{pathIndex}{suffix}",
                StringComparison.Ordinal));
        return property.GetValue(saga)!;
    }

    private static (
        Assembly Assembly,
        object Saga,
        Type StateType,
        Guid WorkflowId,
        object Logger,
        object LeftStart,
        object RightStart,
        object? ThirdStart) CreateTypedForkRuntimeFixture(string? source = null)
    {
        var assembly = CompileGeneratedAssembly(
            source ?? CreateTypedForkCompilationWorkflow(),
            "AGWF045");
        var sagaType = RequiredType(
            assembly,
            "TypedForkCompensationCompilation.ForkOrderSaga");
        var stateType = RequiredType(
            assembly,
            "TypedForkCompensationCompilation.ForkState");
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(
                assembly,
                "TypedForkCompensationCompilation.StartForkOrderCommand"),
            workflowId,
            CreateForkState(stateType, workflowId, value: 0));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var start = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);
        var startWorker = Materialize(InvokeHandle(saga, start, logger)).Single(message =>
            message.GetType().Name == "ExecuteStartStepWorkerCommand");
        var startCompleted = CreateMessage(
            RequiredType(
                assembly,
                "TypedForkCompensationCompilation.StartStepCompleted"),
            workflowId,
            GetProperty<Guid>(startWorker, "StepExecutionId"),
            CreateForkState(stateType, workflowId, value: 1),
            null,
            DateTimeOffset.UtcNow);
        var starts = Materialize(InvokeHandle(saga, startCompleted, logger));
        return (
            assembly,
            saga,
            stateType,
            workflowId,
            logger,
            starts.Single(message => message.GetType().Name == "StartLeftStepCommand"),
            starts.Single(message => message.GetType().Name == "StartRightStepCommand"),
            starts.SingleOrDefault(message => message.GetType().Name == "StartThirdStepCommand"));
    }

    private static T GetProperty<T>(object target, string propertyName) =>
        (T)target.GetType().GetProperty(propertyName)!.GetValue(target)!;

    private static void SetProperty(object target, string propertyName, object? value) =>
        target.GetType().GetProperty(propertyName)!.SetValue(target, value);

    private static void SetEnumProperty(object target, string propertyName, string value)
    {
        var property = target.GetType().GetProperty(propertyName)!;
        property.SetValue(target, Enum.Parse(property.PropertyType, value));
    }

    private static (
        object Saga,
        Type StateType,
        Guid WorkflowId,
        object Logger,
        object FirstStart) CreateLinearInitialFixture(Assembly assembly)
    {
        var sagaType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderSaga");
        var stateType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeState");
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.StartRuntimeOrderCommand"),
            workflowId,
            CreateState(stateType, workflowId, stage: 0, trace: "start"));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var firstStart = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        return (saga, stateType, workflowId, CreateNullLogger(sagaType), firstStart);
    }

    private static object DispatchFirstWorker(object saga, object firstStart, object logger) =>
        Materialize(InvokeHandle(saga, firstStart, logger)).Single(message =>
            message.GetType().Name == "ExecuteAStepWorkerCommand");

    private static (
        Assembly Assembly,
        object Saga,
        Type StateType,
        Guid WorkflowId,
        object Logger,
        object NextAfterB) CreateLinearPrefixFixture(string source)
    {
        var assembly = CompileGeneratedAssembly(source);
        var sagaType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeOrderSaga");
        var stateType = RequiredType(assembly, "RuntimeCompensationExecution.RuntimeState");
        var workflowId = Guid.NewGuid();
        var startWorkflow = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.StartRuntimeOrderCommand"),
            workflowId,
            CreateState(stateType, workflowId, stage: 0, trace: "start"));
        var startResult = sagaType.GetMethod(
            "Start",
            BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [startWorkflow])!;
        var saga = startResult.GetType().GetField("Item1")!.GetValue(startResult)!;
        var startA = startResult.GetType().GetField("Item2")!.GetValue(startResult)!;
        var logger = CreateNullLogger(sagaType);

        var aWorker = Materialize(InvokeHandle(saga, startA, logger)).Single(message =>
            message.GetType().Name == "ExecuteAStepWorkerCommand");
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            workflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(saga, aCompleted, logger)).Single();
        var bWorker = Materialize(InvokeHandle(saga, startB, logger)).Single(message =>
            message.GetType().Name == "ExecuteBStepWorkerCommand");
        var bCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            workflowId,
            GetProperty<Guid>(bWorker, "StepExecutionId"),
            CreateState(stateType, workflowId, stage: 2, trace: "A;B"),
            null,
            DateTimeOffset.UtcNow);
        var nextAfterB = Materialize(InvokeHandle(saga, bCompleted, logger)).Single();
        return (assembly, saga, stateType, workflowId, logger, nextAfterB);
    }

    private static (
        Assembly Assembly,
        object Saga,
        Type StateType,
        Guid WorkflowId,
        object Logger,
        object Trigger,
        object Timeout,
        object FailedWorker) CreateLinearFailureFixture()
    {
        var prefix = CreateLinearPrefixFixture(CreateExecutableTypedWorkflow());
        var cStartMessages = Materialize(InvokeHandle(prefix.Saga, prefix.NextAfterB, prefix.Logger));
        var cWorker = cStartMessages.Single(message => message.GetType().Name == "ExecuteCStepWorkerCommand");
        var timeout = cStartMessages.Single(message => message.GetType().Name == "CStepTimeout");
        var trigger = CreateMessage(
            RequiredType(prefix.Assembly, "RuntimeCompensationExecution.TriggerRuntimeOrderFailureHandlerCommand"),
            prefix.WorkflowId,
            "CStep",
            "boom",
            "InvalidOperationException",
            null);
        CopyCompensationMetadata(cWorker, trigger);
        return (
            prefix.Assembly,
            prefix.Saga,
            prefix.StateType,
            prefix.WorkflowId,
            prefix.Logger,
            trigger,
            timeout,
            cWorker);
    }

    private static (
        Assembly Assembly,
        object Saga,
        Type StateType,
        Guid WorkflowId,
        object Logger,
        object Trigger) CreateReducerFailureBoundaryFixture(Assembly assembly)
    {
        var fixture = CreateLinearInitialFixture(assembly);
        var aWorker = DispatchFirstWorker(fixture.Saga, fixture.FirstStart, fixture.Logger);
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(aWorker, "StepExecutionId"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 1,
                trace: "A",
                phase: "AStep"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(fixture.Saga, aCompleted, fixture.Logger)).Single();
        var bWorker = Materialize(InvokeHandle(fixture.Saga, startB, fixture.Logger)).Single(message =>
            message.GetType().Name == "ExecuteBStepWorkerCommand");
        var bCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            fixture.WorkflowId,
            GetProperty<Guid>(bWorker, "StepExecutionId"),
            CreateState(
                fixture.StateType,
                fixture.WorkflowId,
                stage: 2,
                trace: "A;B",
                phase: "Failed"),
            null,
            DateTimeOffset.UtcNow);
        var trigger = Materialize(InvokeHandle(fixture.Saga, bCompleted, fixture.Logger)).Single(message =>
            message.GetType().Name == "TriggerRuntimeOrderFailureHandlerCommand");
        return (
            assembly,
            fixture.Saga,
            fixture.StateType,
            fixture.WorkflowId,
            fixture.Logger,
            trigger);
    }

    private static List<object> Materialize(object value)
    {
        var result = new List<object>();
        foreach (var item in (System.Collections.IEnumerable)value)
        {
            result.Add(item!);
        }

        return result;
    }

    private static string CreateExecutableTypedWorkflow() => """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Steps;

        namespace RuntimeCompensationExecution;

        public sealed class RuntimeOrder
        {
            public int Stage { get; set; }
        }

        public sealed class RuntimeOrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<RuntimeOrder>("Order", obj =>
                {
                    obj.Action("execute")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 3)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("runtime-order");
                    obj.Action("a")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-a")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 0)
                        .Modifies(order => order.Stage);
                    obj.Action("b")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-b")
                        .Requires(order => order.Stage == 2)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    obj.Action("c")
                        .Requires(order => order.Stage == 2)
                        .Ensures(order => order.Stage == 3)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-c")
                        .Requires(order => order.Stage == 3)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                });
            }
        }

        [WorkflowState]
        public sealed record RuntimeState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }

            public int Stage { get; init; }

            public string Trace { get; init; } = string.Empty;
        }

        public class RuntimeStep : IWorkflowStep<RuntimeState>
        {
            public Task<StepResult<RuntimeState>> ExecuteAsync(
                RuntimeState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<RuntimeState>.FromState(state));
        }

        public sealed class AStep : RuntimeStep { }
        public sealed class BStep : RuntimeStep { }
        public sealed class CStep : RuntimeStep { }
        public sealed class UndoAStep : RuntimeStep { }
        public sealed class UndoBStep : RuntimeStep { }
        public sealed class UndoCStep : RuntimeStep { }

        [Workflow("runtime-order")]
        public static partial class RuntimeOrderWorkflowDefinition
        {
            public static WorkflowDefinition<RuntimeState> Definition => Workflow<RuntimeState>
                .Create("runtime-order")
                .StartWith<AStep>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "a"))
                    .Compensate<UndoAStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-a")))
                .Then<BStep>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "b"))
                    .Compensate<UndoBStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-b")))
                .Finally<CStep>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "c"))
                    .Compensate<UndoCStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-c"))
                    .WithTimeout(TimeSpan.FromSeconds(5)));
        }
        """;

    private static string CreateExecutableTypedValidationWorkflow() =>
        CreateExecutableTypedWorkflow().Replace(
            ".WithTimeout(TimeSpan.FromSeconds(5)))",
            ".ValidateState(state => state.Stage > 100, \"stage rejected\"))",
            StringComparison.Ordinal);

    private static string CreateExecutableReducerFailureWorkflow() =>
        CreateExecutableTypedWorkflow()
            .Replace(
                "[WorkflowState]\npublic sealed record RuntimeState",
                "public readonly record struct RuntimePhaseValue(bool IsFailed)\n"
                + "{\n"
                + "    public static implicit operator RuntimeOrderPhase(RuntimePhaseValue value) =>\n"
                + "        value.IsFailed ? RuntimeOrderPhase.Failed : RuntimeOrderPhase.NotStarted;\n"
                + "}\n\n"
                + "[WorkflowState]\n"
                + "public sealed record RuntimeState",
                StringComparison.Ordinal)
            .Replace(
                "public string Trace { get; init; } = string.Empty;",
                "public string Trace { get; init; } = string.Empty;\n\n"
                + "            public RuntimePhaseValue Phase { get; init; }",
                StringComparison.Ordinal)
            .Replace(
                "public sealed class UndoCStep : RuntimeStep { }",
                "public sealed class UndoCStep : RuntimeStep { }\n"
                + "        public sealed class NotifyFailureStep : RuntimeStep { }",
                StringComparison.Ordinal)
            .Replace(
                ".Finally<CStep>",
                ".OnFailure(flow => flow\n"
                + "            .Then<NotifyFailureStep>(step => step.Performs(\n"
                + "                new WorkflowActionReference(\"orders\", \"Order\", \"a\")))\n"
                + "            .Complete())\n"
                + "        .Finally<CStep>",
                StringComparison.Ordinal);

    private static string CreateExecutableReducerFailureForkWorkflow() =>
        CreateTypedForkCompilationWorkflow()
            .Replace(
                "[WorkflowState]\npublic sealed record ForkState",
                "public readonly record struct ForkPhaseValue(bool IsFailed)\n"
                + "{\n"
                + "    public static implicit operator ForkOrderPhase(ForkPhaseValue value) =>\n"
                + "        value.IsFailed ? ForkOrderPhase.Failed : ForkOrderPhase.NotStarted;\n"
                + "}\n\n"
                + "[WorkflowState]\n"
                + "public sealed record ForkState",
                StringComparison.Ordinal)
            .Replace(
                "public int Value { get; init; }",
                "public int Value { get; init; }\n\n"
                + "    public ForkPhaseValue Phase { get; init; }",
                StringComparison.Ordinal)
            .Replace(
                "public sealed class UndoRightStep : ForkStep { }",
                "public sealed class UndoRightStep : ForkStep { }\n"
                + "public sealed class UndoStartStep : ForkStep { }",
                StringComparison.Ordinal)
            .Replace(
                ".StartWith<StartStep>()",
                ".StartWith<StartStep>(step => step.Compensate<UndoStartStep>(\n"
                + "        new WorkflowActionReference(\"orders\", \"Order\", \"undo-start\")))",
                StringComparison.Ordinal);

    private static string CreateExecutableReducerFailureDiagnosticForkWorkflow() =>
        CreateExecutableReducerFailureWorkflow()
            .Replace(
                ".Finally<CStep>",
                ".AllowDiagnosticFork(fork => fork\n"
                + "            .Anchor(\"BStep\")\n"
                + "            .PermitTrigger(Strategos.Contracts.Generated.ForkTrigger.OperatorExplicit, \"reason\")\n"
                + "            .WithCompensationSeed(\"BStep\")\n"
                + "            .MaxForks(2))\n"
                + "        .Finally<CStep>",
                StringComparison.Ordinal);

    private static string CreateExecutableApprovalLifecycleWorkflow() =>
        CreateExecutableTypedWorkflow()
            .Replace(
                "public sealed class UndoCStep : RuntimeStep { }",
                "public sealed class UndoCStep : RuntimeStep { }\n"
                + "        public sealed class Reviewer { }\n"
                + "        public sealed class EscalateStep : RuntimeStep { }",
                StringComparison.Ordinal)
            .Replace(
                ".Then<BStep>",
                ".AwaitApproval<Reviewer>(approval => approval\n"
                + "            .WithContext(\"review\")\n"
                + "            .OnTimeout(escalation => escalation\n"
                + "                .Then<EscalateStep>(step => step.Performs(\n"
                + "                    new WorkflowActionReference(\"orders\", \"Order\", \"a\")))\n"
                + "                .Complete()))\n"
                + "        .Then<BStep>",
                StringComparison.Ordinal);

    private static string CreateExecutableReducerFailureLoopWorkflow() => """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace RuntimeLoopCompensationExecution;

        public readonly record struct LoopPhaseValue(bool IsFailed)
        {
            public static implicit operator LoopOrderPhase(LoopPhaseValue value) =>
                value.IsFailed ? LoopOrderPhase.Failed : LoopOrderPhase.NotStarted;
        }

        [WorkflowState]
        public sealed record LoopState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }

            public int Value { get; init; }

            public bool Done { get; init; }

            public LoopPhaseValue Phase { get; init; }
        }

        public class LoopStep : IWorkflowStep<LoopState>
        {
            public Task<StepResult<LoopState>> ExecuteAsync(
                LoopState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<LoopState>.FromState(state));
        }

        public sealed class EntryStep : LoopStep { }
        public sealed class BodyStep : LoopStep { }
        public sealed class AfterStep : LoopStep { }
        public sealed class UndoEntryStep : LoopStep { }
        public sealed class UndoBodyStep : LoopStep { }

        [Workflow("loop-order")]
        public static partial class LoopOrderWorkflowDefinition
        {
            public static WorkflowDefinition<LoopState> Definition => Workflow<LoopState>
                .Create("loop-order")
                .StartWith<EntryStep>(step => step.Compensate<UndoEntryStep>(
                    new WorkflowActionReference("orders", "Order", "undo-entry")))
                .RepeatUntil(
                    state => state.Done,
                    "Retry",
                    loop => loop.Then<BodyStep>(step => step.Compensate<UndoBodyStep>(
                        new WorkflowActionReference("orders", "Order", "undo-body"))),
                    maxIterations: 2)
                .Finally<AfterStep>();
        }
        """;

    private static string CreateTypedThreeLaneForkCompilationWorkflow() =>
        CreateTypedForkCompilationWorkflow()
            .Replace(
                "public sealed class RightStep : ForkStep { }",
                "public sealed class RightStep : ForkStep { }\n"
                + "public sealed class ThirdStep : ForkStep { }",
                StringComparison.Ordinal)
            .Replace(
                "public sealed class UndoRightStep : ForkStep { }",
                "public sealed class UndoRightStep : ForkStep { }\n"
                + "public sealed class UndoThirdStep : ForkStep { }",
                StringComparison.Ordinal)
            .Replace(
                "new WorkflowActionReference(\"orders\", \"Order\", \"undo-right\"))))",
                "new WorkflowActionReference(\"orders\", \"Order\", \"undo-right\"))),\n"
                + "    path => path.Then<ThirdStep>(step => step.Compensate<UndoThirdStep>(\n"
                + "        new WorkflowActionReference(\"orders\", \"Order\", \"undo-third\"))))",
                StringComparison.Ordinal);

    private static string CreateExecutableNestedBranchWorkflow() => """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Steps;

        namespace RuntimeNestedCompensationExecution;

        public enum NestedRoute
        {
            Selected = 0,
            Alternate = 1,
        }

        public sealed class NestedOrder
        {
            public int Stage { get; set; }
        }

        public sealed class NestedOrdersOntology : DomainOntology
        {
            public override string DomainName => "nested-orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<NestedOrder>("Order", obj =>
                {
                    obj.Action("execute")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 3)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("nested-order");
                    obj.Action("outer-a")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-outer-a")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 0)
                        .Modifies(order => order.Stage);
                    obj.Action("inner-b")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-inner-b")
                        .Requires(order => order.Stage == 2)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    obj.Action("inner-c")
                        .Requires(order => order.Stage == 2)
                        .Ensures(order => order.Stage == 3)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-inner-c")
                        .Requires(order => order.Stage == 3)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                    obj.Action("alternate")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 3)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-alternate")
                        .Requires(order => order.Stage == 3)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    obj.Action("finish")
                        .Requires(order => order.Stage == 3)
                        .Ensures(order => order.Stage == 3);
                });
            }
        }

        [WorkflowState]
        public sealed record NestedState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }

            public int Stage { get; init; }

            public string Trace { get; init; } = string.Empty;

            public NestedRoute Route { get; init; }
        }

        public class NestedStep : IWorkflowStep<NestedState>
        {
            public Task<StepResult<NestedState>> ExecuteAsync(
                NestedState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<NestedState>.FromState(state));
        }

        public sealed class OuterAStep : NestedStep { }
        public sealed class InnerBStep : NestedStep { }
        public sealed class InnerCStep : NestedStep { }
        public sealed class AlternateStep : NestedStep { }
        public sealed class FinishStep : NestedStep { }
        public sealed class UndoOuterAStep : NestedStep { }
        public sealed class UndoInnerBStep : NestedStep { }
        public sealed class UndoInnerCStep : NestedStep { }
        public sealed class UndoAlternateStep : NestedStep { }

        [Workflow("nested-order")]
        public static partial class NestedOrderWorkflowDefinition
        {
            public static WorkflowDefinition<NestedState> Definition => Workflow<NestedState>
                .Create("nested-order")
                .StartWith<OuterAStep>(step => step
                    .Performs(new WorkflowActionReference("nested-orders", "Order", "outer-a"))
                    .Compensate<UndoOuterAStep>(new WorkflowActionReference(
                        "nested-orders", "Order", "undo-outer-a")))
                .Branch(
                    state => state.Route,
                    BranchCase<NestedState, NestedRoute>.When(
                        NestedRoute.Selected,
                        path => path
                            .Then<InnerBStep>(step => step
                                .Performs(new WorkflowActionReference("nested-orders", "Order", "inner-b"))
                                .Compensate<UndoInnerBStep>(new WorkflowActionReference(
                                    "nested-orders", "Order", "undo-inner-b")))
                            .Then<InnerCStep>(step => step
                                .Performs(new WorkflowActionReference("nested-orders", "Order", "inner-c"))
                                .Compensate<UndoInnerCStep>(new WorkflowActionReference(
                                    "nested-orders", "Order", "undo-inner-c")))),
                    BranchCase<NestedState, NestedRoute>.Otherwise(
                        path => path.Then<AlternateStep>(step => step
                            .Performs(new WorkflowActionReference("nested-orders", "Order", "alternate"))
                            .Compensate<UndoAlternateStep>(new WorkflowActionReference(
                                "nested-orders", "Order", "undo-alternate")))))
                .Finally<FinishStep>(step => step.Performs(
                    new WorkflowActionReference("nested-orders", "Order", "finish")));
        }
        """;

    private static string CreateExecutableNestedBranchApprovalWorkflow() =>
        CreateExecutableNestedBranchWorkflow()
            .Replace(
                "public sealed class UndoAlternateStep : NestedStep { }",
                "public sealed class UndoAlternateStep : NestedStep { }\npublic sealed class Reviewer { }",
                StringComparison.Ordinal)
            .Replace(
                ".Then<InnerCStep>",
                ".AwaitApproval<Reviewer>(approval => approval.WithContext(\"review\"))\n                            .Then<InnerCStep>",
                StringComparison.Ordinal);

    private static string CreateLegacyCompilationWorkflow() => """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace LegacyCompensationCompilation;

        [WorkflowState]
        public sealed record LegacyState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class LegacyStep : IWorkflowStep<LegacyState>
        {
            public Task<StepResult<LegacyState>> ExecuteAsync(
                LegacyState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<LegacyState>.FromState(state));
        }

        public sealed class ChargeStep : LegacyStep { }
        public sealed class FinishStep : LegacyStep { }
        public sealed class RefundStep : LegacyStep { }

        [Workflow("legacy-order")]
        public static partial class LegacyOrderWorkflowDefinition
        {
            public static WorkflowDefinition<LegacyState> Definition => Workflow<LegacyState>
                .Create("legacy-order")
                .StartWith<ChargeStep>(step => step.Compensate<RefundStep>())
                .Finally<FinishStep>();
        }
        """;

    private static string CreateTypedForkCompilationWorkflow() => """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TypedForkCompensationCompilation;

        [WorkflowState]
        public sealed record ForkState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }

            public int Value { get; init; }
        }

        public class ForkStep : IWorkflowStep<ForkState>
        {
            public Task<StepResult<ForkState>> ExecuteAsync(
                ForkState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<ForkState>.FromState(state));
        }

        public sealed class StartStep : ForkStep { }
        public sealed class LeftStep : ForkStep { }
        public sealed class RightStep : ForkStep { }
        public sealed class JoinStep : ForkStep { }
        public sealed class FinishStep : ForkStep { }
        public sealed class UndoLeftStep : ForkStep { }
        public sealed class UndoRightStep : ForkStep { }

        [Workflow("fork-order")]
        public static partial class ForkOrderWorkflowDefinition
        {
            public static WorkflowDefinition<ForkState> Definition => Workflow<ForkState>
                .Create("fork-order")
                .StartWith<StartStep>()
                .Fork(
                    path => path.Then<LeftStep>(step => step.Compensate<UndoLeftStep>(
                        new WorkflowActionReference("orders", "Order", "undo-left"))),
                    path => path.Then<RightStep>(step => step.Compensate<UndoRightStep>(
                        new WorkflowActionReference("orders", "Order", "undo-right"))))
                .Join<JoinStep>()
                .Finally<FinishStep>();
        }
        """;

    private static string CreateTypedCompilationWorkflow() => """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Steps;

        namespace TypedCompensationCompilation;

        public sealed class Order
        {
            public int Stage { get; set; }
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                builder.Object<Order>("Order", obj =>
                {
                    obj.Action("fulfill")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage)
                        .BoundToWorkflow("fulfill-order");
                    obj.Action("receive")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-receive")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 0)
                        .Modifies(order => order.Stage);
                    obj.Action("complete")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 2)
                        .Modifies(order => order.Stage);
                    obj.Action("undo-complete")
                        .Requires(order => order.Stage == 2)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                });
            }
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }

            public FlowState ApplyEvent<TEvent>(TEvent evt) => this;
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class ReceiveStep : TestStep { }
        public sealed class CompleteStep : TestStep { }
        public sealed class UndoReceiveStep : TestStep { }
        public sealed class UndoCompleteStep : TestStep { }

        [Workflow("fulfill-order")]
        public static partial class FulfillOrderWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("fulfill-order")
                .StartWith<ReceiveStep>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "receive"))
                    .Compensate<UndoReceiveStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-receive")))
                .Finally<CompleteStep>(step => step
                    .Performs(new WorkflowActionReference("orders", "Order", "complete"))
                    .Compensate<UndoCompleteStep>(new WorkflowActionReference(
                        "orders", "Order", "undo-complete")));
        }
        """;
}
