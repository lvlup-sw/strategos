// =============================================================================
// <copyright file="IContextAwareStep.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Models;

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Opt-in contract a workflow step implements to receive runtime
/// <see cref="AssembledContext"/> assembled from its <c>.WithContext(...)</c>
/// declaration (DR-6).
/// </summary>
/// <remarks>
/// <para>
/// When a step declares <c>.WithContext(...)</c>, the source generator emits a
/// <c>{Step}ContextAssembler</c> and wires it into the step's worker handler.
/// Before the handler calls the step's <c>ExecuteAsync</c>, it assembles the
/// declared context (state values, ontology retrieval, literals) and — if the
/// step implements this interface — hands the result to
/// <see cref="ReceiveContext"/>.
/// </para>
/// <para>
/// This is intentionally an opt-in side channel rather than a change to
/// <see cref="IWorkflowStep{TState}.ExecuteAsync"/>: a step that does not need
/// assembled context (or that ignores it) keeps the unchanged execution
/// signature, and a step that wants the context implements this one method.
/// The handler always assembles the context for a context-declaring step (so
/// ontology retrieval still runs and is observable), and only the delivery to
/// the step is gated on this interface.
/// </para>
/// </remarks>
public interface IContextAwareStep
{
    /// <summary>
    /// Receives the context assembled for this step's current execution. Invoked
    /// by the generated worker handler immediately before the step's
    /// <c>ExecuteAsync</c>.
    /// </summary>
    /// <param name="context">
    /// The assembled context (state, retrieval, and literal segments) for this
    /// execution. Never <see langword="null"/>; an empty context is
    /// <see cref="AssembledContext.Empty"/>.
    /// </param>
    void ReceiveContext(AssembledContext context);
}
