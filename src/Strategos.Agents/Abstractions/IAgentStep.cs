// =============================================================================
// <copyright file="IAgentStep.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Generic refinement of <see cref="IWorkflowStep{TState}"/> for LLM-powered steps
/// that produce a typed structured result. The two-arity contract subsumes the
/// previous <c>GetOutputSchemaType()</c> sentinel by making the result type part
/// of the interface signature.
/// </summary>
/// <typeparam name="TState">Workflow state type.</typeparam>
/// <typeparam name="TResult">Typed structured result returned by the agent's chat client (use <see cref="string"/> for unstructured text).</typeparam>
public interface IAgentStep<TState, TResult> : IWorkflowStep<TState>
    where TState : class, IWorkflowState
{
}
