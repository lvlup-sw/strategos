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
    /// Reported when the same EffectiveName appears multiple times in a workflow.
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicateStepName = new(
        id: AgwfCodes.DuplicateStepName,
        title: "Duplicate step name",
        messageFormat: "Step '{0}' appears multiple times in workflow '{1}'. Each EffectiveName (the instance name, or the step type when none is given) must be unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each EffectiveName in a workflow must be unique to prevent last-write-win routing. The same step type under distinct instance names is a type collision, not a duplicate name.");

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
    /// and each computed successor — so this reports it there. Over-reach: the declared terminal
    /// has a main-flow successor at all, or a main-flow step's computed successor is a step
    /// owned by a construct. Under-reach: a rejoin construct's last step does not dispatch the
    /// declared terminal. Argument 0 is the declared terminal (under-reach) or the step whose
    /// successor is wrong (over-reach); argument 1 is the workflow name; argument 2 is the other
    /// step of the broken pair — the successor it resolved to, or the last step that should have
    /// dispatched the terminal.
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
        messageFormat: "Workflow '{1}' has an unreachable-termination pair '{0}' / '{2}'. Either a main-flow step chains to an off-flow successor, or a rejoin last step never dispatches the declared terminal.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Either a main-flow step chains to an off-flow successor, or a rejoin last step never dispatches the declared terminal. The generator holds both the declared terminal and each computed successor, so the whole failure class is decidable before anything runs.");

    /// <summary>
    /// Historical catalog member for exclusive-path type collision (#189, #190, #191).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept so the catalog identity shipped in 0.7.0 remains stable. The generator no longer
    /// reports this descriptor: fork path instances that share a type bind
    /// <c>Handle({PhaseName}Completed)</c> (the same phase string <c>Start{PhaseName}Command</c>
    /// already uses), and branch completions keep one <c>Handle({StepType}Completed)</c> that
    /// routes by the live case. Same <c>EffectiveName</c> on two fork or linear steps is still
    /// <see cref="DuplicateStepName"/>.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor PathEndTypeCollision = new(
        id: AgwfCodes.PathEndTypeCollision,
        title: "Path-end type collision",
        messageFormat: "Step type '{0}' is used on more than one exclusive path in workflow '{1}' under distinct instance names. Routing maps key by step type, so instance names do not disambiguate; use distinct step types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Exclusive paths that share a step type under distinct instance names cannot be lowered: completed handlers and successor maps key by step type, so instance names do not disambiguate and the emitter would produce duplicate Handle overloads.");

    /// <summary>
    /// Two <c>PermitTrigger</c> declarations on one diagnostic-fork edge name the same trigger (#156.2).
    /// </summary>
    /// <remarks>
    /// Reported when a C# <c>AllowDiagnosticFork</c> chain or an imported
    /// <c>*.workflow.json</c> permits the same closed trigger more than once on one edge.
    /// The runtime builder already refuses a second <c>PermitTrigger</c> for the same
    /// <c>ForkTrigger</c>; the generator previously accepted the pair and the emitter's
    /// per-trigger switch then failed closed as CS0152. Two same-trigger declarations can
    /// carry different evidence schemas, so first-wins dedup would silently drop one
    /// schema. Reject the whole workflow (no saga) with this dedicated id instead.
    /// Argument 0 is the workflow name (C#) or import file path (JSON); argument 1 is the
    /// edge position (<c>AllowDiagnosticFork</c> or the JSON path); argument 2 is the
    /// duplicated trigger name.
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicatePermittedForkTrigger = new(
        id: AgwfCodes.DuplicatePermittedForkTrigger,
        title: "Duplicate permitted fork trigger",
        messageFormat: "Workflow '{0}' declares permitted trigger '{2}' more than once on one diagnostic-fork edge at {1}. Two same-trigger declarations can carry different evidence schemas; declare each trigger at most once.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A diagnostic-fork edge may permit each closed trigger at most once. Two same-trigger declarations can carry different evidence schemas, so the pair is rejected rather than silently deduplicated.");

    /// <summary>
    /// Two diagnostic-fork edges share a sanitized compensation-seed moniker (#156.3).
    /// </summary>
    /// <remarks>
    /// Reported when a C# <c>AllowDiagnosticFork</c> chain or an imported
    /// <c>*.workflow.json</c> declares two edges whose compensation seeds sanitize to
    /// the same <c>DiagnosticForkCount_{seed}</c> key (the same '-' → '_' sanitizer
    /// used for <c>Fork_{id}_Path{n}State</c>). Sharing a counter would let one
    /// edge's <c>maxForks</c> bound starve the other; reject the workflow (no saga)
    /// instead. Argument 0 is the workflow name (C#) or import file path (JSON);
    /// argument 1 is the edge position (<c>AllowDiagnosticFork</c> or the JSON path);
    /// argument 2 is the colliding compensation seed.
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicateCompensationSeed = new(
        id: AgwfCodes.DuplicateCompensationSeed,
        title: "Duplicate diagnostic-fork compensation seed",
        messageFormat: "Workflow '{0}' declares compensation seed '{2}' more than once on diagnostic-fork edges at {1}. Two edges that share a seed cannot share a DiagnosticForkCount counter; use distinct compensation seeds.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Two diagnostic-fork edges that sanitize to the same compensation-seed key cannot share a DiagnosticForkCount saga property; the pair is rejected rather than merged onto one counter.");

    /// <summary>A bound action's workflow name does not resolve exactly once.</summary>
    public static readonly DiagnosticDescriptor BoundWorkflowNotFound = new(
        id: AgwfCodes.BoundWorkflowNotFound,
        title: "Bound workflow name does not resolve exactly once",
        messageFormat: "Ontology action '{0}' is bound to workflow '{1}', but that name resolves to {2} workflow definitions in this compilation. Declare exactly one C# or imported workflow with that ordinal name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A workflow-bound ontology action must resolve exactly one workflow model by ordinal workflow name in the compilation being built. A referenced assembly's proof catalog carries action contracts, never workflow models, so a binding whose workflow is absent here is deferred to the compilation that lowers it rather than resolved from a catalog. This diagnostic was configurable while a cross-assembly layout had no other exit; exporting the contract is that exit, so the exemption is withdrawn and an unresolved binding is not silenceable.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>A reachable workflow step has no exact static action identity.</summary>
    public static readonly DiagnosticDescriptor WorkflowActionReferenceInvalid = new(
        id: AgwfCodes.WorkflowActionReferenceInvalid,
        title: "Workflow step action reference is invalid",
        messageFormat: "Reachable step '{0}' in workflow '{1}' has {2}. Every reachable step must carry one statically closed action reference that resolves exactly once by (DomainName, ObjectTypeName, ActionName).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every reachable occurrence in a workflow-bound action must declare one closed action reference whose ordinal three-part identity resolves exactly once, here or through a referenced assembly's exported proof catalog. This diagnostic was configurable for the same cross-assembly reason as the bound-workflow-not-found diagnostic, and loses the exemption with it.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>A closed workflow binding has a definite refinement counterexample.</summary>
    public static readonly DiagnosticDescriptor WorkflowBindingRefinementFailed = new(
        id: AgwfCodes.WorkflowBindingRefinementFailed,
        title: "Workflow binding refinement proof failed",
        messageFormat: "Workflow '{0}' does not refine bound action '{1}': {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A closed workflow binding violates a precondition, postcondition, frame, authority, subject, or fork noninterference refinement obligation.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>A workflow binding contains a contract outside the closed proof fragment.</summary>
    public static readonly DiagnosticDescriptor WorkflowContractUnprovable = new(
        id: AgwfCodes.WorkflowContractUnprovable,
        title: "Workflow contract cannot be proved statically",
        messageFormat: "Workflow '{0}' bound to action '{1}' cannot be proved statically: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Workflow binding proof fails closed when an action contract, resource domain, or authority lattice is dynamic, opaque, invalid, or outside the exact finite proof fragment.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>Two workflow identities normalize to the same generated type and hint names.</summary>
    public static readonly DiagnosticDescriptor WorkflowEmissionIdentityCollision = new(
        id: AgwfCodes.WorkflowEmissionIdentityCollision,
        title: "Workflow generated identity collision",
        messageFormat: "Workflow identities [{0}] all normalize to generated name '{1}' and cannot be emitted together. Rename them so every generated PascalCase workflow name is unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Distinct ordinal workflow identities must not normalize to the same generated PascalCase type and source-hint namespace.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>
    /// This compilation declares a workflow binding whose contract it cannot export
    /// for a referencing compilation to prove (#204).
    /// </summary>
    /// <remarks>
    /// The producing side of the cross-assembly seam. A binding whose contract cannot
    /// leave the assembly is a binding nothing downstream can discharge, and the
    /// producing build is the only place that fact is knowable — the consumer sees an
    /// absence, not a reason. Argument 0 is the action identity; argument 1 is why the
    /// export failed.
    /// </remarks>
    public static readonly DiagnosticDescriptor ProofCatalogExportIncomplete = new(
        id: AgwfCodes.ProofCatalogExportIncomplete,
        title: "Proof catalog export is incomplete",
        messageFormat: "This assembly declares workflow binding '{0}' but cannot export its contract: {1}. A binding whose contract no referencing compilation can read is a binding nothing can prove; correct the contract or remove the binding.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A workflow binding whose action contract cannot be projected into the portable proof catalog is refused at the declaring assembly, because no referencing compilation can prove it.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>A referenced assembly's proof catalog could not be read (#204).</summary>
    /// <remarks>
    /// Covers every way a catalog can fail to be trustworthy: an unknown
    /// <c>schemaVersion</c>, malformed JSON, a predicate or literal discriminator this
    /// compiler does not know, an authority the catalog's own lattice does not define,
    /// and a content hash that does not match the bytes. All of them are refusals
    /// rather than omissions — an unreadable catalog that were merely skipped would
    /// silently drop every obligation it carries and leave the build green. Argument 0
    /// is the declaring assembly; argument 1 is the reason.
    /// </remarks>
    public static readonly DiagnosticDescriptor ProofCatalogUnreadable = new(
        id: AgwfCodes.ProofCatalogUnreadable,
        title: "Referenced proof catalog is unreadable",
        messageFormat: "The proof catalog exported by '{0}' cannot be read: {1}. An unreadable catalog is refused rather than skipped, because skipping it would silently drop every binding obligation it carries.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A referenced proof catalog with an unknown version, an unknown discriminator, an incomplete authority lattice, or a content hash that does not match its bytes is refused; it is never partially read.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>Two assemblies declare the same ordinal action identity (#204).</summary>
    /// <remarks>
    /// Identity is ordinal and three-part, and the merge has no tie-break to apply:
    /// picking a winner would make the proved contract depend on reference order.
    /// Argument 0 is the action identity; arguments 1 and 2 are the two declaring
    /// catalogs (this compilation is named as its own assembly).
    /// </remarks>
    public static readonly DiagnosticDescriptor ProofCatalogDuplicateIdentity = new(
        id: AgwfCodes.ProofCatalogDuplicateIdentity,
        title: "Duplicate action identity across proof catalogs",
        messageFormat: "Action '{0}' is declared by both '{1}' and '{2}'. Two assemblies claiming one ordinal action identity is an ambiguity no merge can resolve; give the action one declaring assembly.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "One ordinal action identity must have one declaring assembly. A duplicate across the merge is refused rather than resolved by reference order.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>
    /// A wire slot typed by a closed contract enum carries a value outside that
    /// enum's vocabulary (#221).
    /// </summary>
    /// <remarks>
    /// The wire-DTO twins carry these slots as plain strings (INV-8: the polyglot
    /// identity is the VALUE, never a CLR enum handle), so nothing in the import
    /// path rejected an unknown token — the same failure mode the dangling-gateId
    /// check exists to catch, one level down. The accepted set is read from the
    /// emitted schema through the linked <c>WireEnumVocabularies</c> projection,
    /// never re-typed here, so adding a member cannot leave this check behind.
    /// Argument 0 is the import file; 1 names the construct; 2 is the JSON path;
    /// 3 is the enum; 4 is its vocabulary.
    /// </remarks>
    public static readonly DiagnosticDescriptor ImportClosedEnumValueUnknown = new(
        id: AgwfCodes.ImportClosedEnumValueUnknown,
        title: "Closed-enum wire slot carries an unknown value",
        messageFormat: "Import file '{0}' declares {1} at {2}. A closed enum is not an extension point — both runtimes match {3} BY VALUE, so check for a member name used in place of its wire value (PascalCase for snake_case), or a class retired in an earlier contract major. The {3} vocabulary is exactly: {4}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A closed contract enum is not an extension point. Both runtimes match these slots by wire value, so a token outside the vocabulary — a member name used in place of its value, or a class retired in an earlier contract major — is refused at the boundary rather than carried into the IR.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>An authored compensation action does not implement the derived inverse contract.</summary>
    public static readonly DiagnosticDescriptor AuthoredInverseDisagrees = new(
        id: AgwfCodes.AuthoredInverseDisagrees,
        title: "Authored compensation disagrees with derived inverse",
        messageFormat: "Step '{0}' in workflow '{1}' declares inverse action '{2}' for forward action '{3}', but their contracts disagree: {4}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A typed compensation must have the same subject, frame, and authority as its forward action, require exactly the forward guarantee, and ensure exactly the forward requirement.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    /// <summary>A compensation scope contains a leaf without a mechanically proven inverse.</summary>
    public static readonly DiagnosticDescriptor CompensationScopeNotDerivable = new(
        id: AgwfCodes.CompensationScopeNotDerivable,
        title: "Compensation scope is not mechanically derivable",
        messageFormat: "Workflow '{0}' cannot derive rollback scope '{1}': step '{2}' is not compensable ({3}). Give every rollback-reachable forward occurrence a closed, proven inverse action.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Compensability is derived and propagates: a rollback scope is valid only when every forward leaf that can complete before failure has one statically closed, contract-correct inverse action.",
        customTags: WellKnownDiagnosticTags.NotConfigurable);
}
