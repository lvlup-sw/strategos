// -----------------------------------------------------------------------
// <copyright file="WireToModelBridge.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Helpers;
using Strategos.Generators.Models;

namespace Strategos.Generators.Import;

// =============================================================================
// DR-12 (bridge half) + DR-3 (gates tolerated), task 017 (#100) — the import
// KEYSTONE.
//
// This is the second front-end of the compiler. The C#-authoring front-end
// (FluentDslParser + WorkflowIncrementalGenerator.TransformToResult) parses a
// fluent [Workflow] definition over Roslyn syntax into a WorkflowModel; this
// bridge maps the parsed wire IR (task 026's WireDtos) into the SAME
// WorkflowModel for the importable subset. Both models flow into the identical
// saga emitters via WorkflowIncrementalGenerator.EmitWorkflowSources — one
// lowering path, zero forked emitter logic (INV-1). A JSON-authored workflow
// therefore lowers behaviorally identically to its C#-authored twin.
//
// INV-8: a wire step type is a plain simple-name string moniker. This bridge
// consumes the moniker through WireMonikerResolver (task 016), which yields a
// compile-time INamedTypeSymbol — never a CLR System.Type. The resolved symbol's
// namespaced display name is retained as the StepModel's descriptor string; no
// CLR Type is ever persisted onto the IR.
//
// DR-3 (gates tolerated, saga unaffected): a gate STEP lowers as an ordinary
// step (its stepType resolves and runs like any other), and the workflow's gate
// DECLARATIONS (`gates[]`) plus a step's `gateId` back-reference are
// consumer-plane data the saga never observes. A gate-bearing workflow therefore
// imports IDENTICALLY to its gate-free twin.
//
// SCOPE (task 017): the IMPORTABLE subset only — linear/fork flows, retry/
// timeout/compensation/confidence step config, context-free approval, gates, and
// diagnostic-fork edges.
//
// REJECTION (task 018, DR-14 rejection half + DR-2 + DR-3): before any mapping,
// CollectImportRejections walks the definition and emits a LOUD, per-case stable
// diagnostic — naming the construct + its JSON path — for every runtime-bindable
// carrier (delegate steps, branch points, loops, validation predicates,
// context-bearing approvals) and every semantic violation (a dangling gateId,
// DR-3; a reliability-bearing gate declaration, DR-2). When the scan finds any,
// the bridge returns NO model (so NO saga is emitted for that workflow) with the
// rejection diagnostics attached. Re-binding the dropped bodies (condition,
// lambda, context) is a #100 follow-on (see docs/deferred-features.md).
// =============================================================================

/// <summary>
/// Bridges a parsed wire-IR workflow definition (task 026) to the generator's
/// <see cref="WorkflowModel"/> IR for the importable subset (task 017). Step monikers are
/// resolved to CLR step symbols via <see cref="WireMonikerResolver"/> (task 016), and the mapped
/// model is lowered through the same emitters as a C#-authored workflow.
/// </summary>
internal static class WireToModelBridge
{
    /// <summary>
    /// Symbol display format producing <c>Namespace.TypeName</c> without the <c>global::</c> prefix
    /// — the IDENTICAL format the C#-authoring path uses for a step's descriptor
    /// (<c>StepModel.StepTypeName</c>), so an imported step's DI registration and worker-handler
    /// namespacing match its C#-authored twin's byte-for-byte.
    /// </summary>
    private static readonly SymbolDisplayFormat NamespacedTypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <summary>
    /// Bridges <paramref name="definition"/> into a <see cref="WorkflowModel"/> for the importable
    /// subset.
    /// </summary>
    /// <param name="definition">The parsed wire-IR workflow definition.</param>
    /// <param name="compilation">The compilation whose symbol table resolves step monikers.</param>
    /// <param name="jsonFilePath">The import file path, threaded into moniker-resolution diagnostics.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// The lowered model (or null when the document is not lowerable — an empty/unnamed workflow, a
    /// step whose moniker does not resolve, or a step arm the importable subset does not carry) plus
    /// any moniker-resolution diagnostics.
    /// </returns>
    public static BridgeResult Bridge(
        WorkflowDefinitionV1 definition,
        Compilation compilation,
        string jsonFilePath,
        CancellationToken ct)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        ct.ThrowIfCancellationRequested();

        var diagnostics = new List<Diagnostic>();

        // DR-12 / DR-18: a recognized *.workflow.json that parses as well-formed JSON but binds to a
        // structurally schema-invalid workflow (no name, or no steps — including a non-array `steps`
        // field the reader coerces to an empty list, or scalar values coerced to defaults) must
        // surface a STABLE build diagnostic, not be silently swallowed. Fail closed with the stable
        // AGWF code identity rather than returning an empty result the build never sees.
        var workflowName = definition.Name;
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            diagnostics.Add(Diagnostic.Create(WorkflowDiagnostics.EmptyWorkflowName, Location.None));
            return new BridgeResult(null, diagnostics);
        }

        if (definition.Steps.Count == 0)
        {
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.NoStepsFound,
                Location.None,
                workflowName!));
            return new BridgeResult(null, diagnostics);
        }

        // DR-14 (rejection half) + DR-2 + DR-3: reject runtime-bindable carriers and semantic
        // violations LOUDLY before any mapping. A workflow carrying one is not lowered — the
        // rejection diagnostics are surfaced and NO model (hence NO saga) is produced.
        var rejections = CollectImportRejections(definition, jsonFilePath);
        if (rejections.Count > 0)
        {
            return new BridgeResult(null, rejections);
        }

        // Resolve the entry step first: it anchors the generated namespace and the inferred state
        // type (the wire IR carries neither — both are recovered from the resolved step symbol).
        var entryStep = ResolveEntryStep(definition);
        if (entryStep is null || GetStepMoniker(entryStep) is not { } entryMoniker)
        {
            return new BridgeResult(null, diagnostics);
        }

        var entrySymbol = ResolveStepSymbol(compilation, entryMoniker, jsonFilePath, diagnostics);
        if (entrySymbol is null)
        {
            return new BridgeResult(null, diagnostics);
        }

        var @namespace = entrySymbol.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(@namespace) || @namespace == "<global namespace>")
        {
            // A step in the global namespace has no home for the generated saga; do not lower.
            return new BridgeResult(null, diagnostics);
        }

        var stateSymbol = GetWorkflowStateType(entrySymbol);
        var stateTypeName = stateSymbol?.Name;
        var stateHasPhaseProperty = stateSymbol is not null && HasPublicPhaseProperty(stateSymbol);

        var pascalName = WorkflowIncrementalGenerator.ToPascalCase(workflowName!);

        // Map every top-level step. A moniker that does not resolve, or an arm the importable
        // subset does not carry (e.g. a delegate step), makes the whole workflow non-lowerable —
        // returning no model rather than a partial, uncompilable saga (task 018 adds the loud
        // rejection diagnostic for these; here they simply do not lower).
        var baseStepModels = new List<StepModel>(definition.Steps.Count);
        var stepIdToName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stepDef in definition.Steps)
        {
            var stepModel = MapStep(stepDef, compilation, jsonFilePath, diagnostics);
            if (stepModel is null)
            {
                return new BridgeResult(null, diagnostics);
            }

            baseStepModels.Add(stepModel);
            if (!string.IsNullOrEmpty(stepDef.StepId))
            {
                stepIdToName[stepDef.StepId!] = stepModel.PhaseName;
            }
        }

        // Fork points → ForkModel. Each fork's parallel path steps are woven INLINE into the linear
        // step-name list (at the fork position) by ComposeStepNames below — never into the step
        // MODEL list — mirroring the C#-authoring fluent walk so the fork saga emitter sees the
        // identical shape.
        var forkModels = MapForks(definition, pascalName, compilation, jsonFilePath, stepIdToName, diagnostics);
        if (forkModels is null)
        {
            return new BridgeResult(null, diagnostics);
        }

        // Same EffectiveName / type-collision gates the C# [Workflow] root runs in
        // TransformToResult. Sharing EmitWorkflowSources is not sharing the gate: a JSON
        // fork twin of the #190 shape would otherwise lower to CS0111.
        var identityDiagnostics = CollectIdentityDiagnostics(baseStepModels, forkModels, workflowName!);
        if (identityDiagnostics.Count > 0)
        {
            diagnostics.AddRange(identityDiagnostics);
            return new BridgeResult(null, diagnostics);
        }

        // Context-free approval points → ApprovalModel (DR-14: a context-bearing approval is a
        // rejected carrier handled by task 018; here only the bare, context-free arm is mapped).
        var approvalModels = MapApprovals(definition, compilation, jsonFilePath, stepIdToName, diagnostics);
        if (approvalModels is null)
        {
            return new BridgeResult(null, diagnostics);
        }

        // Diagnostic-fork edges (DR-10) → DiagnosticForkModel, attached to the model. The saga
        // lowering that consumes them is deferred (#151); the bridge only carries the edge.
        var diagnosticForkModels = MapDiagnosticForks(definition);

        // Compose the final step graph exactly as the C#-authoring path does. NOTE the deliberate
        // asymmetry the C# path exhibits:
        //   * StepNames  = top-level linear steps in document order, then each fork's path steps
        //                  APPENDED (fork paths are off the top-level phase chain).
        //   * Steps      = the fork's path step models are woven INLINE at the fork position (the C#
        //                  ExtractStepModels walk inlines them), which is the order the saga's
        //                  not-found handlers are emitted in.
        // Both orders must be reproduced or the generated saga diverges from a C# twin.
        var stepNames = ComposeStepNames(definition, baseStepModels, forkModels);
        var stepModels = ComposeStepModels(definition, baseStepModels, forkModels);
        (stepNames, stepModels) = AppendApprovalSteps(stepNames, stepModels, approvalModels);
        var confidenceHandlerStepNames = AppendConfidenceHandlerSteps(ref stepNames, ref stepModels);
        stepModels = FoldCompensationSteps(stepModels);

        var model = new WorkflowModel(
            WorkflowName: workflowName!,
            PascalName: pascalName,
            Namespace: @namespace!,
            StepNames: stepNames,
            StateTypeName: stateTypeName,
            Version: 1,
            PersistenceMode: PersistenceMode.SagaDocument,
            Steps: stepModels,
            Loops: null,
            Branches: null,
            FailureHandlers: null,
            ApprovalPoints: approvalModels.Count > 0 ? approvalModels : null,
            Forks: forkModels.Count > 0 ? forkModels : null,
            ConfidenceHandlerStepNames: confidenceHandlerStepNames,
            DiagnosticForks: diagnosticForkModels.Count > 0 ? diagnosticForkModels : null)
        {
            StateHasPhaseProperty = stateHasPhaseProperty,

            // A JSON import has no fluent {Pascal}WorkflowDefinition class, so the DI extension must
            // NOT emit the definition-evaluation line that references it (it would not compile).
            HasFluentDefinition = false,
        };

        return new BridgeResult(model, diagnostics);
    }

    /// <summary>
    /// Applies the C# identity gates (duplicate EffectiveName, exclusive-path
    /// type collision) to the mapped import IR so a JSON twin cannot emit a colliding saga.
    /// </summary>
    private static List<Diagnostic> CollectIdentityDiagnostics(
        IReadOnlyList<StepModel> baseStepModels,
        IReadOnlyList<ForkModel> forkModels,
        string workflowName)
    {
        var diagnostics = new List<Diagnostic>();

        var forkPathSteps = forkModels
            .SelectMany(static f => f.Paths.SelectMany(static p => p.Steps))
            .ToList();

        // Round-tripped JSON lists fork-path steps in both steps[] and forkPoints.paths.
        // Consume one matching base entry per fork-path representation so the echo
        // is not a duplicate name, but an extra top-level step with the same phase+type still is.
        var remainingForkPathCopies = new Dictionary<(string Phase, string Type), int>();
        foreach (var step in forkPathSteps)
        {
            var key = (step.PhaseName, step.StepName);
            remainingForkPathCopies[key] = remainingForkPathCopies.TryGetValue(key, out var copies)
                ? copies + 1
                : 1;
        }

        var identitySteps = new List<StepModel>(baseStepModels.Count + forkPathSteps.Count);
        foreach (var step in baseStepModels)
        {
            var key = (step.PhaseName, step.StepName);
            if (remainingForkPathCopies.TryGetValue(key, out var remaining) && remaining > 0)
            {
                remainingForkPathCopies[key] = remaining - 1;
                continue;
            }

            identitySteps.Add(step);
        }

        identitySteps.AddRange(forkPathSteps);

        var duplicateNames = identitySteps
            .GroupBy(static s => s.EffectiveName, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .ToList();

        foreach (var duplicate in duplicateNames)
        {
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.DuplicateStepName,
                Location.None,
                duplicate,
                workflowName));
        }

        foreach (var collidingType in PathEndTypeCollisionFinder.Find(forkModels, []))
        {
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.PathEndTypeCollision,
                Location.None,
                collidingType,
                workflowName));
        }

        return diagnostics;
    }

    /// <summary>
    /// Walks <paramref name="definition"/> and collects a LOUD, per-case stable rejection diagnostic
    /// for every runtime-bindable carrier (DR-14 rejection half) and every semantic violation
    /// (DR-2 / DR-3) an import cannot lower. Each diagnostic names the construct and its JSON path.
    /// A non-empty result means the workflow is not lowered (no saga is emitted).
    /// </summary>
    /// <remarks>
    /// Rejected carriers: a delegate (lambda) step, a branch point, a loop (RepeatUntil), a step's
    /// validation predicate, and a context-bearing approval (a <c>hasContext</c> marker, escalation,
    /// or rejection handler). Rejected semantic violations: a gate step whose <c>gateId</c>
    /// back-reference names an id absent from <c>gates[]</c> (DR-3), a gate declaration carrying
    /// a <c>reliability</c> block (DR-2 — reliability enters a definition only from telemetry), a
    /// diagnostic-fork permitted trigger declaring no <c>requiredEvidenceFields</c> (DR-8 — the wire
    /// <c>@minItems(1)</c> evidence floor; an empty list would lower an always-true occurrence guard),
    /// and a diagnostic-fork edge that permits the same trigger twice (#156.2 — two same-trigger
    /// declarations can carry different evidence schemas; reject, do not first-wins-dedup).
    /// </remarks>
    private static List<Diagnostic> CollectImportRejections(
        WorkflowDefinitionV1 definition,
        string jsonFilePath)
    {
        var rejections = new List<Diagnostic>();

        // The ids a gate step's gateId may back-reference (DR-3): the workflow's gate declarations.
        var gateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gate in definition.Gates)
        {
            if (!string.IsNullOrEmpty(gate.Id))
            {
                gateIds.Add(gate.Id!);
            }
        }

        // (1) Importable step positions — the top-level steps (recursing into confidence handler
        // chains) and every fork path — are scanned for a delegate step, a validation predicate,
        // and the DR-3 dangling-gateId violation. Branch/loop/approval-handler steps live under
        // constructs rejected wholesale below, so they are not descended into here.
        ScanImportableSteps(definition.Steps, "$.steps", jsonFilePath, gateIds, rejections);
        for (var f = 0; f < definition.ForkPoints.Count; f++)
        {
            var fork = definition.ForkPoints[f];
            for (var p = 0; p < fork.Paths.Count; p++)
            {
                ScanImportableSteps(
                    fork.Paths[p].Steps,
                    $"$.forkPoints[{f}].paths[{p}].steps",
                    jsonFilePath,
                    gateIds,
                    rejections);
            }
        }

        // (2) Root-level construct carriers rejected wholesale (DR-14): branch points and loops.
        for (var i = 0; i < definition.BranchPoints.Count; i++)
        {
            rejections.Add(Diagnostic.Create(
                WorkflowDiagnostics.ImportRejectedBranchPoint,
                Location.None,
                jsonFilePath,
                $"$.branchPoints[{i}]",
                DescribeId(definition.BranchPoints[i].BranchPointId)));
        }

        for (var i = 0; i < definition.Loops.Count; i++)
        {
            var loop = definition.Loops[i];
            rejections.Add(Diagnostic.Create(
                WorkflowDiagnostics.ImportRejectedLoop,
                Location.None,
                jsonFilePath,
                $"$.loops[{i}]",
                DescribeId(string.IsNullOrEmpty(loop.LoopName) ? loop.LoopId : loop.LoopName)));
        }

        // (3) Context-bearing approvals rejected (DR-14): a hasContext marker (task 024), an
        // escalation handler, or a rejection handler carries behavior the wire IR drops on export.
        for (var i = 0; i < definition.ApprovalPoints.Count; i++)
        {
            var approval = definition.ApprovalPoints[i];
            if (approval.HasContext
                || approval.EscalationHandler is not null
                || approval.RejectionHandler is not null)
            {
                rejections.Add(Diagnostic.Create(
                    WorkflowDiagnostics.ImportRejectedApprovalContext,
                    Location.None,
                    jsonFilePath,
                    $"$.approvalPoints[{i}]",
                    DescribeId(approval.ApprovalPointId)));
            }
        }

        // (4) Reliability-bearing gate declarations rejected (DR-2 machine-check): reliability
        // enters a definition only from measured telemetry, never from authored import JSON.
        for (var i = 0; i < definition.Gates.Count; i++)
        {
            if (definition.Gates[i].Reliability is not null)
            {
                rejections.Add(Diagnostic.Create(
                    WorkflowDiagnostics.ImportReliabilityBearingGate,
                    Location.None,
                    jsonFilePath,
                    $"$.gates[{i}].reliability",
                    DescribeId(definition.Gates[i].Id)));
            }
        }

        // (5) Diagnostic-fork permitted triggers declaring NO required evidence fields rejected
        // (DR-8 evidence floor). The wire contract pins @minItems(1) on requiredEvidenceFields and
        // the C# builder forces >= 1, but the import path copies the list verbatim into
        // MapDiagnosticForks -> PermittedForkTriggerModel.Create, which enforces the floor by
        // THROWING on an empty list — an unhandled throw that crashes the whole generator (CS8785)
        // and drops ALL generated output for the compilation. (Were the model floor bypassed, the
        // emitter would instead lower a guard arm ForkEvidenceComplete(cmd.Evidence) with ZERO
        // required fields, an always-true no-op that defeats the DR-8 no-unjustified-fork check.)
        // Reject it here, BEFORE mapping, so it fails closed with a stable per-file diagnostic and
        // no saga is lowered — never a generator crash.
        for (var i = 0; i < definition.DiagnosticForks.Count; i++)
        {
            var permittedTriggers = definition.DiagnosticForks[i].PermittedTriggers;
            for (var j = 0; j < permittedTriggers.Count; j++)
            {
                if (permittedTriggers[j].RequiredEvidenceFields.Count == 0)
                {
                    rejections.Add(Diagnostic.Create(
                        WorkflowDiagnostics.ImportForkTriggerWithoutEvidence,
                        Location.None,
                        jsonFilePath,
                        $"$.diagnosticForks[{i}].permittedTriggers[{j}]",
                        DescribeId(permittedTriggers[j].Trigger)));
                }
            }

            // Two PermitTrigger declarations of the same closed trigger on one edge (#156.2).
            // Reject rather than first-wins-dedup: the twins can carry different evidence
            // schemas, and MapDiagnosticForks → DiagnosticForkModel.Create would otherwise
            // throw (CS8785) or the emitter's per-trigger switch would fail closed as CS0152.
            var triggerNames = new string[permittedTriggers.Count];
            for (var j = 0; j < permittedTriggers.Count; j++)
            {
                triggerNames[j] = permittedTriggers[j].Trigger ?? string.Empty;
            }

            foreach (var duplicate in DiagnosticForkModel.FindDuplicateTriggerNames(triggerNames))
            {
                var secondIndex = -1;
                for (var j = 0; j < triggerNames.Length; j++)
                {
                    if (string.Equals(triggerNames[j], duplicate, StringComparison.Ordinal))
                    {
                        if (secondIndex >= 0)
                        {
                            secondIndex = j;
                            break;
                        }

                        secondIndex = j;
                    }
                }

                rejections.Add(Diagnostic.Create(
                    WorkflowDiagnostics.DuplicatePermittedForkTrigger,
                    Location.None,
                    jsonFilePath,
                    $"$.diagnosticForks[{i}].permittedTriggers[{secondIndex}]",
                    DescribeId(duplicate)));
            }
        }

        return rejections;
    }

    /// <summary>
    /// Scans one importable step list (and, recursively, each step's low-confidence handler chain)
    /// for a delegate (lambda) step, a validation predicate, and a DR-3 dangling <c>gateId</c>,
    /// appending a rejection diagnostic — naming the construct + its JSON path — for each.
    /// </summary>
    private static void ScanImportableSteps(
        IReadOnlyList<StepDefinition> steps,
        string pathPrefix,
        string jsonFilePath,
        HashSet<string> gateIds,
        List<Diagnostic> rejections)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var path = $"{pathPrefix}[{i}]";

            // Delegate (lambda) step — a runtime-bindable carrier (DR-14).
            if (step is DelegateStep)
            {
                rejections.Add(Diagnostic.Create(
                    WorkflowDiagnostics.ImportRejectedDelegateStep,
                    Location.None,
                    jsonFilePath,
                    path,
                    DescribeId(string.IsNullOrEmpty(step.StepId) ? step.StepName : step.StepId)));
            }

            // Validation predicate on the step configuration — a runtime-bindable carrier (DR-14).
            if (step.Configuration?.Validation is not null)
            {
                rejections.Add(Diagnostic.Create(
                    WorkflowDiagnostics.ImportRejectedValidationPredicate,
                    Location.None,
                    jsonFilePath,
                    $"{path}.configuration.validation",
                    DescribeId(string.IsNullOrEmpty(step.StepId) ? step.StepName : step.StepId)));
            }

            // Dangling gateId back-reference — a DR-3 semantic violation.
            if (step is GateStep gate
                && !string.IsNullOrEmpty(gate.GateId)
                && !gateIds.Contains(gate.GateId!))
            {
                rejections.Add(Diagnostic.Create(
                    WorkflowDiagnostics.ImportDanglingGateId,
                    Location.None,
                    jsonFilePath,
                    $"{path}.gateId",
                    gate.GateId!));
            }

            // The low-confidence handler chain is the only importable nested step position; descend
            // into it so a carrier buried in a handler step is still rejected.
            var handlerSteps = step.Configuration?.OnLowConfidence?.HandlerSteps;
            if (handlerSteps is not null && handlerSteps.Count > 0)
            {
                ScanImportableSteps(
                    handlerSteps,
                    $"{path}.configuration.onLowConfidence.handlerSteps",
                    jsonFilePath,
                    gateIds,
                    rejections);
            }
        }
    }

    /// <summary>
    /// Renders a wire id/name for a rejection message, substituting a stable placeholder when the
    /// construct declares neither, so the diagnostic still names an actionable position.
    /// </summary>
    private static string DescribeId(string? id) => string.IsNullOrEmpty(id) ? "(unnamed)" : id!;

    /// <summary>
    /// Resolves the entry step — the step named by <see cref="WorkflowDefinitionV1.EntryStepId"/>,
    /// or the first step in document order when no entry id is declared.
    /// </summary>
    private static StepDefinition? ResolveEntryStep(WorkflowDefinitionV1 definition)
    {
        if (!string.IsNullOrEmpty(definition.EntryStepId))
        {
            foreach (var step in definition.Steps)
            {
                if (string.Equals(step.StepId, definition.EntryStepId, StringComparison.Ordinal))
                {
                    return step;
                }
            }
        }

        return definition.Steps.Count > 0 ? definition.Steps[0] : null;
    }

    /// <summary>
    /// Gets the simple-name step-type moniker for an importable step arm (skill/handler/gate), or
    /// null for an arm the importable subset does not carry (a delegate step, an approval step).
    /// </summary>
    private static string? GetStepMoniker(StepDefinition step) => step switch
    {
        SkillStep skill => skill.StepType,
        HandlerStep handler => handler.StepType,
        GateStep gate => gate.StepType,
        _ => null,
    };

    /// <summary>
    /// Maps one importable step definition to a <see cref="StepModel"/>, resolving its moniker and
    /// its retry/timeout/compensation/confidence configuration. Returns null when the moniker does
    /// not resolve or the arm is not importable.
    /// </summary>
    private static StepModel? MapStep(
        StepDefinition step,
        Compilation compilation,
        string jsonFilePath,
        List<Diagnostic> diagnostics)
    {
        if (GetStepMoniker(step) is not { } moniker)
        {
            return null;
        }

        var symbol = ResolveStepSymbol(compilation, moniker, jsonFilePath, diagnostics);
        if (symbol is null)
        {
            return null;
        }

        var stepTypeName = symbol.ToDisplayString(NamespacedTypeFormat);
        var instanceName = string.IsNullOrEmpty(step.InstanceName) ? null : step.InstanceName;

        if (!TryMapConfiguration(
                step.Configuration,
                compilation,
                jsonFilePath,
                diagnostics,
                out var retry,
                out var timeout,
                out var compensation,
                out var confidence))
        {
            // An unresolvable compensation moniker (its Error diagnostic already recorded) makes the
            // whole workflow non-lowerable — matching the fail-closed primary-step path — rather than
            // silently lowering a saga that references an unregistered compensation step.
            return null;
        }

        return StepModel.Create(
            symbol.Name,
            stepTypeName,
            instanceName: instanceName,
            retry: retry,
            timeout: timeout,
            compensation: compensation,
            confidence: confidence);
    }

    /// <summary>
    /// Maps a wire step configuration tree to the generator's resilience IR
    /// (<see cref="RetryModel"/> / <see cref="TimeoutModel"/> / <see cref="CompensationModel"/> /
    /// <see cref="ConfidenceModel"/>). A validation guard (LB-1 declarative predicate) is a rejected
    /// carrier (task 018) and is intentionally not mapped here. Returns <see langword="false"/> (with
    /// the resolution diagnostic recorded) when a declared compensation moniker does not resolve, so
    /// the caller can treat the whole workflow as non-lowerable — matching the fail-closed
    /// primary-step path (<see cref="ResolveStepSymbol"/>) rather than silently lowering a saga that
    /// references an unregistered compensation step.
    /// </summary>
    private static bool TryMapConfiguration(
        StepConfigurationDefinition? config,
        Compilation compilation,
        string jsonFilePath,
        List<Diagnostic> diagnostics,
        out RetryModel? retry,
        out TimeoutModel? timeout,
        out CompensationModel? compensation,
        out ConfidenceModel? confidence)
    {
        retry = null;
        timeout = null;
        compensation = null;
        confidence = null;

        if (config is null)
        {
            return true;
        }

        retry = config.Retry is { } r
            ? new RetryModel(
                MaxAttempts: r.MaxAttempts,
                InitialDelay: ParseIsoDuration(r.InitialDelay),
                BackoffMultiplier: r.BackoffMultiplier,
                MaxDelay: ParseIsoDuration(r.MaxDelay),
                UseJitter: r.UseJitter ?? false)
            : null;

        timeout = ParseIsoDuration(config.Timeout) is { } t
            ? new TimeoutModel(t)
            : null;

        if (config.Compensation is { } c && !string.IsNullOrEmpty(c.CompensationStepType))
        {
            // Fail CLOSED on an unresolvable compensation moniker: ResolveStepSymbol records the
            // stable Error diagnostic and returns null, and we propagate the failure so no saga is
            // lowered — the SAME contract the primary step moniker follows (was fail-open: the
            // resolution diagnostic was discarded and the raw moniker lowered with IsRegisteredStep:false).
            var compSymbol = ResolveStepSymbol(compilation, c.CompensationStepType!, jsonFilePath, diagnostics);
            if (compSymbol is null)
            {
                return false;
            }

            compensation = new CompensationModel(
                CompensationStepTypeName: compSymbol.ToDisplayString(NamespacedTypeFormat),
                RequiredOnFailure: c.RequiredOnFailure ?? true,
                IsRegisteredStep: true);
        }

        if (config.OnLowConfidence is { } handler)
        {
            confidence = MapConfidence(config.ConfidenceThreshold ?? 0.0, handler, compilation, jsonFilePath, diagnostics);
        }

        return true;
    }

    /// <summary>
    /// Maps a wire low-confidence handler to a <see cref="ConfidenceModel"/> carrying the ordered
    /// handler chain (G-4). A wire handler that terminates (<c>isTerminal</c>) maps to a
    /// non-rejoining chain (the DR-5 back-compat default); a non-terminal handler rejoins the main
    /// flow.
    /// </summary>
    private static ConfidenceModel? MapConfidence(
        double threshold,
        LowConfidenceHandlerDefinition handler,
        Compilation compilation,
        string jsonFilePath,
        List<Diagnostic> diagnostics)
    {
        if (handler.HandlerSteps.Count == 0)
        {
            return new ConfidenceModel(threshold);
        }

        var handlerSteps = new List<StepModel>(handler.HandlerSteps.Count);
        foreach (var handlerStepDef in handler.HandlerSteps)
        {
            var handlerStep = MapStep(handlerStepDef, compilation, jsonFilePath, diagnostics);
            if (handlerStep is null)
            {
                // A handler step that does not resolve leaves the confidence gate unmappable; the
                // moniker diagnostic is already recorded. Drop the whole handler so the parent
                // step still lowers as an ungated step rather than referencing a phantom step.
                return new ConfidenceModel(threshold);
            }

            handlerSteps.Add(handlerStep);
        }

        var chain = new LowConfidenceHandlerChainModel(
            Steps: handlerSteps,
            RejoinsMainFlow: !handler.IsTerminal);

        return new ConfidenceModel(
            Threshold: threshold,
            OnLowConfidenceHandlerId: handlerSteps[0].StepName,
            OnLowConfidenceHandlerStep: handlerSteps[0],
            OnLowConfidenceHandlerChain: chain);
    }

    /// <summary>
    /// Maps the wire fork points to <see cref="ForkModel"/>s. The generator-internal
    /// <see cref="ForkModel.ForkId"/> is derived deterministically as
    /// <c>{pascalName}-Fork{index}</c> — the IDENTICAL id the C#-authoring <c>ForkExtractor</c>
    /// mints (it is passed the workflow's PascalName) — so the generated fork command/event names
    /// match a C#-authored twin's. The wire <c>forkPointId</c> (the builder's edge id) is
    /// intentionally NOT used for the generated identity. Returns null when a fork path step does
    /// not resolve.
    /// </summary>
    private static IReadOnlyList<ForkModel>? MapForks(
        WorkflowDefinitionV1 definition,
        string pascalName,
        Compilation compilation,
        string jsonFilePath,
        IReadOnlyDictionary<string, string> stepIdToName,
        List<Diagnostic> diagnostics)
    {
        if (definition.ForkPoints.Count == 0)
        {
            return [];
        }

        var forks = new List<ForkModel>(definition.ForkPoints.Count);
        for (var forkIndex = 0; forkIndex < definition.ForkPoints.Count; forkIndex++)
        {
            var forkPoint = definition.ForkPoints[forkIndex];

            var previousStepName = forkPoint.FromStepId is { } fromId && stepIdToName.TryGetValue(fromId, out var prev)
                ? prev
                : string.Empty;
            var joinStepName = forkPoint.JoinStepId is { } joinId && stepIdToName.TryGetValue(joinId, out var join)
                ? join
                : string.Empty;

            var paths = new List<ForkPathModel>(forkPoint.Paths.Count);
            foreach (var pathDef in forkPoint.Paths)
            {
                var pathSteps = new List<StepModel>(pathDef.Steps.Count);
                foreach (var pathStepDef in pathDef.Steps)
                {
                    var pathStep = MapStep(pathStepDef, compilation, jsonFilePath, diagnostics);
                    if (pathStep is null)
                    {
                        return null;
                    }

                    pathSteps.Add(pathStep);
                }

                if (pathSteps.Count == 0)
                {
                    // A fork path with no steps is unrepresentable; do not lower.
                    return null;
                }

                paths.Add(ForkPathModel.Create(
                    pathDef.PathIndex,
                    pathSteps,
                    hasFailureHandler: pathDef.FailureHandler is not null,
                    isTerminalOnFailure: pathDef.FailureHandler?.IsTerminal ?? false));
            }

            if (paths.Count < 2)
            {
                // A fork with fewer than two paths is not a fork; do not lower.
                return null;
            }

            forks.Add(ForkModel.Create(
                $"{pascalName}-Fork{forkIndex}",
                previousStepName,
                paths,
                joinStepName));
        }

        return forks;
    }

    /// <summary>
    /// Maps the wire approval points to context-free <see cref="ApprovalModel"/>s. An approval that
    /// declares context (<c>hasContext</c>), an escalation, or a rejection handler is a rejected
    /// carrier (task 018) and makes the workflow non-lowerable here (returns null); only the bare,
    /// context-free approval arm is importable.
    /// </summary>
    private static IReadOnlyList<ApprovalModel>? MapApprovals(
        WorkflowDefinitionV1 definition,
        Compilation compilation,
        string jsonFilePath,
        IReadOnlyDictionary<string, string> stepIdToName,
        List<Diagnostic> diagnostics)
    {
        if (definition.ApprovalPoints.Count == 0)
        {
            return [];
        }

        var approvals = new List<ApprovalModel>(definition.ApprovalPoints.Count);
        var approvalIndex = 0;
        foreach (var approvalDef in definition.ApprovalPoints)
        {
            // Context-bearing / escalation / rejection approvals are carriers rejected by task 018.
            if (approvalDef.HasContext
                || approvalDef.EscalationHandler is not null
                || approvalDef.RejectionHandler is not null
                || string.IsNullOrEmpty(approvalDef.ApproverType)
                || string.IsNullOrEmpty(approvalDef.PrecedingStepId))
            {
                return null;
            }

            // Fail CLOSED on an unresolvable approver moniker: ResolveStepSymbol records the stable
            // Error diagnostic and returns null, and we treat the workflow as non-lowerable — the
            // SAME contract the primary step moniker follows (was fail-open: the resolution diagnostic
            // was discarded and the raw moniker lowered into the approval).
            var approverSymbol = ResolveStepSymbol(compilation, approvalDef.ApproverType!, jsonFilePath, diagnostics);
            if (approverSymbol is null)
            {
                return null;
            }

            var approverTypeName = approverSymbol.ToDisplayString(NamespacedTypeFormat);

            if (!stepIdToName.TryGetValue(approvalDef.PrecedingStepId!, out var precedingStepName))
            {
                return null;
            }

            // DERIVE the approval-point name (a valid C# identifier) from the approver type name, via
            // the SAME shared derivation the C#-authoring path uses (ApprovalPointNaming.Derive), so
            // the two channels cannot drift. The wire ApprovalPointId is a GUID IDENTITY (e.g.
            // Guid.NewGuid().ToString("N")), NOT a C# identifier — feeding a digit-leading GUID to
            // ApprovalModel.Create fails IdentifierValidator and crashes the generator (CS8785). It is
            // kept for wire identity/lookup only and never used as the generated point name.
            var approvalPointName = ApprovalPointNaming.Derive(approverSymbol.Name, approvalIndex);
            approvalIndex++;

            approvals.Add(ApprovalModel.Create(
                approvalPointName,
                approverTypeName,
                precedingStepName));
        }

        return approvals;
    }

    /// <summary>
    /// Maps the wire diagnostic-fork edges (DR-10) to <see cref="DiagnosticForkModel"/>s. Each edge
    /// is carried onto the model; the saga lowering that consumes them is deferred (#151).
    /// </summary>
    private static IReadOnlyList<DiagnosticForkModel> MapDiagnosticForks(WorkflowDefinitionV1 definition)
    {
        if (definition.DiagnosticForks.Count == 0)
        {
            return [];
        }

        var edges = new List<DiagnosticForkModel>(definition.DiagnosticForks.Count);
        foreach (var edgeDef in definition.DiagnosticForks)
        {
            var triggers = new List<PermittedForkTriggerModel>(edgeDef.PermittedTriggers.Count);
            foreach (var trigger in edgeDef.PermittedTriggers)
            {
                triggers.Add(PermittedForkTriggerModel.Create(
                    trigger.Trigger ?? string.Empty,
                    [.. trigger.RequiredEvidenceFields]));
            }

            edges.Add(DiagnosticForkModel.Create(
                [.. edgeDef.AnchorStepIds],
                triggers,
                edgeDef.CompensationSeed ?? string.Empty,
                edgeDef.MaxForks));
        }

        return edges;
    }

    /// <summary>
    /// Composes the linear step-NAME order, weaving each fork's parallel path steps INLINE right
    /// after the step the fork originates from (its wire <c>fromStepId</c>). This mirrors the
    /// C#-authoring fluent walk, which places a fork's path steps between the pre-fork step and the
    /// join step (e.g. <c>Intake, Assess, Review, Aggregate, Settle</c>) rather than appending them.
    /// The join step is an ordinary top-level step, emitted in its own document position; fork path
    /// steps are NEVER added to the step MODEL list — their completion is handled by the fork
    /// path-completed handler, not the generic step-completed handler.
    /// </summary>
    private static List<string> ComposeStepNames(
        WorkflowDefinitionV1 definition,
        IReadOnlyList<StepModel> baseStepModels,
        IReadOnlyList<ForkModel> forkModels)
    {
        // Map the wire step id a fork originates from to its ForkModel (same order as the wire fork
        // points), so a fork's path steps can be woven in right after the step that precedes it.
        var forksByFromStepId = new Dictionary<string, ForkModel>(StringComparer.Ordinal);
        for (var i = 0; i < forkModels.Count && i < definition.ForkPoints.Count; i++)
        {
            var fromId = definition.ForkPoints[i].FromStepId;
            if (!string.IsNullOrEmpty(fromId))
            {
                forksByFromStepId[fromId!] = forkModels[i];
            }
        }

        var stepNames = new List<string>(baseStepModels.Count);
        var existing = new HashSet<string>(StringComparer.Ordinal);

        void AddName(string name)
        {
            if (existing.Add(name))
            {
                stepNames.Add(name);
            }
        }

        for (var i = 0; i < baseStepModels.Count; i++)
        {
            AddName(baseStepModels[i].PhaseName);

            var stepId = definition.Steps[i].StepId;
            if (!string.IsNullOrEmpty(stepId) && forksByFromStepId.TryGetValue(stepId!, out var fork))
            {
                foreach (var path in fork.Paths)
                {
                    foreach (var step in path.Steps)
                    {
                        AddName(step.PhaseName);
                    }
                }
            }
        }

        // An export that already lists its path steps as top-level steps has had them added in
        // document position above, so this sweep is a dedupe no-op. It remains as the catch-all
        // for a fork whose originating step id does not resolve, so no path step is ever dropped
        // from the lowering.
        foreach (var fork in forkModels)
        {
            foreach (var path in fork.Paths)
            {
                foreach (var step in path.Steps)
                {
                    AddName(step.PhaseName);
                }
            }
        }

        return stepNames;
    }

    /// <summary>
    /// Composes the step MODEL list, weaving each fork's parallel path step models INLINE right
    /// after the step the fork originates from (its wire <c>fromStepId</c>). This mirrors the
    /// C#-authoring <c>ExtractStepModels</c> walk, which inlines fork path steps into
    /// <c>model.Steps</c> (e.g. <c>Intake, Assess, Review, Aggregate, Settle</c>) — the order the
    /// saga's per-step not-found handlers are emitted in. The generic step-completed handler skips
    /// these fork path steps (recognizing them via <c>model.Forks</c>); their completion is handled
    /// by the fork path-completed handler instead.
    /// </summary>
    private static List<StepModel> ComposeStepModels(
        WorkflowDefinitionV1 definition,
        IReadOnlyList<StepModel> baseStepModels,
        IReadOnlyList<ForkModel> forkModels)
    {
        // Map the wire step id a fork originates from to its ForkModel (same order as the wire fork
        // points), so a fork's path step models can be woven in right after the step that precedes it.
        var forksByFromStepId = new Dictionary<string, ForkModel>(StringComparer.Ordinal);
        for (var i = 0; i < forkModels.Count && i < definition.ForkPoints.Count; i++)
        {
            var fromId = definition.ForkPoints[i].FromStepId;
            if (!string.IsNullOrEmpty(fromId))
            {
                forksByFromStepId[fromId!] = forkModels[i];
            }
        }

        var stepModels = new List<StepModel>(baseStepModels.Count);
        var existing = new HashSet<string>(StringComparer.Ordinal);

        void AddModel(StepModel step)
        {
            if (existing.Add(step.StepName))
            {
                stepModels.Add(step);
            }
        }

        for (var i = 0; i < baseStepModels.Count; i++)
        {
            AddModel(baseStepModels[i]);

            var stepId = definition.Steps[i].StepId;
            if (!string.IsNullOrEmpty(stepId) && forksByFromStepId.TryGetValue(stepId!, out var fork))
            {
                foreach (var path in fork.Paths)
                {
                    foreach (var step in path.Steps)
                    {
                        AddModel(step);
                    }
                }
            }
        }

        return stepModels;
    }

    /// <summary>
    /// Appends each approval's rejection/escalation steps to the step lists. A context-free approval
    /// (the only importable arm, per <see cref="MapApprovals"/>) has none, so this is a no-op for
    /// the importable subset; it is kept for parity with the C#-authoring composition.
    /// </summary>
    private static (List<string> StepNames, List<StepModel> StepModels) AppendApprovalSteps(
        List<string> stepNames,
        List<StepModel> stepModels,
        IReadOnlyList<ApprovalModel> approvals)
    {
        if (approvals.Count == 0)
        {
            return (stepNames, stepModels);
        }

        var existingNames = new HashSet<string>(stepNames, StringComparer.Ordinal);
        var existingModelNames = new HashSet<string>(stepModels.Select(s => s.StepName), StringComparer.Ordinal);

        void AddSteps(IReadOnlyList<StepModel>? steps)
        {
            if (steps is null)
            {
                return;
            }

            foreach (var step in steps)
            {
                if (existingNames.Add(step.StepName))
                {
                    stepNames.Add(step.StepName);
                }

                if (existingModelNames.Add(step.StepName))
                {
                    stepModels.Add(step);
                }
            }
        }

        foreach (var approval in approvals)
        {
            AddSteps(approval.RejectionSteps);
            AddSteps(approval.EscalationSteps);
        }

        return (stepNames, stepModels);
    }

    /// <summary>
    /// Derives the <c>OnLowConfidence</c> handler-chain steps from the step models and appends them
    /// to the step lists (they get full lowering but stay off the main linear flow), returning the
    /// lowered handler step names. Mirrors the C#-authoring path's confidence lowering.
    /// </summary>
    private static IReadOnlyList<string>? AppendConfidenceHandlerSteps(
        ref List<string> stepNames,
        ref List<StepModel> stepModels)
    {
        var handlerSteps = stepModels
            .Where(s => s.Confidence?.OnLowConfidenceHandlerChain is not null)
            .SelectMany(s => s.Confidence!.OnLowConfidenceHandlerChain!.Steps)
            .ToList();

        if (handlerSteps.Count == 0)
        {
            return null;
        }

        var confidenceHandlerStepNames = new List<string>(handlerSteps.Count);
        var existingNames = new HashSet<string>(stepNames, StringComparer.Ordinal);
        var existingModelNames = new HashSet<string>(stepModels.Select(s => s.StepName), StringComparer.Ordinal);

        foreach (var handlerStep in handlerSteps)
        {
            confidenceHandlerStepNames.Add(handlerStep.StepName);

            if (existingNames.Add(handlerStep.StepName))
            {
                stepNames.Add(handlerStep.StepName);
            }

            if (existingModelNames.Add(handlerStep.StepName))
            {
                stepModels.Add(handlerStep);
            }
        }

        return confidenceHandlerStepNames;
    }

    /// <summary>
    /// Folds each step's compensation (rollback) step TYPE into the model list — mirroring the
    /// C#-authoring compensation fold — so the compensation step gets its worker command, worker
    /// handler, completed event, and DI registration. The compensation step is folded into the step
    /// MODELS only, never into the linear step NAMES: it is reached exclusively via the saga
    /// compensation handler chain, not the happy path.
    /// </summary>
    private static List<StepModel> FoldCompensationSteps(List<StepModel> stepModels)
    {
        if (!stepModels.Any(s => s.Compensation is not null))
        {
            return stepModels;
        }

        var existingModelNames = new HashSet<string>(stepModels.Select(s => s.StepName), StringComparer.Ordinal);
        foreach (var step in stepModels.ToList())
        {
            if (step.Compensation is null)
            {
                continue;
            }

            var compTypeName = step.Compensation.CompensationStepTypeName;
            var compStepName = NamingHelper.GetSimpleTypeName(compTypeName);
            if (existingModelNames.Add(compStepName))
            {
                stepModels.Add(StepModel.Create(compStepName, compTypeName));
            }
        }

        return stepModels;
    }

    /// <summary>
    /// Resolves a wire step moniker to its CLR step symbol, recording the stable resolution
    /// diagnostic (unresolvable / ambiguous) when it does not bind to exactly one type.
    /// </summary>
    private static INamedTypeSymbol? ResolveStepSymbol(
        Compilation compilation,
        string moniker,
        string jsonFilePath,
        List<Diagnostic> diagnostics)
    {
        var resolution = WireMonikerResolver.Resolve(compilation, moniker, jsonFilePath);
        if (resolution.IsResolved)
        {
            return resolution.Symbol;
        }

        if (resolution.Diagnostic is not null)
        {
            diagnostics.Add(resolution.Diagnostic);
        }

        return null;
    }

    /// <summary>
    /// Recovers the workflow state type from a step symbol's
    /// <c>Strategos.Abstractions.IWorkflowStep&lt;TState&gt;</c> implementation. The wire IR carries
    /// no state type, so the bridge infers it from the resolved step (every step in a workflow
    /// shares one <c>TState</c>).
    /// </summary>
    private static INamedTypeSymbol? GetWorkflowStateType(INamedTypeSymbol stepSymbol)
    {
        foreach (var iface in stepSymbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            var original = iface.OriginalDefinition;
            if (string.Equals(original.MetadataName, "IWorkflowStep`1", StringComparison.Ordinal)
                && string.Equals(
                    original.ContainingNamespace?.ToDisplayString(),
                    "Strategos.Abstractions",
                    StringComparison.Ordinal))
            {
                return iface.TypeArguments.Length == 1
                    ? iface.TypeArguments[0] as INamedTypeSymbol
                    : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether <paramref name="stateSymbol"/> declares (or inherits) a public, non-static
    /// <c>Phase</c> property — the same signal the C#-authoring path computes to gate the
    /// failure-handler <c>Phase = State.Phase</c> sync.
    /// </summary>
    private static bool HasPublicPhaseProperty(INamedTypeSymbol stateSymbol)
    {
        for (var current = stateSymbol; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers("Phase"))
            {
                if (member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Parses an ISO-8601 duration string (the language-neutral form the wire projection emits via
    /// <c>XmlConvert.ToString(TimeSpan)</c>) back into a <see cref="TimeSpan"/>. Returns null for a
    /// null/empty or unparseable value so an absent or malformed duration simply carries no policy.
    /// </summary>
    private static TimeSpan? ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrEmpty(iso))
        {
            return null;
        }

        try
        {
            return System.Xml.XmlConvert.ToTimeSpan(iso!);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// The outcome of <see cref="WireToModelBridge.Bridge"/>: the lowered <see cref="WorkflowModel"/>
/// (or null when the document is not lowerable) plus any moniker-resolution diagnostics the bridge
/// surfaced.
/// </summary>
internal sealed class BridgeResult
{
    /// <summary>Initializes a new instance of the <see cref="BridgeResult"/> class.</summary>
    /// <param name="model">The lowered model, or null.</param>
    /// <param name="diagnostics">The bridge diagnostics.</param>
    public BridgeResult(WorkflowModel? model, IReadOnlyList<Diagnostic> diagnostics)
    {
        this.Model = model;
        this.Diagnostics = diagnostics;
    }

    /// <summary>Gets the lowered workflow model, or null when nothing was lowered.</summary>
    public WorkflowModel? Model { get; }

    /// <summary>Gets the moniker-resolution diagnostics the bridge surfaced.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}
