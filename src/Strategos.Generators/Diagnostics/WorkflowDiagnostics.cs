// -----------------------------------------------------------------------
// <copyright file="WorkflowDiagnostics.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Diagnostics;

/// <summary>
/// Defines diagnostic descriptors for the workflow source generator.
/// </summary>
internal static class WorkflowDiagnostics
{
    /// <summary>
    /// Diagnostic category for all workflow generator diagnostics.
    /// </summary>
    public const string Category = "Strategos";

    /// <summary>
    /// AGWF001: Empty workflow name.
    /// </summary>
    /// <remarks>
    /// Reported when the [Workflow] attribute is applied with an empty or whitespace-only name.
    /// </remarks>
    public static readonly DiagnosticDescriptor EmptyWorkflowName = new(
        id: "AGWF001",
        title: "Empty workflow name",
        messageFormat: "Workflow name cannot be empty or whitespace",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The workflow name specified in the [Workflow] attribute must not be empty or consist only of whitespace characters.");

    /// <summary>
    /// AGWF002: No workflow steps found.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow definition has no steps defined in its DSL chain.
    /// </remarks>
    public static readonly DiagnosticDescriptor NoStepsFound = new(
        id: "AGWF002",
        title: "No workflow steps found",
        messageFormat: "Could not find any steps in workflow '{0}'. Ensure the workflow uses StartWith<T>(), Then<T>(), and Finally<T>() methods.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The workflow definition should contain at least one step defined using the fluent DSL (StartWith<T>(), Then<T>(), Finally<T>()).");

    /// <summary>
    /// AGWF003: Duplicate step name.
    /// </summary>
    /// <remarks>
    /// Reported when the same step type appears multiple times in a workflow.
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicateStepName = new(
        id: "AGWF003",
        title: "Duplicate step name",
        messageFormat: "Step '{0}' appears multiple times in workflow '{1}'. Each step type should be unique.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each step type in a workflow should be unique to prevent ambiguous phase transitions.");

    /// <summary>
    /// AGWF004: Invalid namespace.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow is declared in an invalid namespace (e.g., global namespace).
    /// </remarks>
    public static readonly DiagnosticDescriptor InvalidNamespace = new(
        id: "AGWF004",
        title: "Invalid namespace",
        messageFormat: "Workflow '{0}' must be declared in a namespace. Global namespace is not supported.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Workflows must be declared in a named namespace to ensure proper code generation.");

    /// <summary>
    /// AGWF009: Missing StartWith.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow definition does not start with StartWith&lt;T&gt;().
    /// </remarks>
    public static readonly DiagnosticDescriptor MissingStartWith = new(
        id: "AGWF009",
        title: "Missing StartWith",
        messageFormat: "Workflow '{0}' must begin with StartWith<T>(). Found '{1}' instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every workflow definition must begin with StartWith<T>() to define the entry step. Using Then<T>() or other methods first is not supported.");

    /// <summary>
    /// AGWF012: Fork without Join.
    /// </summary>
    /// <remarks>
    /// Reported when a Fork construct is not followed by a Join step.
    /// </remarks>
    public static readonly DiagnosticDescriptor ForkWithoutJoin = new(
        id: "AGWF012",
        title: "Fork without Join",
        messageFormat: "Workflow '{0}' has a Fork that is not followed by Join. Every Fork must be closed with a Join<T>() call.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every Fork construct in a workflow must be followed by a Join<T>() call to merge the parallel execution paths.");

    /// <summary>
    /// AGWF010: Missing Finally.
    /// </summary>
    /// <remarks>
    /// Reported as a warning when a workflow does not end with Finally&lt;T&gt;().
    /// This is a warning rather than an error because some patterns may intentionally
    /// short-circuit or use Complete() in branches.
    /// </remarks>
    public static readonly DiagnosticDescriptor MissingFinally = new(
        id: "AGWF010",
        title: "Missing Finally",
        messageFormat: "Workflow '{0}' does not end with Finally<T>(). Consider adding a Finally step to mark workflow completion.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Workflows should typically end with Finally<T>() to mark completion. This is a warning because some patterns may intentionally short-circuit using Complete() in branches.");

    /// <summary>
    /// AGWF014: Loop without body.
    /// </summary>
    /// <remarks>
    /// Reported when a RepeatUntil loop has an empty body (no steps).
    /// </remarks>
    public static readonly DiagnosticDescriptor LoopWithoutBody = new(
        id: "AGWF014",
        title: "Loop without body",
        messageFormat: "Loop '{0}' in workflow '{1}' has no steps in its body. A loop must contain at least one step.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every RepeatUntil loop must contain at least one step in its body. An empty loop body serves no purpose and is likely an error.");

    /// <summary>
    /// AGWF015: Invalid persistence mode.
    /// </summary>
    /// <remarks>
    /// Reported when the [Workflow] attribute specifies an unrecognized Persistence value.
    /// </remarks>
    public static readonly DiagnosticDescriptor InvalidPersistenceMode = new(
        id: "AGWF015",
        title: "Invalid persistence mode",
        messageFormat: "Workflow '{0}' specifies an unrecognized Persistence value ({1}). Valid values are SagaDocument (0) and EventSourced (1).",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Persistence property of the [Workflow] attribute must be a valid PersistenceMode value.");

    /// <summary>
    /// AGWF016: Event-sourced workflow requires state type.
    /// </summary>
    /// <remarks>
    /// Reported when a workflow uses PersistenceMode.EventSourced but does not declare a state type.
    /// </remarks>
    public static readonly DiagnosticDescriptor EventSourcedRequiresState = new(
        id: "AGWF016",
        title: "Event-sourced workflow requires state type",
        messageFormat: "Workflow '{0}' uses PersistenceMode.EventSourced but no state type was found. Event-sourced workflows require a state type that implements IEventSourcedState<TState> with an ApplyEvent method.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Event-sourced workflows require a state type to generate handlers that call State.ApplyEvent(evt). Ensure the workflow uses Workflow<TState>.Create() with a state type that implements IEventSourcedState<TState>.");
}
