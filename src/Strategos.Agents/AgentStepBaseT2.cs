// =============================================================================
// <copyright file="AgentStepBaseT2.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Configuration;
using Strategos.Agents.Exceptions;
using Strategos.Steps;

namespace Strategos.Agents;

/// <summary>
/// Sealed orchestrator for LLM-powered workflow steps with typed structured results (DR-1).
/// Constructed via <c>AgentStepBuilder&lt;TState, TResult&gt;</c>; never subclassed.
/// </summary>
/// <typeparam name="TState">Workflow state type.</typeparam>
/// <typeparam name="TResult">Typed structured result produced by the agent's chat client.</typeparam>
/// <remarks>
/// This file lives alongside the legacy <c>AgentStepBase&lt;TState&gt;</c> (single-arity, abstract)
/// during the migration window. T-021 deletes the legacy type and renames this file to
/// <c>AgentStepBase.cs</c>.
/// </remarks>
public sealed class AgentStepBase<TState, TResult> : IAgentStep<TState, TResult>
    where TState : class, IWorkflowState
{
    /// <summary>Default upper bound on tool-call iterations per request (DR-8).</summary>
    public const int DefaultMaxToolIterations = 8;

    private readonly IChatClient _chatClient;
    private readonly AgentStepConfiguration<TState, TResult> _configuration;

    internal AgentStepBase(
        IChatClient chatClient,
        AgentStepConfiguration<TState, TResult> configuration)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc/>
    public async Task<StepResult<TState>> ExecuteAsync(
        TState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var messages = BuildMessages(state);

        var response = await _chatClient.GetResponseAsync<TResult>(
                messages,
                _configuration.ChatOptions,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (response is null)
        {
            // T-010 lands AGAG006 here.
            throw new NotImplementedException("Null/empty response handling lands in T-010.");
        }

        if (!response.TryGetResult(out var typedResult) || typedResult is null)
        {
            // DR-3 / DR-10 no-silent-fallback: throw with AGAG002 and the raw payload.
            // The exception ctor handles ≤4 KB truncation; apply-result hook is NOT invoked.
            throw new AgentStructuredOutputException(response.Text);
        }

        return await _configuration.ApplyResult(state, typedResult, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the message sequence sent to the chat client for a given state.
    /// </summary>
    /// <param name="state">The current workflow state.</param>
    /// <returns>The ordered system + user messages.</returns>
    internal IList<ChatMessage> BuildMessages(TState state)
    {
        return new List<ChatMessage>
        {
            new(ChatRole.System, _configuration.SystemPrompt(state)),
            new(ChatRole.User, _configuration.UserPrompt(state)),
        };
    }
}
