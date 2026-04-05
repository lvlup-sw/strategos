// -----------------------------------------------------------------------
// <copyright file="StateApplicationHelper.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Shared helper for emitting state application logic in saga handlers.
/// </summary>
/// <remarks>
/// <para>
/// Centralizes the mode-dependent state mutation pattern so that all emitters
/// produce consistent code for both <see cref="PersistenceMode.SagaDocument"/>
/// and <see cref="PersistenceMode.EventSourced"/> modes.
/// </para>
/// <para>
/// <b>SagaDocument mode:</b>
/// <code>
/// State = {Reducer}.Reduce(State, evt.UpdatedState);
/// </code>
/// </para>
/// <para>
/// <b>EventSourced mode:</b>
/// <code>
/// session.Events.Append(WorkflowId, evt);
/// State = State.ApplyEvent(evt);
/// </code>
/// </para>
/// </remarks>
internal static class StateApplicationHelper
{
    /// <summary>
    /// Emits state application code appropriate for the workflow's persistence mode.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="model">The workflow model (determines mode and reducer type).</param>
    /// <param name="evtVarName">The variable name for the event parameter (e.g., "evt").</param>
    /// <param name="indent">The indentation prefix for each line.</param>
    public static void EmitStateApplication(
        StringBuilder sb,
        WorkflowModel model,
        string evtVarName = "evt",
        string indent = "        ")
    {
        if (string.IsNullOrEmpty(model.StateTypeName))
        {
            return;
        }

        if (model.IsEventSourced)
        {
            sb.AppendLine($"{indent}session.Events.Append(WorkflowId, {evtVarName});");
            sb.AppendLine($"{indent}State = State.ApplyEvent({evtVarName});");
        }
        else
        {
            sb.AppendLine($"{indent}State = {model.ReducerTypeName}.Reduce(State, {evtVarName}.UpdatedState);");
        }
    }

    /// <summary>
    /// Emits the <c>IDocumentSession session</c> parameter for handler methods
    /// when the workflow uses event-sourced persistence.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="indent">The indentation prefix.</param>
    /// <remarks>
    /// Emits the parameter with a trailing comma and newline. Emits nothing
    /// for <see cref="PersistenceMode.SagaDocument"/> mode.
    /// </remarks>
    public static void EmitSessionParameter(
        StringBuilder sb,
        WorkflowModel model,
        string indent = "        ")
    {
        if (model.IsEventSourced)
        {
            sb.AppendLine($"{indent}IDocumentSession session,");
        }
    }

    /// <summary>
    /// Emits the null guard for the <c>session</c> parameter when event-sourced.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="indent">The indentation prefix.</param>
    public static void EmitSessionGuard(
        StringBuilder sb,
        WorkflowModel model,
        string indent = "        ")
    {
        if (model.IsEventSourced)
        {
            sb.AppendLine($"{indent}ArgumentNullException.ThrowIfNull(session, nameof(session));");
        }
    }
}
