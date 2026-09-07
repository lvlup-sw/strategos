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
        await Assert.That(saga).Contains("CompensationJournalSchemaVersion = 1");
        await Assert.That(saga).Contains("if (CompensationJournalSchemaVersion != 1)");
        await Assert.That(saga).Contains("RollbackId = forwardExecutionId");
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

    /// <summary>Malformed inverse metadata cannot recurse or emit forward completion.</summary>
    [Test]
    public async Task Emit_TypedInverseWithMalformedMetadata_UsesRollbackFailureRouteOnly()
    {
        var handlers = WorkerHandlerEmitter.Emit(CreateLinearModel());

        await Assert.That(handlers).Contains("(cmd, ex, bus) => cmd.IsCompensation");
        await Assert.That(handlers).DoesNotContain("cmd.IsCompensation\n            && cmd.RollbackId");
        await Assert.That(handlers).Contains("cmd.RollbackId ?? cmd.StepExecutionId");
        await Assert.That(handlers).Contains("cmd.RollbackJournalSequence ?? -1L");
        await Assert.That(handlers).Contains("Malformed inverse metadata must never fall through to forward completion");
        await Assert.That(handlers).Contains("Inverse command is missing durable rollback metadata.");
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
        await Assert.That(failureRegion).Contains("entry.Status is \"RolledBack\" or \"Failed\" or \"OutcomeUnknown\"");
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
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(CreateTypedCompilationWorkflow());
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "FulfillOrderSaga.g.cs");

        await Assert.That(saga).Contains("using System.Linq;");
        await Assert.That(saga).Contains("CompensationJournalEntry");
        await Assert.That(saga).Contains("switch (forkId, pathIndex)");
        await Assert.That(saga).Contains("default:\n                break;");
        await Assert.That(saga).DoesNotContain("switch (forkId, pathIndex)\n        {\n        }");
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

    /// <summary>Event-sourced typed rollback compiles with its durable Marten handler path.</summary>
    [Test]
    public async Task Emit_EventSourcedTypedProgram_FullGeneratedOutputCompiles()
    {
        var source = CreateTypedCompilationWorkflow().Replace(
            "[Workflow(\"fulfill-order\")]",
            "[Workflow(\"fulfill-order\", Persistence = PersistenceMode.EventSourced)]",
            StringComparison.Ordinal);
        var result = GeneratorTestHelper.RunGeneratorWithValidInput(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "FulfillOrderSaga.g.cs");
        var region = RollbackCompletionRegion(saga, "FulfillOrderUndoReceiveStepRollbackCompleted evt");

        await Assert.That(region).Contains("IDocumentSession session,");
        await Assert.That(region).Contains("session.Events.Append(WorkflowId, evt);");
        await Assert.That(region).Contains("State = State.ApplyEvent(evt);");
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

        _ = InvokeHandle(saga, startA, logger);
        var aCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.AStepCompleted"),
            workflowId,
            Guid.NewGuid(),
            CreateState(stateType, workflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startB = Materialize(InvokeHandle(saga, aCompleted, logger)).Single();
        _ = InvokeHandle(saga, startB, logger);
        var bCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.BStepCompleted"),
            workflowId,
            Guid.NewGuid(),
            CreateState(stateType, workflowId, stage: 2, trace: "A;B"),
            null,
            DateTimeOffset.UtcNow);
        var startC = Materialize(InvokeHandle(saga, bCompleted, logger)).Single();
        var cWorker = InvokeHandle(saga, startC, logger);

        var trigger = CreateMessage(
            RequiredType(assembly, "RuntimeCompensationExecution.TriggerRuntimeOrderFailureHandlerCommand"),
            workflowId,
            "CStep",
            "boom",
            "InvalidOperationException",
            null);
        CopyProperty(cWorker, trigger, "ForwardOccurrenceKey");
        CopyProperty(cWorker, trigger, "CompensationScopeKey");
        CopyProperty(cWorker, trigger, "CompensationScopeKind");
        CopyProperty(cWorker, trigger, "CompensationLaneKey");
        CopyProperty(cWorker, trigger, "CompensationForkId");
        CopyProperty(cWorker, trigger, "CompensationForkPathIndex");
        CopyProperty(cWorker, trigger, "CompensationJournalSequenceAtDispatch");

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

        _ = InvokeHandle(saga, startOuterA, logger);
        var outerCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.OuterAStepCompleted"),
            workflowId,
            Guid.NewGuid(),
            CreateState(stateType, workflowId, stage: 1, trace: "A"),
            null,
            DateTimeOffset.UtcNow);
        var startInnerB = InvokeHandle(saga, outerCompleted, logger);
        _ = InvokeHandle(saga, startInnerB, logger);
        var innerBCompleted = CreateMessage(
            RequiredType(assembly, "RuntimeNestedCompensationExecution.InnerBStepCompleted"),
            workflowId,
            Guid.NewGuid(),
            CreateState(stateType, workflowId, stage: 2, trace: "A;B"),
            null,
            DateTimeOffset.UtcNow);
        var startInnerC = Materialize(InvokeHandle(saga, innerBCompleted, logger)).Single();
        var innerCWorker = InvokeHandle(saga, startInnerC, logger);
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

    private static WorkflowModel CreateLinearModel()
    {
        var steps = new List<StepModel>
        {
            TypedStep("A", "UndoA", "a", "undo-a"),
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

    private static WorkflowModel CreateForkModel()
    {
        var left = TypedStep("Left", "UndoLeft", "left", "undo-left");
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

    private static Assembly CompileGeneratedAssembly(string source)
    {
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
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
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

    private static object CreateState(Type stateType, Guid workflowId, int stage, string trace)
    {
        var state = Activator.CreateInstance(stateType)!;
        stateType.GetProperty("WorkflowId")!.SetValue(state, workflowId);
        stateType.GetProperty("Stage")!.SetValue(state, stage);
        stateType.GetProperty("Trace")!.SetValue(state, trace);
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
    }

    private static T GetProperty<T>(object target, string propertyName) =>
        (T)target.GetType().GetProperty(propertyName)!.GetValue(target)!;

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
                        "orders", "Order", "undo-c")));
        }
        """;

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
