// -----------------------------------------------------------------------
// <copyright file="WorkflowIncrementalGenerator.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Emitters;
using Strategos.Generators.Helpers;
using Strategos.Generators.Import;
using Strategos.Generators.Models;

namespace Strategos.Generators;

/// <summary>
/// Incremental source generator that produces Phase enums and other artifacts
/// from workflow definitions marked with [Workflow] attribute.
/// </summary>
[Generator]
public sealed class WorkflowIncrementalGenerator : IIncrementalGenerator
{
    private const string WorkflowAttributeFullName = "Strategos.Attributes.WorkflowAttribute";

    /// <summary>
    /// The file-name convention for workflow-definition JSON <c>AdditionalFiles</c> the
    /// import front-end discovers (DR-12, #100). A file participates in JSON import when its
    /// path ends with this suffix (case-insensitive).
    /// </summary>
    internal const string WorkflowDefinitionFileSuffix = ".workflow.json";

    /// <summary>The tracking name of the import file-read pipeline step (incremental-cache test hook).</summary>
    internal const string ImportReadTrackingName = "StrategosWorkflowImportRead";

    /// <summary>The tracking name of the import analysis pipeline step (incremental-cache test hook).</summary>
    internal const string ImportAnalyzeTrackingName = "StrategosWorkflowImportAnalyze";

    /// <summary>The only wire-IR schema version the import front-end binds (DR-12).</summary>
    private const string SupportedSchemaVersion = "1.0";

    /// <summary>The placeholder surfaced in a version-skew diagnostic when no schemaVersion was declared.</summary>
    private const string MissingSchemaVersionText = "(none)";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // DR-12 (#100) — the JSON import front-end (ingestion half). Discover
        // workflow-definition AdditionalFiles by the *.workflow.json convention, parse each
        // through task 026's vendored WireWorkflowReader, and surface malformed input and
        // schemaVersion skew as stable build diagnostics instead of crashing the generator.
        // The wire-IR -> WorkflowModel bridge and moniker resolution are separate tasks; here
        // the parsed DTO is only validated and its failure modes reported. The read step
        // (path + content) is separated from the analysis step so the incremental driver can
        // cache the parse when a file's content is unchanged and re-run it when it is edited.
        var workflowImports = context.AdditionalTextsProvider
            .Where(static text => IsWorkflowDefinitionFile(text.Path))
            .Select(static (text, ct) => ReadImportFile(text, ct))
            .WithTrackingName(ImportReadTrackingName)
            .Select(static (file, ct) => AnalyzeImportFile(file, ct))
            .WithTrackingName(ImportAnalyzeTrackingName);

        context.RegisterSourceOutput(
            workflowImports,
            static (spc, analysis) => ReportImportDiagnostics(spc, analysis));

        // Find all classes/structs with [Workflow] attribute
        var workflowDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WorkflowAttributeFullName,
                predicate: static (node, _) => IsValidTargetNode(node),
                transform: static (ctx, ct) => TransformToResult(ctx, ct));

        // Register source output for each workflow
        context.RegisterSourceOutput(workflowDeclarations, static (spc, result) =>
        {
            // Report diagnostics
            foreach (var diagnostic in result.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            // Generate source if model is valid
            if (result.Model is not null)
            {
                // Emit Phase enum
                var phaseSource = PhaseEnumEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PhaseEnumName}.g.cs", phaseSource);

                // Emit Commands
                var commandsSource = CommandsEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Commands.g.cs", commandsSource);

                // Emit Events
                var eventsSource = EventsEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Events.g.cs", eventsSource);

                // Emit Transitions
                var transitionsSource = TransitionsEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Transitions.g.cs", transitionsSource);

                // Emit Saga
                var sagaClassName = SagaEmitter.GetSagaClassName(result.Model);
                var sagaSource = SagaEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{sagaClassName}.g.cs", sagaSource);

                // Emit Context Assemblers (DR-6). Only steps that declared
                // .WithContext(...) produce a {Step}ContextAssembler; when no step
                // has context the emitter returns empty and no file is added, so a
                // context-free workflow keeps its prior generated-file set
                // byte-identical. The worker handler below wires each assembler into
                // its step's execution path.
                var assemblersSource = ContextAssemblerEmitter.Emit(result.Model);
                if (!string.IsNullOrWhiteSpace(assemblersSource))
                {
                    GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Assemblers.g.cs", assemblersSource);
                }

                // Emit Worker Handlers (Brain & Muscle pattern - Muscle component)
                var handlersSource = WorkerHandlerEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Handlers.g.cs", handlersSource);

                // Emit DI Extensions
                var extensionsSource = ExtensionsEmitter.Emit(result.Model);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Extensions.g.cs", extensionsSource);

                // Emit Mermaid Diagram (as C# file with diagram in raw string constant)
                var diagramContent = MermaidEmitter.Emit(result.Model);
                var diagramSource = WrapMermaidAsCSharp(result.Model, diagramContent);
                GeneratedCodeStamper.AddStampedSource(spc, $"{result.Model.PascalName}Diagram.g.cs", diagramSource);
            }
        });
    }

    private static bool IsValidTargetNode(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax or StructDeclarationSyntax;
    }

    /// <summary>
    /// Whether an <c>AdditionalFile</c> path is a workflow-definition JSON document, matched by
    /// the <see cref="WorkflowDefinitionFileSuffix"/> convention (case-insensitive).
    /// </summary>
    /// <param name="path">The AdditionalFile path.</param>
    /// <returns><see langword="true"/> when the file participates in JSON import.</returns>
    private static bool IsWorkflowDefinitionFile(string? path) =>
        path is not null && path.EndsWith(WorkflowDefinitionFileSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads an import <c>AdditionalText</c> into an equatable (path + content) value so the
    /// downstream parse step caches on content. The read is kept free of Roslyn objects
    /// (<c>SourceText</c>, <c>Location</c>) so the incremental cache key is a plain string pair.
    /// </summary>
    /// <param name="text">The workflow-definition AdditionalFile.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The path and full textual content of the file.</returns>
    private static ImportFile ReadImportFile(AdditionalText text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var content = text.GetText(ct)?.ToString() ?? string.Empty;
        return new ImportFile(text.Path, content);
    }

    /// <summary>
    /// Parses an import file through task 026's <see cref="WireWorkflowReader"/> and classifies
    /// its outcome: a <see cref="JsonParseException"/> becomes <see cref="ImportFailure.Malformed"/>;
    /// a bound document whose <c>schemaVersion</c> is not the supported <c>"1.0"</c> becomes
    /// <see cref="ImportFailure.UnsupportedSchemaVersion"/>; anything else is
    /// <see cref="ImportFailure.None"/>. The parsed DTO is intentionally discarded — the
    /// wire-IR -> WorkflowModel bridge is a separate task — but parsing is what surfaces the
    /// malformed-input failure mode without crashing the generator.
    /// </summary>
    /// <param name="file">The path + content read by <see cref="ReadImportFile"/>.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The equatable analysis result the source-output step reports from.</returns>
    private static ImportAnalysis AnalyzeImportFile(ImportFile file, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fileName = System.IO.Path.GetFileName(file.Path);

        WorkflowDefinitionV1 definition;
        try
        {
            definition = WireWorkflowReader.Read(file.Text);
        }
        catch (JsonParseException ex)
        {
            return new ImportAnalysis(fileName, ImportFailure.Malformed, ex.Message);
        }

        if (!string.Equals(definition.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            return new ImportAnalysis(
                fileName,
                ImportFailure.UnsupportedSchemaVersion,
                definition.SchemaVersion ?? MissingSchemaVersionText);
        }

        return new ImportAnalysis(fileName, ImportFailure.None, string.Empty);
    }

    /// <summary>
    /// Materializes the import diagnostic (if any) from an <see cref="ImportAnalysis"/>. The
    /// <see cref="Diagnostic"/> — and its <see cref="Location"/> — are built here, at report
    /// time, rather than in the cached analysis step, so the incremental cache value stays a
    /// plain equatable record.
    /// </summary>
    /// <param name="spc">The source-production context.</param>
    /// <param name="analysis">The classified import outcome.</param>
    private static void ReportImportDiagnostics(SourceProductionContext spc, ImportAnalysis analysis)
    {
        switch (analysis.Failure)
        {
            case ImportFailure.Malformed:
                spc.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.MalformedWorkflowJson,
                    Location.None,
                    analysis.FileName,
                    analysis.Detail));
                break;

            case ImportFailure.UnsupportedSchemaVersion:
                spc.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.UnsupportedSchemaVersion,
                    Location.None,
                    analysis.FileName,
                    analysis.Detail));
                break;

            case ImportFailure.None:
            default:
                break;
        }
    }

    private static WorkflowGeneratorResult TransformToResult(
        GeneratorAttributeSyntaxContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var diagnostics = new List<Diagnostic>();

        // Get workflow name and version from attribute
        var attribute = context.Attributes.FirstOrDefault();
        if (attribute is null || attribute.ConstructorArguments.Length < 1)
        {
            return new WorkflowGeneratorResult(null, diagnostics);
        }

        var workflowName = attribute.ConstructorArguments[0].Value as string;

        // Extract version (defaults to 1 if not provided)
        var version = 1;
        if (attribute.ConstructorArguments.Length >= 2
            && attribute.ConstructorArguments[1].Value is int v)
        {
            version = v;
        }

        // Extract persistence mode from named argument (defaults to SagaDocument)
        var persistenceMode = Models.PersistenceMode.SagaDocument;
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "Persistence" && namedArg.Value.Value is int pm)
            {
                if (!Enum.IsDefined(typeof(Models.PersistenceMode), pm))
                {
                    var location = GetAttributeLocation(context);
                    diagnostics.Add(Diagnostic.Create(
                        WorkflowDiagnostics.InvalidPersistenceMode,
                        location,
                        workflowName ?? "<unknown>",
                        pm));
                    return new WorkflowGeneratorResult(null, diagnostics);
                }

                persistenceMode = (Models.PersistenceMode)pm;
            }
        }

        // Check for empty/whitespace workflow name
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.EmptyWorkflowName,
                location));
            return new WorkflowGeneratorResult(null, diagnostics);
        }

        // Safe: IsNullOrWhiteSpace guard above ensures workflowName is non-null
        var validName = workflowName!;

        // Get namespace from symbol
        var symbol = context.TargetSymbol as INamedTypeSymbol;
        var ns = symbol?.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns) || ns == "<global namespace>")
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.InvalidNamespace,
                location,
                validName));
            return new WorkflowGeneratorResult(null, diagnostics);
        }

        // Safe: IsNullOrEmpty guard above ensures ns is non-null
        var validNs = ns!;

        // Convert kebab-case to PascalCase for enum name
        var pascalName = ToPascalCase(validName);

        // Parse step names from the DSL definition
        var stepNames = FluentDslParser.ExtractStepNames(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Extract state type name from Workflow<TState>
        var stateTypeName = FluentDslParser.ExtractStateTypeName(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Whether the state type exposes a public Phase property. Gates the
        // failure-handler Phase = State.Phase sync so a realistic state type
        // (no Phase member) never produces an uncompilable State.Phase reference.
        var stateHasPhaseProperty = FluentDslParser.StateTypeHasPhaseProperty(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Validate event-sourced mode requires a state type
        if (persistenceMode == Models.PersistenceMode.EventSourced
            && string.IsNullOrEmpty(stateTypeName))
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.EventSourcedRequiresState,
                location,
                validName));
            return new WorkflowGeneratorResult(null, diagnostics);
        }

        // Extract step models with type information
        var stepModels = FluentDslParser.ExtractStepModels(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Extract per-step context models (.WithContext(...)) so they can be folded
        // onto the matching step models (DR-6 T015). The parse already builds a
        // ContextModel per WithContext call; the merge (below) is what makes the
        // ContextAssemblerEmitter (previously dead) emit a {Step}ContextAssembler
        // and lets the worker handler assemble ontology-backed context before the
        // step runs.
        //
        // F5: the extraction stays here, but the MERGE is deferred to after ALL
        // step-model lowering completes (failure-handler steps, low-confidence
        // handler steps, compensation steps are appended to stepModels later). If
        // we merged now, .WithContext(...) declared on one of those off-main-flow
        // handler steps would never attach, because the handler step model is not
        // in stepModels yet. See the deferred merge just before the WorkflowModel
        // construction.
        var contextModels = FluentDslParser.ExtractContextModels(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Extract loop models for loop handler generation
        // Use original validName (not pascalName) to match runtime condition ID format
        var loopModels = FluentDslParser.ExtractLoopModels(
            context.TargetNode,
            context.SemanticModel,
            validName,
            ct);

        // Extract branch models for branch handler generation
        var branchModels = FluentDslParser.ExtractBranchModels(
            context.TargetNode,
            context.SemanticModel,
            pascalName,
            ct);

        // Extract fork models for parallel execution handler generation
        var forkModels = FluentDslParser.ExtractForkModels(
            context.TargetNode,
            context.SemanticModel,
            pascalName,
            ct);

        // Extract diagnostic-fork edges into generator IR (DR-9, #151). The saga lowering
        // that consumes these is deferred; here they are only parsed and attached to the model.
        var diagnosticForkModels = FluentDslParser.ExtractDiagnosticForkModels(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Extract failure handler models for saga handler generation
        var failureHandlerModels = FluentDslParser.ExtractFailureHandlerModels(
            context.TargetNode,
            context.SemanticModel,
            pascalName,
            ct);

        // Extract approval models for approval handler generation
        var approvalModels = FluentDslParser.ExtractApprovalModels(
            context.TargetNode,
            context.SemanticModel,
            validName,
            ct);

        // Include failure handler step names and step models in the overall lists
        // This ensures commands and worker handlers are generated for failure handler steps
        if (failureHandlerModels.Count > 0)
        {
            // Estimate additional capacity needed from failure handlers
            var estimatedAdditionalSteps = failureHandlerModels.Sum(h => h.StepNames.Count);
            var estimatedAdditionalModels = failureHandlerModels.Sum(h => h.Steps?.Count ?? 0);

            // Pre-allocate with estimated capacity to avoid reallocations
            var allStepNames = new List<string>(stepNames.Count + estimatedAdditionalSteps);
            allStepNames.AddRange(stepNames);
            var allStepModels = new List<StepModel>(stepModels.Count + estimatedAdditionalModels);
            allStepModels.AddRange(stepModels);

            // Use HashSet for O(1) Contains lookups instead of O(n) List.Contains
            var existingStepNames = new HashSet<string>(stepNames, StringComparer.Ordinal);
            var existingStepModelNames = new HashSet<string>(stepModels.Select(s => s.StepName), StringComparer.Ordinal);

            foreach (var handler in failureHandlerModels)
            {
                foreach (var handlerStep in handler.StepNames)
                {
                    if (!existingStepNames.Contains(handlerStep))
                    {
                        allStepNames.Add(handlerStep);
                        existingStepNames.Add(handlerStep);
                    }
                }

                // Add step models from failure handler (for worker handler generation)
                if (handler.Steps is not null)
                {
                    foreach (var handlerStepModel in handler.Steps)
                    {
                        if (!existingStepModelNames.Contains(handlerStepModel.StepName))
                        {
                            allStepModels.Add(handlerStepModel);
                            existingStepModelNames.Add(handlerStepModel.StepName);
                        }
                    }
                }
            }

            stepNames = allStepNames;
            stepModels = allStepModels;
        }

        // Include fork path step names and join step names in the overall step list
        if (forkModels.Count > 0)
        {
            // Estimate additional capacity needed from fork paths and join steps
            var estimatedAdditionalSteps = forkModels.Sum(f =>
                f.Paths.Sum(p => p.StepNames.Count) + (string.IsNullOrEmpty(f.JoinStepName) ? 0 : 1));

            // Pre-allocate with estimated capacity to avoid reallocations
            var allStepNames = new List<string>(stepNames.Count + estimatedAdditionalSteps);
            allStepNames.AddRange(stepNames);

            // Use HashSet for O(1) Contains lookups instead of O(n) List.Contains
            var existingStepNames = new HashSet<string>(stepNames, StringComparer.Ordinal);

            foreach (var fork in forkModels)
            {
                // Add fork path steps
                foreach (var path in fork.Paths)
                {
                    foreach (var pathStep in path.StepNames)
                    {
                        if (!existingStepNames.Contains(pathStep))
                        {
                            allStepNames.Add(pathStep);
                            existingStepNames.Add(pathStep);
                        }
                    }
                }

                // Add join step name for command generation
                if (!string.IsNullOrEmpty(fork.JoinStepName) && !existingStepNames.Contains(fork.JoinStepName))
                {
                    allStepNames.Add(fork.JoinStepName);
                    existingStepNames.Add(fork.JoinStepName);
                }
            }

            stepNames = allStepNames;
        }

        // Include loop exit branch step names in the overall step list
        // These are steps from Branch constructs that follow RepeatUntil loops
        if (loopModels.Count > 0)
        {
            // Estimate additional capacity needed from loop exit branches
            var estimatedAdditionalSteps = loopModels
                .Where(l => l.BranchOnExit is not null)
                .Sum(l => l.BranchOnExit!.Cases.Sum(c => c.StepNames.Count));

            // Pre-allocate with estimated capacity to avoid reallocations
            var allStepNames = new List<string>(stepNames.Count + estimatedAdditionalSteps);
            allStepNames.AddRange(stepNames);

            // Use HashSet for O(1) Contains lookups instead of O(n) List.Contains
            var existingStepNames = new HashSet<string>(stepNames, StringComparer.Ordinal);

            foreach (var loop in loopModels)
            {
                if (loop.BranchOnExit is not null)
                {
                    foreach (var branchCase in loop.BranchOnExit.Cases)
                    {
                        foreach (var branchStep in branchCase.StepNames)
                        {
                            if (!existingStepNames.Contains(branchStep))
                            {
                                allStepNames.Add(branchStep);
                                existingStepNames.Add(branchStep);
                            }
                        }
                    }
                }
            }

            stepNames = allStepNames;
        }

        // Include approval rejection/escalation step names in the overall step list
        // These are steps from OnRejection and OnTimeout handlers in AwaitApproval constructs
        if (approvalModels.Count > 0)
        {
            // Estimate additional capacity needed from approval rejection/escalation steps
            static int CountApprovalSteps(IReadOnlyList<ApprovalModel>? approvals)
            {
                if (approvals is null)
                {
                    return 0;
                }

                var count = 0;
                foreach (var approval in approvals)
                {
                    count += approval.RejectionSteps?.Count ?? 0;
                    count += approval.EscalationSteps?.Count ?? 0;
                    count += CountApprovalSteps(approval.NestedEscalationApprovals);
                }

                return count;
            }

            var estimatedAdditionalSteps = CountApprovalSteps(approvalModels);

            // Pre-allocate with estimated capacity to avoid reallocations
            var allStepNames = new List<string>(stepNames.Count + estimatedAdditionalSteps);
            allStepNames.AddRange(stepNames);
            var allStepModels = new List<StepModel>(stepModels.Count + estimatedAdditionalSteps);
            allStepModels.AddRange(stepModels);

            // Use HashSet for O(1) Contains lookups instead of O(n) List.Contains
            var existingStepNamesSet = new HashSet<string>(stepNames, StringComparer.Ordinal);
            var existingStepModelNames = new HashSet<string>(stepModels.Select(s => s.StepName), StringComparer.Ordinal);

            void AddApprovalSteps(IReadOnlyList<ApprovalModel>? approvals)
            {
                if (approvals is null)
                {
                    return;
                }

                foreach (var approval in approvals)
                {
                    // Add rejection steps
                    if (approval.RejectionSteps is not null)
                    {
                        foreach (var step in approval.RejectionSteps)
                        {
                            if (!existingStepNamesSet.Contains(step.StepName))
                            {
                                allStepNames.Add(step.StepName);
                                existingStepNamesSet.Add(step.StepName);
                            }

                            if (!existingStepModelNames.Contains(step.StepName))
                            {
                                allStepModels.Add(step);
                                existingStepModelNames.Add(step.StepName);
                            }
                        }
                    }

                    // Add escalation steps
                    if (approval.EscalationSteps is not null)
                    {
                        foreach (var step in approval.EscalationSteps)
                        {
                            if (!existingStepNamesSet.Contains(step.StepName))
                            {
                                allStepNames.Add(step.StepName);
                                existingStepNamesSet.Add(step.StepName);
                            }

                            if (!existingStepModelNames.Contains(step.StepName))
                            {
                                allStepModels.Add(step);
                                existingStepModelNames.Add(step.StepName);
                            }
                        }
                    }

                    // Recursively process nested approvals
                    AddApprovalSteps(approval.NestedEscalationApprovals);
                }
            }

            AddApprovalSteps(approvalModels);
            stepNames = allStepNames;
            stepModels = allStepModels;
        }

        // Include low-confidence handler step names and step models in the overall
        // lists (DR-5). A step's .RequireConfidence(t).OnLowConfidence(alt => alt.Then<H>())
        // captures the handler step H only as a ConfidenceModel descriptor; lowering it here
        // (mirroring failure-handler / approval step lowering) gives H its own phase, worker
        // handler, start/completed commands and events, and a terminal saga completed handler.
        // The saga's confidence-gated completed handler then routes to Start{H}Command via a
        // Wolverine cascade when the result confidence is below the threshold (INV-1).
        // Lower EVERY step in each OnLowConfidence handler chain (G-4 / #139), in order. Before
        // #139 only the first Then<T> was lowered; a multi-step chain now contributes all of its
        // steps so the chain runs end to end. The chain's ordered Steps are the source of truth;
        // OnLowConfidenceHandlerStep (the first step) is retained only for the saga routing surface.
        var confidenceHandlerSteps = stepModels
            .Where(s => s.Confidence?.OnLowConfidenceHandlerChain is not null)
            .SelectMany(s => s.Confidence!.OnLowConfidenceHandlerChain!.Steps)
            .ToList();

        // DR-4 (#145 gap A): a fork-path step's confidence gate lowers into the fork
        // PATH-COMPLETED handler (the LAST step of each path — "the fork handler"). Lower
        // that step's OnLowConfidence handler chain exactly like a top-level gated step's,
        // so Start{H}Command, the worker handler, the phase, the events and a terminal
        // completed handler all exist and the path handler can cascade to the chain
        // (INV-1). Only the last step of each path is gated here; an intermediate
        // (non-last) fork-path step's confidence stays inert and is structurally guarded.
        foreach (var fork in forkModels)
        {
            foreach (var path in fork.Paths)
            {
                if (path.Steps.Count == 0)
                {
                    continue;
                }

                var lastStep = path.Steps[path.Steps.Count - 1];
                if (lastStep.Confidence?.OnLowConfidenceHandlerChain is not null)
                {
                    confidenceHandlerSteps.AddRange(lastStep.Confidence.OnLowConfidenceHandlerChain.Steps);
                }
            }
        }

        // Track the lowered handler step names so the saga emitter can keep them off the
        // main linear flow (they must not displace the workflow's terminal step nor be
        // chained to as a normal "next" step).
        List<string>? confidenceHandlerStepNames = null;

        if (confidenceHandlerSteps.Count > 0)
        {
            confidenceHandlerStepNames = new List<string>(confidenceHandlerSteps.Count);
            var allStepNames = new List<string>(stepNames.Count + confidenceHandlerSteps.Count);
            allStepNames.AddRange(stepNames);
            var allStepModels = new List<StepModel>(stepModels.Count + confidenceHandlerSteps.Count);
            allStepModels.AddRange(stepModels);

            var existingStepNames = new HashSet<string>(stepNames, StringComparer.Ordinal);
            var existingStepModelNames = new HashSet<string>(stepModels.Select(s => s.StepName), StringComparer.Ordinal);

            foreach (var handlerStep in confidenceHandlerSteps)
            {
                confidenceHandlerStepNames.Add(handlerStep.StepName);

                if (!existingStepNames.Contains(handlerStep.StepName))
                {
                    allStepNames.Add(handlerStep.StepName);
                    existingStepNames.Add(handlerStep.StepName);
                }

                if (!existingStepModelNames.Contains(handlerStep.StepName))
                {
                    allStepModels.Add(handlerStep);
                    existingStepModelNames.Add(handlerStep.StepName);
                }
            }

            stepNames = allStepNames;
            stepModels = allStepModels;
        }

        // Check for missing steps
        if (stepNames.Count == 0)
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.NoStepsFound,
                location,
                validName));
        }

        // Context-aware duplicate step detection
        // Use raw steps (no deduplication) with execution context to detect problematic duplicates:
        // - Duplicates in Linear context: ERROR (same step twice in main flow)
        // - Duplicates in ForkPath context: ERROR (same step in parallel paths causes routing issues)
        // - Duplicates in BranchPath context: OK (same step in exclusive paths - only one executes)
        var rawSteps = FluentDslParser.ExtractRawStepInfos(
            context.TargetNode,
            context.SemanticModel,
            ct);

        // Find duplicates only in non-BranchPath contexts
        // Use EffectiveName (= InstanceName ?? StepName) to allow same step type with different instance names
        var nonBranchSteps = rawSteps.Where(s => s.Context != Helpers.StepContext.BranchPath);
        var duplicateSteps = nonBranchSteps
            .GroupBy(s => s.EffectiveName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicateSteps)
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.DuplicateStepName,
                location,
                duplicate,
                validName));
        }

        // Validate workflow starts with StartWith<T>()
        var (hasStartWith, firstMethodName) = FluentDslParser.ValidateStartsWith(
            context.TargetNode,
            context.SemanticModel,
            ct);

        if (!hasStartWith && firstMethodName is not null)
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.MissingStartWith,
                location,
                validName,
                firstMethodName));
        }

        // Validate that all Fork constructs are followed by Join
        var hasForkWithoutJoin = forkModels.Any(f => string.IsNullOrEmpty(f.JoinStepName));
        if (hasForkWithoutJoin)
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.ForkWithoutJoin,
                location,
                validName));
        }

        // Validate workflow ends with Finally<T>() (Warning only - does not block generation)
        var (hasFinally, hasSteps) = FluentDslParser.ValidateEndsWith(
            context.TargetNode,
            context.SemanticModel,
            ct);

        if (!hasFinally && hasSteps)
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.MissingFinally,
                location,
                validName));
        }

        // Validate all loops have non-empty bodies
        var emptyLoops = FluentDslParser.FindEmptyLoops(
            context.TargetNode,
            context.SemanticModel,
            ct);

        foreach (var emptyLoopName in emptyLoops)
        {
            var location = GetAttributeLocation(context);
            diagnostics.Add(Diagnostic.Create(
                WorkflowDiagnostics.LoopWithoutBody,
                location,
                emptyLoopName,
                validName));
        }

        // Validate per-step resilience configuration (DR-8 / INV-5; the resilience AGWF
        // diagnostics). These are advisory signals over the parsed IR (StepModel.Retry/
        // Timeout/Compensation/Confidence): some mirror builder-runtime throws (retry < 1,
        // confidence ∉ [0,1]) so consumers get the same signal at compile time and can
        // suppress it by id; others (non-positive timeout, RequireConfidence without
        // OnLowConfidence, Compensate<T> non-step) are net-new. They do not gate code
        // generation — the builder runtime / C# generic constraint already enforce them.
        ReportResilienceDiagnostics(stepModels, forkModels, loopModels, validName, GetAttributeLocation(context), diagnostics);

        // Return null model (no code generation) when there are errors
        var hasErrors = duplicateSteps.Count > 0
            || (!hasStartWith && firstMethodName is not null)
            || hasForkWithoutJoin
            || emptyLoops.Count > 0;
        if (hasErrors)
        {
            return new WorkflowGeneratorResult(null, diagnostics);
        }

        // Fold compensation (rollback) step TYPES into the step-model list so the
        // emitters that key off model.Steps (worker handler, worker command,
        // completed event, DI registration) produce the artifacts that let the
        // rollback step RUN via the proven main-flow worker dispatch (DR-3 T008).
        //
        // Crucially the compensation step is folded into the step MODELS only, NOT
        // into stepNames: stepNames is the saga's linear chain, so adding it there
        // would run the rollback on the happy path. The compensation step is reached
        // exclusively via the saga compensation handler chain
        // (SagaCompensationComponentEmitter), which dispatches its worker command
        // only when the trigger failure-handler command arrives.
        if (stepModels.Any(s => s.Compensation is not null))
        {
            var allStepModels = new List<StepModel>(stepModels);
            var existingModelNames = new HashSet<string>(
                stepModels.Select(s => s.StepName),
                StringComparer.Ordinal);

            foreach (var step in stepModels)
            {
                if (step.Compensation is null)
                {
                    continue;
                }

                var compTypeName = step.Compensation.CompensationStepTypeName;
                var compStepName = NamingHelper.GetSimpleTypeName(compTypeName);

                if (existingModelNames.Add(compStepName))
                {
                    allStepModels.Add(StepModel.Create(compStepName, compTypeName));
                }
            }

            stepModels = allStepModels;
        }

        // Deferred context merge (F5): fold each .WithContext(...) ContextModel onto
        // its declaring step model AFTER all step-model lowering has completed —
        // failure-handler steps, low-confidence handler steps, and compensation
        // steps are all in stepModels by now. Merging earlier left context declared
        // on those off-main-flow handler steps unattached, so the assembler was
        // never emitted (F5) and never registered (F3, ExtensionsEmitter). The
        // ContextModel is keyed by the declaring step's name, so it binds to the
        // matching step regardless of where in the flow the step lives.
        if (contextModels.Count > 0)
        {
            var contextByStep = new Dictionary<string, ContextModel>(StringComparer.Ordinal);
            foreach (var (stepName, contextModel) in contextModels)
            {
                contextByStep[stepName] = contextModel;
            }

            var mergedStepModels = new List<StepModel>(stepModels.Count);
            foreach (var step in stepModels)
            {
                if (step.Context is null && contextByStep.TryGetValue(step.StepName, out var ctxModel))
                {
                    mergedStepModels.Add(step with { Context = ctxModel });
                }
                else
                {
                    mergedStepModels.Add(step);
                }
            }

            stepModels = mergedStepModels;
        }

        var model = new WorkflowModel(
            WorkflowName: validName,
            PascalName: pascalName,
            Namespace: validNs,
            StepNames: stepNames,
            StateTypeName: stateTypeName,
            Version: version,
            PersistenceMode: persistenceMode,
            Steps: stepModels,
            Loops: loopModels,
            Branches: branchModels,
            FailureHandlers: failureHandlerModels,
            Forks: forkModels,
            ApprovalPoints: approvalModels,
            ConfidenceHandlerStepNames: confidenceHandlerStepNames,
            DiagnosticForks: diagnosticForkModels)
        {
            StateHasPhaseProperty = stateHasPhaseProperty,
        };

        return new WorkflowGeneratorResult(model, diagnostics);
    }

    /// <summary>
    /// Reports the per-step resilience diagnostics (DR-8 / INV-5) over the parsed step
    /// models. Each reported diagnostic carries a stable, suppressible AGWF id.
    /// </summary>
    /// <param name="stepModels">The parsed step models whose resilience IR is validated.</param>
    /// <param name="forkModels">The parsed fork models, used to detect declared-but-inert config on fork-path steps.</param>
    /// <param name="loopModels">The parsed loop models, used to detect declared-but-inert config on intermediate loop-body steps.</param>
    /// <param name="workflowName">The validated workflow name, threaded into messages.</param>
    /// <param name="location">The diagnostic location (the workflow attribute).</param>
    /// <param name="diagnostics">The diagnostics accumulator to append to.</param>
    private static void ReportResilienceDiagnostics(
        IReadOnlyList<StepModel> stepModels,
        IReadOnlyList<ForkModel> forkModels,
        IReadOnlyList<LoopModel> loopModels,
        string workflowName,
        Location location,
        List<Diagnostic> diagnostics)
    {
        foreach (var step in stepModels)
        {
            // CompensateNotAStep — Compensate<T> where T is not a registered IWorkflowStep<TState>.
            if (step.Compensation is { IsRegisteredStep: false } compensation)
            {
                diagnostics.Add(Diagnostic.Create(
                    WorkflowDiagnostics.CompensateNotAStep,
                    location,
                    step.EffectiveName,
                    workflowName,
                    compensation.CompensationStepTypeName));
            }

            if (step.Confidence is { } confidence)
            {
                // ConfidenceThresholdOutOfRange — RequireConfidence(x) with x outside [0.0, 1.0].
                if (confidence.Threshold < 0.0 || confidence.Threshold > 1.0)
                {
                    diagnostics.Add(Diagnostic.Create(
                        WorkflowDiagnostics.ConfidenceThresholdOutOfRange,
                        location,
                        step.EffectiveName,
                        workflowName,
                        confidence.Threshold));
                }

                // RequireConfidenceWithoutHandler — RequireConfidence with no OnLowConfidence handler.
                if (confidence.OnLowConfidenceHandlerStep is null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        WorkflowDiagnostics.RequireConfidenceWithoutHandler,
                        location,
                        step.EffectiveName,
                        workflowName));
                }
            }

            // RetryMaxAttemptsBelowOne — retry maxAttempts < 1.
            if (step.Retry is { } retry && retry.MaxAttempts < 1)
            {
                diagnostics.Add(Diagnostic.Create(
                    WorkflowDiagnostics.RetryMaxAttemptsBelowOne,
                    location,
                    step.EffectiveName,
                    workflowName,
                    retry.MaxAttempts));
            }

            // NonPositiveTimeout — non-positive WithTimeout.
            if (step.Timeout is { } timeout && timeout.Timeout <= TimeSpan.Zero)
            {
                diagnostics.Add(Diagnostic.Create(
                    WorkflowDiagnostics.NonPositiveTimeout,
                    location,
                    step.EffectiveName,
                    workflowName));
            }
        }

        // DeclaredButInert (#143, G-6) — confidence gating on an INTERMEDIATE (non-last)
        // fork-path step. Confidence on the LAST step of a fork path now lowers into the
        // fork path-completed handler (DR-4 / #145 gap A), so it is NOT flagged. An
        // intermediate fork-path step, however, still runs through the generic completed
        // handler with no confidence gate: its configure lambda reaches the IR (so an
        // out-of-range threshold still surfaces the ConfidenceThresholdOutOfRange code),
        // but no confidence gate and no OnLowConfidence routing reach the generated saga.
        // That variant remains deferred (v2.10.0 / DR-17, #134). Surface it so a deferred
        // configuration cannot masquerade as working.
        foreach (var fork in forkModels)
        {
            foreach (var path in fork.Paths)
            {
                for (var i = 0; i < path.Steps.Count; i++)
                {
                    var isLastStepInPath = i == path.Steps.Count - 1;
                    var forkStep = path.Steps[i];

                    if (!isLastStepInPath && forkStep.Confidence is not null)
                    {
                        diagnostics.Add(Diagnostic.Create(
                            WorkflowDiagnostics.DeclaredButInert,
                            location,
                            forkStep.EffectiveName,
                            workflowName,
                            "confidence gating (RequireConfidence/OnLowConfidence) on an intermediate fork-path step"));
                    }
                }
            }
        }

        // DeclaredButInert (#145 gap B) — confidence gating on an INTERMEDIATE (non-last)
        // loop-body step. Confidence on the LAST step of a loop body now lowers into the loop
        // completed handler (DR-5 / #145 gap B, mirroring the fork path-completed handler), so
        // it is NOT flagged. An intermediate loop-body step's confidence lowering is deferred
        // (v2.10.0 / DR-17, #145). Task 009 promoted the loop body to configured StepModel
        // records on LoopModel.BodySteps, so — unlike before, when loop-body confidence was
        // dropped from the IR entirely and was structurally undiagnosable — the config is now in
        // the IR and this diagnostic can see it. Surface it so a deferred configuration cannot
        // masquerade as working.
        foreach (var loop in loopModels)
        {
            for (var i = 0; i < loop.BodySteps.Count; i++)
            {
                var isLastBodyStep = i == loop.BodySteps.Count - 1;
                var bodyStep = loop.BodySteps[i];

                if (!isLastBodyStep && bodyStep.Confidence is not null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        WorkflowDiagnostics.DeclaredButInert,
                        location,
                        bodyStep.EffectiveName,
                        workflowName,
                        "confidence gating (RequireConfidence/OnLowConfidence) on an intermediate loop-body step"));
                }
            }
        }
    }

    private static Location GetAttributeLocation(GeneratorAttributeSyntaxContext context)
    {
        // Try to get the attribute syntax location
        var attributeList = context.TargetNode
            .DescendantNodes()
            .OfType<AttributeListSyntax>()
            .FirstOrDefault();

        return attributeList?.GetLocation() ?? context.TargetNode.GetLocation();
    }

    private static string WrapMermaidAsCSharp(WorkflowModel model, string mermaidContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Mermaid state diagram for the {model.WorkflowName} workflow.");
        sb.AppendLine("/// Copy the content of the Diagram field to a Mermaid renderer to visualize.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"internal static partial class {model.PascalName}Diagram");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// The Mermaid state diagram source.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public const string Diagram = \"\"\"");
        sb.Append(mermaidContent);
        sb.AppendLine("\"\"\";");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToPascalCase(string kebabCase)
    {
        if (string.IsNullOrEmpty(kebabCase))
        {
            return string.Empty;
        }

        var parts = kebabCase.Split('-');
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1).ToLowerInvariant());
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Result of transforming a workflow declaration, including model and diagnostics.
    /// </summary>
    private sealed record WorkflowGeneratorResult(
        WorkflowModel? Model,
        IReadOnlyList<Diagnostic> Diagnostics);

    /// <summary>
    /// The equatable (path + content) snapshot of a workflow-definition AdditionalFile. Value
    /// equality on these two strings is the import pipeline's incremental cache key: an unchanged
    /// file yields an equal <see cref="ImportFile"/> and the parse step is cached; an edit yields
    /// a different content string and the parse re-runs (DR-12 incremental correctness).
    /// </summary>
    /// <param name="Path">The AdditionalFile path.</param>
    /// <param name="Text">The full textual content of the file.</param>
    private sealed record ImportFile(string Path, string Text);

    /// <summary>The classified outcome of parsing a workflow-definition import file.</summary>
    private enum ImportFailure
    {
        /// <summary>The file parsed and declared the supported schema version — no diagnostic.</summary>
        None,

        /// <summary>The file is not well-formed JSON (the reader threw <c>JsonParseException</c>).</summary>
        Malformed,

        /// <summary>The file parsed but declared a <c>schemaVersion</c> other than the supported one.</summary>
        UnsupportedSchemaVersion,
    }

    /// <summary>
    /// The equatable classification of a single import file, produced by the cached analysis step
    /// and consumed by the source-output step. Carries only strings + an enum so it stays a stable
    /// incremental cache value (no <see cref="Diagnostic"/> or <see cref="Location"/>).
    /// </summary>
    /// <param name="FileName">The file's leaf name, threaded into the diagnostic message.</param>
    /// <param name="Failure">The classified failure mode (or <see cref="ImportFailure.None"/>).</param>
    /// <param name="Detail">The failure detail — the parser message, or the offending schema version.</param>
    private sealed record ImportAnalysis(string FileName, ImportFailure Failure, string Detail);
}
