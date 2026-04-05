// -----------------------------------------------------------------------
// <copyright file="WorkflowAttribute.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Attributes;

/// <summary>
/// Marks a class or struct containing a workflow definition for source generation.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a class or struct that contains a workflow definition
/// created using the fluent DSL (Workflow&lt;TState&gt;.Create()). The source generator
/// will produce a Phase enum and other artifacts based on the workflow structure.
/// </para>
/// <para>
/// Example usage:
/// </para>
/// <code>
/// [Workflow("process-order")]
/// public static partial class ProcessOrderWorkflow
/// {
///     public static WorkflowDefinition&lt;OrderState&gt; Definition => Workflow&lt;OrderState&gt;
///         .Create("process-order")
///         .StartWith&lt;ValidateOrder&gt;()
///         .Then&lt;ProcessPayment&gt;()
///         .Finally&lt;SendConfirmation&gt;();
/// }
/// </code>
/// <para>
/// For versioned workflows (breaking schema changes):
/// </para>
/// <code>
/// [Workflow("process-order", version: 2)]
/// public static partial class ProcessOrderWorkflowV2
/// {
///     // V2 workflow definition...
/// }
/// </code>
/// </remarks>
/// <param name="name">The workflow name used for generated type naming (e.g., "process-order" becomes ProcessOrderPhase).</param>
/// <param name="version">The workflow schema version. Increment when making breaking changes. Default is 1.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class WorkflowAttribute(string name, int version = 1) : Attribute
{
    /// <summary>
    /// Gets the workflow name.
    /// </summary>
    /// <remarks>
    /// The name is typically in kebab-case (e.g., "process-order") and will be
    /// converted to PascalCase for generated type names (e.g., ProcessOrderPhase).
    /// </remarks>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the workflow schema version.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Increment the version when making breaking changes to workflow structure that would
    /// affect in-flight workflows. The source generator produces versioned saga classes:
    /// </para>
    /// <list type="bullet">
    /// <item>Version 1: <c>{PascalName}Saga</c> (e.g., ProcessOrderSaga)</item>
    /// <item>Version 2+: <c>{PascalName}SagaV{N}</c> (e.g., ProcessOrderSagaV2)</item>
    /// </list>
    /// <para>
    /// This enables side-by-side execution of different workflow versions during deployment,
    /// preventing breaking changes to in-flight workflows.
    /// </para>
    /// </remarks>
    public int Version { get; } = version;

    /// <summary>
    /// Gets or sets the persistence mode for generated saga handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <see cref="PersistenceMode.SagaDocument"/> (direct state mutation via reducers).
    /// Set to <see cref="PersistenceMode.EventSourced"/> to generate handlers that append events
    /// to the Marten event stream and apply them locally for saga routing.
    /// </para>
    /// <para>
    /// When using <see cref="PersistenceMode.EventSourced"/>, the state type must implement
    /// <c>IEventSourcedState&lt;TState&gt;</c> which provides the
    /// <c>ApplyEvent</c> method used for local state application.
    /// </para>
    /// </remarks>
    public PersistenceMode Persistence { get; set; } = PersistenceMode.SagaDocument;
}
