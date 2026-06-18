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
}
