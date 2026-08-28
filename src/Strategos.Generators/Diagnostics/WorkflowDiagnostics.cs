// -----------------------------------------------------------------------
// <copyright file="WorkflowDiagnostics.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Contracts.Generated;

namespace Strategos.Generators.Diagnostics;

/// <summary>
/// Defines diagnostic descriptors for the workflow source generator.
/// </summary>
/// <remarks>
/// The diagnostic code IDs are single-sourced from <c>AgwfCatalog.tsp</c> (#52):
/// each <c>id:</c> below references a generated <see cref="AgwfCodes"/> constant
/// rather than a hand-authored <c>AGWF0xx</c> literal (enforced by the grep gate
/// / INV-5). Severities and message formats remain authored here — the catalog
/// is the single source for the code <em>identity</em>, the descriptor stays the
/// runtime reporting object.
/// </remarks>
internal static class WorkflowDiagnostics
{
    /// <summary>
    /// Diagnostic category for all workflow generator diagnostics.
    /// </summary>
    public const string Category = "Strategos";

    /// <summary>
    /// Empty workflow name.
    /// </summary>
    /// <remarks>
    /// Reported when the [Workflow] attribute is applied with an empty or whitespace-only name.
    /// </remarks>
    public static readonly DiagnosticDescriptor EmptyWorkflowName = new(
        id: AgwfCodes.EmptyWorkflowName,
        title: "Empty workflow name",
        messageFormat: "Workflow name cannot be empty or whitespace",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The workflow name specified in the [Workflow] attribute must not be empty or consist only of whitespace characters.");

    /// <summary>
    /// No workflow steps found.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow definition has no steps defined in its DSL chain.
    /// </remarks>
    public static readonly DiagnosticDescriptor NoStepsFound = new(
        id: AgwfCodes.NoStepsFound,
        title: "No workflow steps found",
        messageFormat: "Could not find any steps in workflow '{0}'. Ensure the workflow uses StartWith<T>(), Then<T>(), and Finally<T>() methods.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The workflow definition should contain at least one step defined using the fluent DSL (StartWith<T>(), Then<T>(), Finally<T>()).");

    /// <summary>
    /// Duplicate step name.
    /// </summary>
    /// <remarks>
    /// Reported when the same step type appears multiple times in a workflow.
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicateStepName = new(
        id: AgwfCodes.DuplicateStepName,
        title: "Duplicate step name",
        messageFormat: "Step '{0}' appears multiple times in workflow '{1}'. Each step type should be unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each step type in a workflow should be unique to prevent ambiguous phase transitions.");

    /// <summary>
    /// Invalid namespace.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow is declared in an invalid namespace (e.g., global namespace).
    /// </remarks>
    public static readonly DiagnosticDescriptor InvalidNamespace = new(
        id: AgwfCodes.InvalidNamespace,
        title: "Invalid namespace",
        messageFormat: "Workflow '{0}' must be declared in a namespace. Global namespace is not supported.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Workflows must be declared in a named namespace to ensure proper code generation.");

    /// <summary>
    /// Missing StartWith.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow definition does not start with StartWith&lt;T&gt;().
    /// </remarks>
    public static readonly DiagnosticDescriptor MissingStartWith = new(
        id: AgwfCodes.MissingStartWith,
        title: "Missing StartWith",
        messageFormat: "Workflow '{0}' must begin with StartWith<T>(). Found '{1}' instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every workflow definition must begin with StartWith<T>() to define the entry step. Using Then<T>() or other methods first is not supported.");

    /// <summary>
    /// Fork without Join.
    /// </summary>
    /// <remarks>
    /// Reported when a Fork construct is not followed by a Join step.
    /// </remarks>
    public static readonly DiagnosticDescriptor ForkWithoutJoin = new(
        id: AgwfCodes.ForkWithoutJoin,
        title: "Fork without Join",
        messageFormat: "Workflow '{0}' has a Fork that is not followed by Join. Every Fork must be closed with a Join<T>() call.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every Fork construct in a workflow must be followed by a Join<T>() call to merge the parallel execution paths.");

    /// <summary>
    /// Missing Finally.
    /// </summary>
    /// <remarks>
    /// Reported as a warning when a workflow does not end with Finally&lt;T&gt;().
    /// This is a warning rather than an error because some patterns may intentionally
    /// short-circuit or use Complete() in branches.
    /// </remarks>
    public static readonly DiagnosticDescriptor MissingFinally = new(
        id: AgwfCodes.MissingFinally,
        title: "Missing Finally",
        messageFormat: "Workflow '{0}' does not end with Finally<T>(). Consider adding a Finally step to mark workflow completion.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Workflows should typically end with Finally<T>() to mark completion. This is a warning because some patterns may intentionally short-circuit using Complete() in branches.");

    /// <summary>
    /// Loop without body.
    /// </summary>
    /// <remarks>
    /// Reported when a RepeatUntil loop has an empty body (no steps).
    /// </remarks>
    public static readonly DiagnosticDescriptor LoopWithoutBody = new(
        id: AgwfCodes.LoopWithoutBody,
        title: "Loop without body",
        messageFormat: "Loop '{0}' in workflow '{1}' has no steps in its body. A loop must contain at least one step.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every RepeatUntil loop must contain at least one step in its body. An empty loop body serves no purpose and is likely an error.");

    /// <summary>
    /// Invalid persistence mode.
    /// </summary>
    /// <remarks>
    /// Reported when the [Workflow] attribute specifies an unrecognized Persistence value.
    /// </remarks>
    public static readonly DiagnosticDescriptor InvalidPersistenceMode = new(
        id: AgwfCodes.InvalidPersistenceMode,
        title: "Invalid persistence mode",
        messageFormat: "Workflow '{0}' specifies an unrecognized Persistence value ({1}). Valid values are SagaDocument (0) and EventSourced (1).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Persistence property of the [Workflow] attribute must be a valid PersistenceMode value.");

    /// <summary>
    /// Event-sourced workflow requires state type.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow uses PersistenceMode.EventSourced but does not declare a state type.
    /// </remarks>
    public static readonly DiagnosticDescriptor EventSourcedRequiresState = new(
        id: AgwfCodes.EventSourcedRequiresState,
        title: "Event-sourced workflow requires state type",
        messageFormat: "Workflow '{0}' uses PersistenceMode.EventSourced but no state type was found. Event-sourced workflows require a state type that implements IEventSourcedState<TState> with an ApplyEvent method.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Event-sourced workflows require a state type to generate handlers that call State.ApplyEvent(evt). Ensure the workflow uses Workflow<TState>.Create() with a state type that implements IEventSourcedState<TState>.");

    /// <summary>
    /// Compensation step is not a workflow step (DR-8 / INV-5).
    /// </summary>
    /// <remarks>
    /// Reported when a step's <c>Compensate&lt;T&gt;</c> names a type that does not implement
    /// <c>IWorkflowStep&lt;TState&gt;</c>. The DSL's generic constraint also rejects this at the
    /// C# call site; the diagnostic gives a stable, suppressible id with a clearer message.
    /// </remarks>
    public static readonly DiagnosticDescriptor CompensateNotAStep = new(
        id: AgwfCodes.CompensateNotAStep,
        title: "Compensation step is not a workflow step",
        messageFormat: "Step '{0}' in workflow '{1}' compensates with '{2}', which does not implement IWorkflowStep<TState>. Compensation types must be a registered workflow step.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compensation (rollback) step declared via Compensate<T>() must be a registered workflow step implementing IWorkflowStep<TState> so it can be lowered into the saga compensation chain.");

    /// <summary>
    /// Confidence threshold out of range (DR-8 / INV-5).
    /// </summary>
    /// <remarks>
    /// Reported when <c>RequireConfidence(x)</c> is called with <c>x</c> outside the inclusive
    /// range [0.0, 1.0]. Mirrors the builder-runtime <see cref="System.ArgumentOutOfRangeException"/>
    /// so consumers get the same signal at compile time and can suppress it by id.
    /// </remarks>
    public static readonly DiagnosticDescriptor ConfidenceThresholdOutOfRange = new(
        id: AgwfCodes.ConfidenceThresholdOutOfRange,
        title: "Confidence threshold out of range",
        messageFormat: "Step '{0}' in workflow '{1}' calls RequireConfidence({2}). The threshold must be between 0.0 and 1.0 inclusive.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A confidence threshold expresses a probability and must lie in [0.0, 1.0]. Values outside this range cannot gate step results meaningfully.");

    /// <summary>
    /// RequireConfidence without OnLowConfidence handler (DR-8 / INV-5).
    /// </summary>
    /// <remarks>
    /// Reported when a step declares <c>RequireConfidence</c> but no corresponding
    /// <c>OnLowConfidence</c> handler. Without a handler, a low-confidence result has no
    /// routing path. A warning because some callers may intentionally fail-fast on low confidence.
    /// </remarks>
    public static readonly DiagnosticDescriptor RequireConfidenceWithoutHandler = new(
        id: AgwfCodes.RequireConfidenceWithoutHandler,
        title: "RequireConfidence without OnLowConfidence handler",
        messageFormat: "Step '{0}' in workflow '{1}' calls RequireConfidence but declares no OnLowConfidence handler. Add OnLowConfidence(alt => ...) so low-confidence results have a routing path.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A step that gates on confidence should declare an OnLowConfidence handler so that results below the threshold are routed somewhere. This is a warning because a caller may intentionally fail-fast on low confidence.");

    /// <summary>
    /// Retry maxAttempts below one (DR-8 / INV-5).
    /// </summary>
    /// <remarks>
    /// Reported when <c>WithRetry</c> is configured with <c>maxAttempts &lt; 1</c>. Mirrors the
    /// builder-runtime <see cref="System.ArgumentOutOfRangeException"/> so consumers get the same
    /// signal at compile time and can suppress it by id.
    /// </remarks>
    public static readonly DiagnosticDescriptor RetryMaxAttemptsBelowOne = new(
        id: AgwfCodes.RetryMaxAttemptsBelowOne,
        title: "Retry maxAttempts below one",
        messageFormat: "Step '{0}' in workflow '{1}' configures WithRetry({2}). The maxAttempts value must be at least 1.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A retry policy must allow at least one attempt. A maxAttempts value below 1 would prevent the step from ever executing.");

    /// <summary>
    /// Non-positive timeout (DR-8 / INV-5).
    /// </summary>
    /// <remarks>
    /// Reported when <c>WithTimeout</c> is configured with a non-positive duration
    /// (zero or negative). A non-positive deadline would expire immediately or never apply.
    /// </remarks>
    public static readonly DiagnosticDescriptor NonPositiveTimeout = new(
        id: AgwfCodes.NonPositiveTimeout,
        title: "Non-positive timeout",
        messageFormat: "Step '{0}' in workflow '{1}' configures WithTimeout with a non-positive duration. The timeout must be greater than zero.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A step timeout must be a positive duration. A zero or negative timeout cannot bound the step's execution meaningfully.");

    /// <summary>
    /// Declared-but-inert step configuration (#143, G-6).
    /// </summary>
    /// <remarks>
    /// Reported when a step declares a configuration concern that the generator does not
    /// lower for that step's kind, so the configuration silently has no effect.
    /// <para>
    /// The guarded case is confidence gating (<c>RequireConfidence</c>/<c>OnLowConfidence</c>)
    /// declared on the step an <c>AwaitApproval</c> checkpoint follows. That step's completed
    /// handler becomes the approval-request handler: it applies the reducer, moves the saga into
    /// the waiting phase and asks for the decision. The threshold comparison is never emitted, so
    /// the <c>OnLowConfidence</c> chain — which IS lowered into its own phase, start command and
    /// worker handler — has nothing that can reach it, and the step's score is ignored.
    /// </para>
    /// <para>
    /// The configure lambda is still threaded into the IR, so an out-of-range threshold on such a
    /// step continues to surface <see cref="ConfidenceThresholdOutOfRange"/>. A warning rather
    /// than an error, so an author can suppress it by id.
    /// </para>
    /// <para>
    /// Every other position where confidence can be declared lowers, and none of them is reported
    /// here: an intermediate path or loop-body step falls through to the generic completed
    /// handler, whose gate applies no position test; a fork path's last step is gated by the fork
    /// path-completed handler; a loop body's last step by the loop completed handler; and a branch
    /// case's last step — rejoining or workflow-ending alike — by the branch path-end handler.
    /// The id is retargeted as those gaps close, never renumbered or reused.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor DeclaredButInert = new(
        id: AgwfCodes.DeclaredButInert,
        title: "Declared-but-inert step configuration",
        messageFormat: "Step '{0}' in workflow '{1}' declares {2}, which the generator does not lower for this step kind, so the configuration is inert. Remove it or move the step to a position where the configuration is lowered.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A step configuration concern the generator does not lower for the step's kind is silently inert. Surfacing it prevents a deferred or unsupported configuration from masquerading as working.");

    /// <summary>
    /// Malformed workflow import JSON (DR-12).
    /// </summary>
    /// <remarks>
    /// Reported when a <c>*.workflow.json</c> AdditionalFile is not well-formed JSON. The
    /// vendored reader (<c>WireWorkflowReader</c>) throws <c>JsonParseException</c>; the
    /// import front-end catches it and reports this stable diagnostic so a malformed file
    /// surfaces as a build error rather than crashing the generator. Argument 0 is the file
    /// name; argument 1 is the parser's failure message.
    /// </remarks>
    public static readonly DiagnosticDescriptor MalformedWorkflowJson = new(
        id: AgwfCodes.MalformedWorkflowJson,
        title: "Malformed workflow import JSON",
        messageFormat: "Workflow import file '{0}' is not well-formed JSON and was skipped: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A workflow-definition JSON AdditionalFile must be well-formed JSON. Malformed input cannot be bound to the wire IR and is skipped; fix the JSON syntax so the workflow can be imported.");

    /// <summary>
    /// Unsupported workflow schema version (DR-12).
    /// </summary>
    /// <remarks>
    /// Reported when a <c>*.workflow.json</c> AdditionalFile parses successfully but declares a
    /// <c>schemaVersion</c> other than the supported <c>"1.0"</c> (including an absent version).
    /// The import front-end rejects the skew with this stable diagnostic rather than binding an
    /// incompatible shape. Argument 0 is the file name; argument 1 is the declared version.
    /// </remarks>
    public static readonly DiagnosticDescriptor UnsupportedSchemaVersion = new(
        id: AgwfCodes.UnsupportedSchemaVersion,
        title: "Unsupported workflow schema version",
        messageFormat: "Workflow import file '{0}' declares schemaVersion '{1}'. Only schemaVersion '1.0' is supported.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The workflow import front-end binds the wire IR at schema version 1.0. A file declaring a different (or missing) schemaVersion is rejected so incompatible shapes are not silently misbound.");

    /// <summary>
    /// Unresolvable workflow step moniker (DR-13).
    /// </summary>
    /// <remarks>
    /// Reported when a wire simple-name step moniker on an imported <c>*.workflow.json</c> does not
    /// resolve to any accessible <c>IWorkflowStep&lt;TState&gt;</c> type in the compilation symbol
    /// table. Argument 0 is the import file path; argument 1 is the offending moniker. The moniker is
    /// consumed as a string descriptor (INV-8) — nothing persists a CLR <see cref="System.Type"/>.
    /// </remarks>
    public static readonly DiagnosticDescriptor UnresolvableStepMoniker = new(
        id: AgwfCodes.UnresolvableStepMoniker,
        title: "Unresolvable workflow step moniker",
        messageFormat: "Workflow import file '{0}' references step moniker '{1}', which does not resolve to any accessible workflow step type in the compilation. Add the step type (implementing IWorkflowStep<TState>) or correct the moniker.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A wire step moniker must bind to exactly one accessible IWorkflowStep<TState> type in the compilation. A moniker that resolves to no such type is rejected so an import cannot silently drop a step.");

    /// <summary>
    /// Ambiguous workflow step moniker (DR-13).
    /// </summary>
    /// <remarks>
    /// Reported when a wire simple-name step moniker on an imported <c>*.workflow.json</c> resolves to
    /// two or more accessible <c>IWorkflowStep&lt;TState&gt;</c> types sharing that simple name.
    /// Argument 0 is the import file path; argument 1 is the moniker; argument 2 is the deterministic,
    /// ordinal-sorted list of all candidate fully-qualified type names.
    /// </remarks>
    public static readonly DiagnosticDescriptor AmbiguousStepMoniker = new(
        id: AgwfCodes.AmbiguousStepMoniker,
        title: "Ambiguous workflow step moniker",
        messageFormat: "Workflow import file '{0}' references step moniker '{1}', which resolves to more than one workflow step type: {2}. Rename all but one, or make the others inaccessible, so the moniker binds a single type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A wire step moniker must bind to exactly one accessible IWorkflowStep<TState> type. When two or more candidates share the simple name, the moniker is ambiguous and rejected; the candidates are listed deterministically so the collision is actionable.");

    /// <summary>
    /// Imported delegate (lambda) step is not supported (DR-14 rejection half).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> carries a delegate step (its
    /// <c>lambda</c> lossiness marker is set, LB-1). A lambda body is dropped on export and cannot
    /// be re-bound on import, so the whole workflow is rejected and NO saga is generated. Argument 0
    /// is the import file path; argument 1 is the JSON path of the offending step; argument 2 names
    /// the step. Lambda re-binding (a step registry) is a #100 follow-on.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportRejectedDelegateStep = new(
        id: AgwfCodes.ImportRejectedDelegateStep,
        title: "Imported delegate (lambda) step is not supported",
        messageFormat: "Workflow import file '{0}' declares a delegate (lambda) step at {1} (step '{2}'). A lambda step body is dropped on export (LB-1) and cannot be re-bound on import, so the workflow is rejected and no saga is generated. Replace it with a named IWorkflowStep<TState> step type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A delegate (lambda) step carries an executable body the wire IR cannot represent (LB-1). An import carrying one is rejected loudly rather than silently dropped, so a lossy workflow cannot masquerade as a working saga.");

    /// <summary>
    /// Imported branch point is not supported (DR-14 rejection half).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> declares a branch point (a conditional
    /// fan-out). A branch point carries a runtime-bound condition the import subset cannot re-bind,
    /// so the whole workflow is rejected and NO saga is generated. Argument 0 is the import file
    /// path; argument 1 is the JSON path; argument 2 names the branch point. Condition re-binding is
    /// a #100 follow-on.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportRejectedBranchPoint = new(
        id: AgwfCodes.ImportRejectedBranchPoint,
        title: "Imported branch point is not supported",
        messageFormat: "Workflow import file '{0}' declares a branch point at {1} (branch point '{2}'). A conditional branch point carries a runtime-bound condition that is not importable, so the workflow is rejected and no saga is generated.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A conditional branch point routes on a runtime-bound predicate the wire IR cannot represent. An import carrying one is rejected loudly rather than silently dropped.");

    /// <summary>
    /// Imported loop is not supported (DR-14 rejection half).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> declares a loop (a <c>RepeatUntil</c>
    /// construct). A loop carries a runtime-bound exit condition the import subset cannot re-bind,
    /// so the whole workflow is rejected and NO saga is generated. Argument 0 is the import file
    /// path; argument 1 is the JSON path; argument 2 names the loop. Condition re-binding is a #100
    /// follow-on.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportRejectedLoop = new(
        id: AgwfCodes.ImportRejectedLoop,
        title: "Imported loop is not supported",
        messageFormat: "Workflow import file '{0}' declares a loop at {1} (loop '{2}'). A RepeatUntil loop carries a runtime-bound exit condition that is not importable, so the workflow is rejected and no saga is generated.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A RepeatUntil loop terminates on a runtime-bound exit condition the wire IR cannot represent. An import carrying one is rejected loudly rather than silently dropped.");

    /// <summary>
    /// Imported validation predicate is not supported (DR-14 rejection half).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> step carries a validation guard (a
    /// declarative predicate, LB-1). The predicate has no re-bindable executable body, so the whole
    /// workflow is rejected and NO saga is generated. Argument 0 is the import file path; argument 1
    /// is the JSON path; argument 2 names the step. Condition re-binding is a #100 follow-on.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportRejectedValidationPredicate = new(
        id: AgwfCodes.ImportRejectedValidationPredicate,
        title: "Imported validation predicate is not supported",
        messageFormat: "Workflow import file '{0}' declares a validation predicate at {1} (step '{2}'). A declarative validation predicate carries no re-bindable executable body (LB-1), so the workflow is rejected and no saga is generated.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A validation guard is a declarative description of a predicate, not executable code (LB-1). An import carrying one is rejected loudly rather than silently dropped, since the guard cannot be lowered.");

    /// <summary>
    /// Imported approval context is not supported (DR-14 rejection half).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> approval carries context (its
    /// <c>hasContext</c> marker is set), an escalation handler, or a rejection handler. That
    /// behavior is dropped on export and cannot be re-bound on import, so the whole workflow is
    /// rejected and NO saga is generated. Argument 0 is the import file path; argument 1 is the JSON
    /// path; argument 2 names the approval point. Context re-binding is a #100 follow-on.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportRejectedApprovalContext = new(
        id: AgwfCodes.ImportRejectedApprovalContext,
        title: "Imported approval context is not supported",
        messageFormat: "Workflow import file '{0}' declares a context-bearing approval at {1} (approval '{2}'). Approval context is dropped on export (LB-1) and cannot be re-bound on import, so the workflow is rejected and no saga is generated. Use a context-free approval.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An approval carrying context (or an escalation / rejection handler) carries behavior the wire IR drops on export (LB-1). An import carrying it is rejected loudly rather than silently dropped; only a context-free approval is importable.");

    /// <summary>
    /// Imported gate id does not resolve (DR-3 semantic rule).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> gate step's <c>gateId</c> back-reference
    /// names an id absent from the workflow's <c>gates[]</c> declarations. The dangling reference is
    /// a semantic error, so the whole workflow is rejected and NO saga is generated. Argument 0 is
    /// the import file path; argument 1 is the JSON path; argument 2 is the dangling gate id.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportDanglingGateId = new(
        id: AgwfCodes.ImportDanglingGateId,
        title: "Imported gate id does not resolve",
        messageFormat: "Workflow import file '{0}' references gate id '{2}' at {1}, which is not declared in the workflow's gates[]. The dangling gate reference is rejected and no saga is generated; declare the gate in gates[] or remove the gateId.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A gate step's gateId must back-reference a gate declared in the workflow's gates[]. A gateId naming an absent declaration is a dangling reference and is rejected so the semantic error surfaces at import rather than silently.");

    /// <summary>
    /// Imported gate declaration carries reliability (DR-2 import-channel machine-check).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> gate declaration carries a <c>reliability</c>
    /// block. Reliability enters a definition only from measured telemetry, never from authored JSON,
    /// so the whole workflow is rejected and NO saga is generated. Argument 0 is the import file
    /// path; argument 1 is the JSON path; argument 2 names the gate declaration.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportReliabilityBearingGate = new(
        id: AgwfCodes.ImportReliabilityBearingGate,
        title: "Imported gate declaration carries reliability",
        messageFormat: "Workflow import file '{0}' declares a reliability block at {1} (gate '{2}'). Gate reliability enters a definition only from measured telemetry, never from authored JSON, so the workflow is rejected and no saga is generated. Remove the reliability block.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A gate reliability block is measured telemetry provenance, never hand-authored. An import channel is a hand-authoring surface, so a gate declaration carrying reliability is rejected to keep telemetry out of the authored definition.");

    /// <summary>
    /// Imported diagnostic-fork permitted trigger declares no required evidence fields (DR-8 evidence floor).
    /// </summary>
    /// <remarks>
    /// Reported when an imported <c>*.workflow.json</c> declares a diagnostic-fork permitted trigger
    /// whose <c>requiredEvidenceFields</c> is empty. The wire contract pins <c>@minItems(1)</c> on that
    /// list, and the C# builder's <c>PermitTrigger</c> forces at least one field — but the import path
    /// copies the list verbatim into <c>MapDiagnosticForks</c> →
    /// <c>PermittedForkTriggerModel.Create</c>, which enforces the floor by THROWING on an empty list.
    /// That unhandled throw crashes the whole generator (CS8785) and drops ALL generated output for the
    /// compilation. (Were the model floor bypassed, the emitter would instead lower a guard arm
    /// <c>ForkEvidenceComplete(cmd.Evidence)</c> with ZERO required fields — always true for any evidence
    /// map, defeating the DR-8 "no unjustified fork" invariant.) Rejecting here, before mapping, turns
    /// both failure modes into one loud, fail-closed diagnostic: the whole workflow is rejected and NO
    /// saga is generated. Argument 0 is the import file path; argument 1 is the JSON path; argument 2
    /// names the offending trigger.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportForkTriggerWithoutEvidence = new(
        id: AgwfCodes.ImportForkTriggerWithoutEvidence,
        title: "Imported fork trigger declares no required evidence fields",
        messageFormat: "Workflow import file '{0}' declares a diagnostic-fork permitted trigger at {1} (trigger '{2}') with no required evidence fields. A permitted fork trigger must declare at least one required evidence field (wire @minItems(1)) so the DR-8 no-unjustified-fork guard has an evidence floor to enforce; the workflow is rejected and no saga is generated. Add the evidence field(s) the trigger requires.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A permitted fork trigger with no required evidence fields has no DR-8 evidence floor: on import it crashes the generator (the model floor throws, CS8785), and were that bypassed the emitted occurrence guard would be always-true. The wire contract pins @minItems(1); the import channel is a hand-authoring surface, so a trigger declaring no evidence floor is rejected before mapping to fail closed with a stable diagnostic.");

    /// <summary>
    /// A workflow's main flow does not end at its declared termination (#155).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A workflow's step-name list is not purely the main flow: several lowering blocks append
    /// names to it so an off-main-flow step gets a phase, a worker handler, commands and events,
    /// even though that step is only ever reached through its own construct. Resolving a
    /// successor by list position therefore chains a main-flow step — the declared terminal
    /// above all — into a fork path, a branch case, a failure or approval handler, or a
    /// low-confidence handler chain. The saga then runs past its termination; when the step it
    /// lands on rejoins at that same terminal, it laps without bound and the saga document is
    /// never deleted.
    /// </para>
    /// <para>
    /// The whole class is decidable at emission — the generator holds both the declared terminal
    /// and each computed successor — so this reports it there. Two conditions: the declared
    /// terminal has a main-flow successor at all, or a main-flow step's computed successor is a
    /// step owned by a construct. Argument 0 is the step whose successor is wrong; argument 1 is
    /// the workflow name; argument 2 is the successor it resolved to.
    /// </para>
    /// <para>
    /// An error, not a warning: a workflow that cannot reach its termination does not run. Until
    /// this landed the only thing that caught the class was a container-backed run, which is the
    /// wrong tier for a defect the generator can see.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor UnreachableTermination = new(
        id: AgwfCodes.UnreachableTermination,
        title: "Workflow termination is unreachable",
        messageFormat: "Step '{0}' in workflow '{1}' chains to '{2}', which is not on the workflow's main flow. A step reached only through its own construct — a fork path, a branch case, a failure or approval handler, or a low-confidence handler chain — is never a main-flow successor, so the saga runs past its declared termination instead of completing.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A main-flow step whose successor is a step reached only through its own construct sends the saga past its declared termination. The generator holds both the declared terminal and each computed successor, so the whole failure class is decidable before anything runs.");

    /// <summary>
    /// Exclusive paths collide on a step type under distinct instance names (#189, #190, #191).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fork already rejects duplicate <c>EffectiveName</c>s (<see cref="DuplicateStepName"/>). Two
    /// fork paths of the same fork whose last steps share <c>StepName</c> (type) but differ in
    /// <c>InstanceName</c> compile past that check and emit duplicate
    /// <c>Handle({Type}Completed)</c> overloads (CS0111). Branch cases that share a step type
    /// under distinct instance names have the same problem: the extractor records bare type names
    /// into <c>StepNames</c>, so instance names do not disambiguate the successor map.
    /// </para>
    /// <para>
    /// An error that blocks generation: a CS0111 in generated code is worse than a diagnostic.
    /// Authors who need the same step type on exclusive paths must use distinct types.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor PathEndTypeCollision = new(
        id: AgwfCodes.PathEndTypeCollision,
        title: "Path-end type collision",
        messageFormat: "Step type '{0}' is used on more than one exclusive path in workflow '{1}' under distinct instance names. Routing maps key by step type, so instance names do not disambiguate; use distinct step types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Exclusive paths that share a step type under distinct instance names cannot be lowered: path-end handlers and successor maps key by step type, so instance names do not disambiguate and the emitter would produce duplicate Handle overloads.");
}
