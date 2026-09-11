// -----------------------------------------------------------------------
// <copyright file="ImportRejectionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;
using System.Threading;

using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// Task 018 (#100) — DR-14 (rejection half) + DR-2 (import-channel machine-check) + DR-3 (dangling
/// gateId). Drives <see cref="WorkflowIncrementalGenerator"/> over <c>*.workflow.json</c>
/// <c>AdditionalFiles</c> and pins that every runtime-bindable CARRIER and every SEMANTIC violation
/// is rejected LOUDLY: each gets its OWN stable diagnostic that NAMES the construct + its JSON path,
/// and NO saga is emitted for that workflow. A well-declared gate (a resolvable back-reference, no
/// reliability) is the negative control — it must NOT be rejected and must still lower a saga, so the
/// scan is proven additive rather than over-broad.
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class ImportRejectionTests
{
    // Stable AGWF ids under test. Literal ids are permitted in tests (the single-source grep gate
    // excludes *.Tests projects); production C# routes through the generated AgwfCodes constants.
    private const string DelegateCode = "AGWF027";
    private const string BranchPointCode = "AGWF028";
    private const string LoopCode = "AGWF029";
    private const string ValidationCode = "AGWF030";
    private const string ApprovalContextCode = "AGWF031";
    private const string DanglingGateIdCode = "AGWF032";
    private const string ReliabilityGateCode = "AGWF033";
    private const string ForkTriggerEvidenceCode = "AGWF034";
    private const string DuplicatePermittedForkTriggerCode = "AGWF037";
    private const string DuplicateCompensationSeedCode = "AGWF038";
    private const string WorkflowContractUnprovableCode = "AGWF042";

    /// <summary>
    /// Real step types so a NON-rejected import can resolve its monikers and lower a saga — the
    /// rejection scan runs before moniker resolution, so the rejected cases do not depend on these.
    /// </summary>
    private const string StepTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Steps;

        namespace RejectNs;

        [WorkflowState]
        public sealed record RejectState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class RejectStepA : IWorkflowStep<RejectState>
        {
            public Task<StepResult<RejectState>> ExecuteAsync(RejectState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RejectState>.FromState(s));
        }

        public sealed class RejectStepB : IWorkflowStep<RejectState>
        {
            public Task<StepResult<RejectState>> ExecuteAsync(RejectState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RejectState>.FromState(s));
        }

        public sealed class RejectStepC : IWorkflowStep<RejectState>
        {
            public Task<StepResult<RejectState>> ExecuteAsync(RejectState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<RejectState>.FromState(s));
        }
        """;

    // A delegate (lambda) step at $.steps[1] (LB-1 carrier).
    private const string DelegateJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-delegate",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "delegate", "stepId": "d1", "stepName": "InlineLog", "isTerminal": true, "lambda": true }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "d1"
        }
        """;

    // A branch point at $.branchPoints[0] (conditional fan-out carrier).
    private const string BranchPointJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-branch",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "loops": [], "forkPoints": [],
          "branchPoints": [
            {
              "branchPointId": "b1",
              "fromStepId": "s1",
              "paths": [
                { "pathId": "bp0", "conditionDescription": "amount > 100", "steps": [ { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" } ], "isTerminal": true }
              ]
            }
          ],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A loop (RepeatUntil) at $.loops[0] (runtime-bound exit-condition carrier).
    private const string LoopJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-loop",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "branchPoints": [], "forkPoints": [],
          "loops": [
            {
              "loopId": "l1",
              "loopName": "Retry",
              "fromStepId": "s1",
              "maxIterations": 3,
              "bodySteps": [ { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": false, "stepType": "RejectStepB" } ]
            }
          ],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A validation predicate at $.steps[0].configuration.validation (LB-1 predicate carrier).
    private const string ValidationJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-validation",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA",
              "configuration": { "validation": { "predicateExpression": "state.Amount > 0", "errorMessage": "Amount must be positive" } } },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A context-bearing approval at $.approvalPoints[0] (task-024 hasContext marker).
    private const string ApprovalContextJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-approval-context",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [
            { "approvalPointId": "ap1", "approverType": "RejectStepC", "precedingStepId": "s1", "hasContext": true }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A root failure handler whose executable recovery steps are not carried by the import bridge.
    private const string RootFailureHandlerJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-root-failure-handler",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [
            {
              "handlerId": "root-recovery",
              "scope": "workflow",
              "steps": [
                { "kind": "handler", "stepId": "r1", "stepName": "RejectStepC", "isTerminal": true, "stepType": "RejectStepC" }
              ],
              "isTerminal": true
            }
          ],
          "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A fork-path failure handler currently reduced to flags by MapForks, losing its recovery steps.
    private const string ForkPathFailureHandlerJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-fork-path-failure-handler",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s4", "stepName": "RejectStepC", "isTerminal": true, "stepType": "RejectStepC" }
          ],
          "transitions": [], "branchPoints": [], "loops": [],
          "forkPoints": [
            {
              "forkPointId": "fork-1",
              "fromStepId": "s1",
              "joinStepId": "s4",
              "paths": [
                {
                  "pathId": "primary",
                  "pathIndex": 0,
                  "steps": [
                    { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": false, "stepType": "RejectStepB" }
                  ],
                  "failureHandler": {
                    "handlerId": "path-recovery",
                    "scope": "forkPath",
                    "steps": [
                      { "kind": "handler", "stepId": "r1", "stepName": "RejectStepC", "isTerminal": true, "stepType": "RejectStepC" }
                    ],
                    "isTerminal": false
                  }
                },
                {
                  "pathId": "secondary",
                  "pathIndex": 1,
                  "steps": [
                    { "kind": "skill", "stepId": "s3", "stepName": "RejectStepC", "isTerminal": false, "stepType": "RejectStepC" }
                  ]
                }
              ]
            }
          ],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s4"
        }
        """;

    // An approval step nested in a low-confidence handler used to make MapConfidence silently
    // discard the handler because ApprovalStep has no executable step moniker.
    private const string LowConfidenceApprovalStepJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-low-confidence-approval-step",
          "steps": [
            {
              "kind": "skill",
              "stepId": "s1",
              "stepName": "RejectStepA",
              "isTerminal": false,
              "stepType": "RejectStepA",
              "configuration": {
                "confidenceThreshold": 0.75,
                "onLowConfidence": {
                  "handlerId": "low-confidence-handler",
                  "handlerSteps": [
                    {
                      "kind": "approval",
                      "stepId": "low-confidence-approval",
                      "stepName": "ManualReview",
                      "isTerminal": true,
                      "approverType": "RejectStepC"
                    }
                  ],
                  "isTerminal": true
                }
              }
            },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A gate step whose gateId ("gX") is absent from gates[] — a DR-3 dangling reference.
    private const string DanglingGateJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-dangling-gate",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "gate", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB", "gateId": "gX" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "gates": [ { "class": "rules", "id": "g1" } ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A gate declaration carrying a reliability block at $.gates[0].reliability — a DR-2 violation.
    private const string ReliabilityGateJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-reliability-gate",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "gates": [
            { "class": "rules", "id": "g1",
              "reliability": { "fpr": 0.02, "sampleSize": 500, "asOf": "2026-01-01T00:00:00Z", "source": "telemetry" } }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // A diagnostic-fork permitted trigger declaring an EMPTY requiredEvidenceFields at
    // $.diagnosticForks[0].permittedTriggers[0] — the DR-8 evidence-floor violation (wire
    // @minItems(1)). Absent the AGWF034 rejection, MapDiagnosticForks copies the empty list into
    // PermittedForkTriggerModel.Create, which enforces the floor by THROWING — an unhandled throw
    // that crashes the whole generator (CS8785) and drops ALL output for the compilation. The rest
    // of the workflow is a valid linear import (a NON-empty-evidence twin lowers cleanly — the
    // negative control), so the empty evidence floor is the sole reason this one is rejected.
    private const string ForkEmptyEvidenceJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-fork-empty-evidence",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "diagnosticForks": [
            {
              "anchorStepIds": [ "RejectStepA" ],
              "permittedTriggers": [ { "trigger": "ratification_failure", "requiredEvidenceFields": [] } ],
              "maxForks": 2,
              "compensationSeed": "RejectStepB"
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // Negative control: a diagnostic-fork permitted trigger with a NON-EMPTY requiredEvidenceFields
    // is tolerated (DR-8 floor satisfied) — no AGWF034 — and still lowers a saga.
    private const string ForkWithEvidenceJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-fork-with-evidence-ok",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "diagnosticForks": [
            {
              "anchorStepIds": [ "RejectStepA" ],
              "permittedTriggers": [ { "trigger": "ratification_failure", "requiredEvidenceFields": [ "stampId" ] } ],
              "maxForks": 2,
              "compensationSeed": "RejectStepB"
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // Two permittedTriggers entries naming the same closed trigger on one edge, with
    // DIFFERENT evidence schemas — the #156.2 case. First-wins dedup would silently
    // drop one schema; AGWF037 must reject the workflow instead.
    private const string ForkDuplicateTriggerJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-fork-duplicate-trigger",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "diagnosticForks": [
            {
              "anchorStepIds": [ "RejectStepA" ],
              "permittedTriggers": [
                { "trigger": "ratification_failure", "requiredEvidenceFields": [ "stampId" ] },
                { "trigger": "ratification_failure", "requiredEvidenceFields": [ "otherStampId" ] }
              ],
              "maxForks": 2,
              "compensationSeed": "RejectStepB"
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // Two diagnostic-fork edges that share a compensation seed — the #156.3 case.
    // Sharing a DiagnosticForkCount_{seed} counter is rejected as AGWF038.
    private const string ForkDuplicateSeedJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-fork-duplicate-seed",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "diagnosticForks": [
            {
              "anchorStepIds": [ "RejectStepA" ],
              "permittedTriggers": [
                { "trigger": "ratification_failure", "requiredEvidenceFields": [ "stampId" ] }
              ],
              "maxForks": 2,
              "compensationSeed": "RejectStepB"
            },
            {
              "anchorStepIds": [ "RejectStepB" ],
              "permittedTriggers": [
                { "trigger": "gate_contradiction", "requiredEvidenceFields": [ "leftGateId", "rightGateId" ] }
              ],
              "maxForks": 1,
              "compensationSeed": "RejectStepB"
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // Negative control: two DISTINCT triggers on one edge stay clean — no AGWF037.
    private const string ForkDistinctTriggersJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-fork-distinct-triggers-ok",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "skill", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": true, "stepType": "RejectStepB" }
          ],
          "transitions": [ { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": false } ],
          "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "diagnosticForks": [
            {
              "anchorStepIds": [ "RejectStepA" ],
              "permittedTriggers": [
                { "trigger": "ratification_failure", "requiredEvidenceFields": [ "stampId" ] },
                { "trigger": "gate_contradiction", "requiredEvidenceFields": [ "leftGateId", "rightGateId" ] }
              ],
              "maxForks": 2,
              "compensationSeed": "RejectStepB"
            }
          ],
          "entryStepId": "s1", "terminalStepId": "s2"
        }
        """;

    // Negative control: a well-declared gate (resolvable gateId, no reliability) — DR-3 tolerated.
    private const string WellDeclaredGateJson = """
        {
          "schemaVersion": "1.0",
          "name": "reject-gate-ok",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "RejectStepA", "isTerminal": false, "stepType": "RejectStepA" },
            { "kind": "gate", "stepId": "s2", "stepName": "RejectStepB", "isTerminal": false, "stepType": "RejectStepB", "gateId": "g1" },
            { "kind": "skill", "stepId": "s3", "stepName": "RejectStepC", "isTerminal": true, "stepType": "RejectStepC" }
          ],
          "transitions": [], "branchPoints": [], "loops": [], "forkPoints": [],
          "failureHandlers": [], "approvalPoints": [],
          "gates": [ { "class": "rules", "id": "g1" } ],
          "entryStepId": "s1", "terminalStepId": "s3"
        }
        """;

    /// <summary>A delegate (lambda) step is rejected with AGWF027 naming the step + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DelegateStep_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-delegate.workflow.json", DelegateJson),
            DelegateCode);
        await AssertRejected(result, DelegateCode, "$.steps[1]", "d1");
    }

    /// <summary>A branch point is rejected with AGWF028 naming the branch point + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchPoint_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-branch.workflow.json", BranchPointJson),
            BranchPointCode);
        await AssertRejected(result, BranchPointCode, "$.branchPoints[0]", "b1");
    }

    /// <summary>A loop is rejected with AGWF029 naming the loop + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Loop_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-loop.workflow.json", LoopJson),
            LoopCode);
        await AssertRejected(result, LoopCode, "$.loops[0]", "Retry");
    }

    /// <summary>A validation predicate is rejected with AGWF030 naming the step + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidationPredicate_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-validation.workflow.json", ValidationJson),
            ValidationCode);
        await AssertRejected(result, ValidationCode, "$.steps[0].configuration.validation", "s1");
    }

    /// <summary>A context-bearing approval is rejected with AGWF031 naming the approval + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ApprovalWithContext_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-approval-context.workflow.json", ApprovalContextJson),
            ApprovalContextCode);
        await AssertRejected(result, ApprovalContextCode, "$.approvalPoints[0]", "ap1");
    }

    /// <summary>An unbound root failure handler preserves the existing import/runtime contract.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RootFailureHandler_WhenUnbound_RemainsImportable()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-root-failure-handler.workflow.json", RootFailureHandlerJson));

        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(d => d.Id == WorkflowContractUnprovableCode))
            .IsFalse()
            .Because("ordinary import fidelity remains available when no action requests a closed proof.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("root failure-handler imports retain their existing best-effort runtime lowering.");
    }

    /// <summary>A bound imported root handler marks the proof topology unresolved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RootFailureHandler_WhenWorkflowBound_ReportsAgwf042AndStillLowersSaga()
    {
        const string boundDescriptor = """

            public static class ImportedRootBinding
            {
                public static readonly Strategos.Ontology.Descriptors.ActionDescriptor Value = new(
                    new Strategos.Ontology.Descriptors.ActionSubject("orders", "Order"),
                    "recoverable-flow",
                    "")
                {
                    BindingType = Strategos.Ontology.Descriptors.ActionBindingType.Workflow,
                    BoundWorkflow = new Strategos.Ontology.Descriptors.WorkflowBindingReference(
                        "reject-root-failure-handler"),
                };
            }
            """;
        var result = RunGenerator(
            StepTypes + boundDescriptor,
            ("reject-root-failure-handler.workflow.json", RootFailureHandlerJson),
            WorkflowContractUnprovableCode);

        var errors = ErrorDiagnostics(result);
        await Assert.That(errors).HasCount().EqualTo(1)
            .Because($"a bound unsupported root handler must fail exclusively with {WorkflowContractUnprovableCode}.");
        var diagnostic = errors.SingleOrDefault(d => d.Id == WorkflowContractUnprovableCode);
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.GetMessage()).Contains("$.failureHandlers[0]");
        await Assert.That(diagnostic.GetMessage()).Contains("root-recovery");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("proof fails closed without changing the ordinary import/runtime lowering contract.");
    }

    /// <summary>
    /// A fork-path failure handler is rejected with AGWF042 instead of lowering only its Boolean
    /// flags and silently discarding the executable recovery steps.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkPathFailureHandler_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-fork-path-failure-handler.workflow.json", ForkPathFailureHandlerJson),
            WorkflowContractUnprovableCode);

        await AssertRejected(
            result,
            WorkflowContractUnprovableCode,
            "$.forkPoints[0].paths[0].failureHandler",
            "path-recovery");
    }

    /// <summary>
    /// An approval step nested in a low-confidence handler is rejected rather than silently
    /// removing the complete handler during import lowering.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LowConfidenceApprovalStep_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-low-confidence-approval-step.workflow.json", LowConfidenceApprovalStepJson),
            WorkflowContractUnprovableCode);

        await AssertRejected(
            result,
            WorkflowContractUnprovableCode,
            "$.steps[0].configuration.onLowConfidence.handlerSteps[0]",
            "low-confidence-approval");
    }

    /// <summary>A dangling gateId is rejected with AGWF032 naming the gate id + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DanglingGateId_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-dangling-gate.workflow.json", DanglingGateJson),
            DanglingGateIdCode);
        await AssertRejected(result, DanglingGateIdCode, "$.steps[1].gateId", "gX");
    }

    /// <summary>A reliability-bearing gate declaration is rejected with AGWF033 naming the gate + its JSON path; no saga.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReliabilityBearingGate_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-reliability-gate.workflow.json", ReliabilityGateJson),
            ReliabilityGateCode);
        await AssertRejected(result, ReliabilityGateCode, "$.gates[0].reliability", "g1");
    }

    /// <summary>
    /// A diagnostic-fork permitted trigger with an EMPTY <c>requiredEvidenceFields</c> is rejected
    /// with AGWF034 naming the trigger + its JSON path; no saga. This closes the DR-8 evidence floor
    /// on the import channel — the wire <c>@minItems(1)</c> that the builder enforces but the import
    /// path previously copied verbatim, which would have lowered an always-true occurrence guard.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkTriggerWithEmptyEvidence_IsRejected_WithDiagnosticAndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-fork-empty-evidence.workflow.json", ForkEmptyEvidenceJson),
            ForkTriggerEvidenceCode);
        await AssertRejected(result, ForkTriggerEvidenceCode, "$.diagnosticForks[0].permittedTriggers[0]", "ratification_failure");
    }

    /// <summary>
    /// Negative control: a diagnostic-fork permitted trigger with a NON-EMPTY
    /// <c>requiredEvidenceFields</c> satisfies the DR-8 floor — no AGWF034 — and still lowers a saga.
    /// Proves the AGWF034 check is additive (the empty-list floor), not over-broad (a declared floor
    /// is tolerated). Also anchors the kill-probe: the empty-evidence twin only differs in the floor,
    /// so its rejection is the sole reason it does not lower this same saga.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkTriggerWithDeclaredEvidence_IsNotRejected_AndLowersSaga()
    {
        var result = RunGenerator(StepTypes, ("reject-fork-with-evidence-ok.workflow.json", ForkWithEvidenceJson));

        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(d => d.Id == ForkTriggerEvidenceCode))
            .IsFalse()
            .Because("a fork trigger declaring at least one required evidence field satisfies the DR-8 floor, not rejected.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a fork import whose trigger declares its evidence floor must still lower a saga.");
    }

    /// <summary>
    /// JSON-import twin: two permittedTriggers entries naming the same closed trigger
    /// (different evidence schemas) fire AGWF037 and emit no saga (#156.2).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkDuplicateTrigger_IsRejected_WithAgwf037AndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-fork-duplicate-trigger.workflow.json", ForkDuplicateTriggerJson),
            DuplicatePermittedForkTriggerCode);
        await AssertRejected(
            result,
            DuplicatePermittedForkTriggerCode,
            "$.diagnosticForks[0].permittedTriggers[1]",
            "ratification_failure");
    }

    /// <summary>
    /// JSON-import twin: two diagnostic-fork edges that share a compensation seed
    /// fire AGWF038 and emit no saga (#156.3).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkDuplicateCompensationSeed_IsRejected_WithAgwf038AndNoSaga()
    {
        var result = RunGenerator(
            StepTypes,
            ("reject-fork-duplicate-seed.workflow.json", ForkDuplicateSeedJson),
            DuplicateCompensationSeedCode);
        await AssertRejected(
            result,
            DuplicateCompensationSeedCode,
            "$.diagnosticForks[1]",
            "RejectStepB");
    }

    /// <summary>
    /// Negative control: two DISTINCT triggers on one imported edge stay clean — no AGWF037 —
    /// and still lower a saga.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ForkDistinctTriggers_IsNotRejected_AndLowersSaga()
    {
        var result = RunGenerator(StepTypes, ("reject-fork-distinct-triggers-ok.workflow.json", ForkDistinctTriggersJson));

        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(d => d.Id == DuplicatePermittedForkTriggerCode))
            .IsFalse()
            .Because("distinct permitted triggers on one imported edge must stay silent.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a fork import with distinct permitted triggers must still lower a saga.");
    }

    /// <summary>
    /// Each rejected carrier/violation surfaces its OWN distinct diagnostic id — no case borrows
    /// another's. Pins that AGWF027–AGWF034 are one-per-case (not a single shared "rejected" id).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EachRejectedCase_HasItsOwnDistinctDiagnosticId()
    {
        var cases = new (string Json, string Path, string Code)[]
        {
            ("reject-delegate.workflow.json", DelegateJson, DelegateCode),
            ("reject-branch.workflow.json", BranchPointJson, BranchPointCode),
            ("reject-loop.workflow.json", LoopJson, LoopCode),
            ("reject-validation.workflow.json", ValidationJson, ValidationCode),
            ("reject-approval-context.workflow.json", ApprovalContextJson, ApprovalContextCode),
            ("reject-dangling-gate.workflow.json", DanglingGateJson, DanglingGateIdCode),
            ("reject-reliability-gate.workflow.json", ReliabilityGateJson, ReliabilityGateCode),
            ("reject-fork-empty-evidence.workflow.json", ForkEmptyEvidenceJson, ForkTriggerEvidenceCode),
        };

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (path, json, code) in cases)
        {
            var result = RunGenerator(StepTypes, (path, json), code);
            var errors = ErrorDiagnostics(result);
            await Assert.That(errors).HasCount().EqualTo(1)
                .Because($"{path} must fail for exactly its own rejection reason.");
            await Assert.That(errors[0].Id).IsEqualTo(code)
                .Because($"{path} must surface its own {code} diagnostic.");

            seen.Add(code);
        }

        await Assert.That(seen.Count).IsEqualTo(cases.Length)
            .Because("every rejected case must own a distinct AGWF id (one-per-case, AGWF027–AGWF034).");
    }

    /// <summary>
    /// Negative control: a gate step whose <c>gateId</c> resolves to a <c>gates[]</c> declaration
    /// (and no reliability) is NOT rejected — no AGWF032/AGWF033 — and still lowers a saga. Proves the
    /// DR-3/DR-2 checks are additive, not over-broad (DR-3 gate tolerance is preserved).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WellDeclaredGate_IsNotRejected_AndLowersSaga()
    {
        var result = RunGenerator(StepTypes, ("reject-gate-ok.workflow.json", WellDeclaredGateJson));

        await AssertNoErrors(result);
        await Assert.That(result.Diagnostics.Any(d => d.Id == DanglingGateIdCode || d.Id == ReliabilityGateCode))
            .IsFalse()
            .Because("a gate with a resolvable gateId and no reliability block is tolerated (DR-3), not rejected.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsTrue()
            .Because("a well-declared gate import must still lower a saga.");
    }

    /// <summary>
    /// Asserts the run reported the expected stable diagnostic (naming the construct + JSON path) and
    /// emitted NO saga for the rejected workflow.
    /// </summary>
    private static async Task AssertRejected(
        GeneratorDriverRunResult result,
        string expectedId,
        string expectedJsonPath,
        string expectedConstruct)
    {
        var errors = ErrorDiagnostics(result);
        await Assert.That(errors).HasCount().EqualTo(1)
            .Because($"the rejected carrier/violation must fail exclusively with {expectedId}.");
        var diagnostic = errors.SingleOrDefault(d => d.Id == expectedId);
        await Assert.That(diagnostic).IsNotNull()
            .Because($"the rejected carrier/violation must surface the stable {expectedId} diagnostic.");

        var message = diagnostic!.GetMessage();
        await Assert.That(message).Contains(expectedJsonPath)
            .Because($"{expectedId} must name the JSON path of the offending construct.");
        await Assert.That(message).Contains(expectedConstruct)
            .Because($"{expectedId} must name the offending construct.");

        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal)))
            .IsFalse()
            .Because($"a workflow rejected by {expectedId} must not emit a saga (no model is lowered).");
    }

    private static GeneratorDriverRunResult RunGenerator(
        string source,
        (string Path, string Content) additionalText,
        params string[] allowedGeneratorErrorIds)
    {
        AdditionalText[] texts = [new InMemoryAdditionalText(additionalText.Path, additionalText.Content)];

        return GeneratorTestHelper.RunGeneratorWithValidInput(
            source,
            texts,
            allowedGeneratorErrorIds);
    }

    private static Diagnostic[] ErrorDiagnostics(GeneratorDriverRunResult result) =>
        result.Diagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

    private static async Task AssertNoErrors(GeneratorDriverRunResult result) =>
        await Assert.That(ErrorDiagnostics(result)).IsEmpty()
            .Because("a legal import must not be accepted alongside an allowed error diagnostic.");

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
