// =============================================================================
// <copyright file="AgentStepBase.cs" company="Levelup Software">
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

        ChatResponse<TResult>? response;
        try
        {
            response = await _chatClient.GetResponseAsync<TResult>(
                    messages,
                    _configuration.ChatOptions,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a domain failure (DR-10) — propagate unwrapped.
            throw;
        }
        catch (ArgumentNullException)
        {
            // MEAI's typed extension throws ArgumentNullException when the underlying
            // chat client returns a null ChatResponse. That's the DR-10 boundary we
            // own: re-classify as AGAG006 with no partial state mutation.
            throw new AgentChatResponseException(
                "Chat client returned a null response.");
        }

        if (response is null)
        {
            // Defensive: should not happen via MEAI typed extension (it throws ANE),
            // but if a direct caller produces null we surface the same DR-10 contract.
            throw new AgentChatResponseException(
                "Chat client returned a null response.");
        }

        // DR-8: detect the FunctionInvokingChatClient iteration cap. MEAI 10.5 does not
        // throw when it reaches MaximumIterationsPerRequest — it logs
        // "Reached maximum iteration count of {N}. Stopping function invocation loop."
        // and returns the latest inner response as-is. That response retains
        // FinishReason == ToolCalls (the model still wanted to call tools when the
        // middleware bailed). We surface that condition as AGAG005 with the captured
        // PartialTrace. This check MUST precede the AGAG006 empty-payload check,
        // because the capped response will also have an empty Text / no TryGetResult.
        if (response.FinishReason == ChatFinishReason.ToolCalls)
        {
            var maxIterations = _configuration.MaxToolIterations ?? DefaultMaxToolIterations;
            // Snapshot the messages into an immutable IReadOnlyList. We do NOT alias
            // response.Messages directly — callers must observe a stable trace even
            // if the response is later mutated by another consumer.
            var partialTrace = response.Messages.ToArray();
            throw new AgentToolLoopException(maxIterations, partialTrace);
        }

        var hasResult = response.TryGetResult(out var typedResult) && typedResult is not null;
        if (!hasResult && string.IsNullOrEmpty(response.Text))
        {
            // DR-10: empty payload (no Result, no Text) — distinct from a malformed
            // structured-output failure (AGAG002). State is untouched (apply-result
            // never runs); the caller's TState instance remains reference-equal.
            throw new AgentChatResponseException(
                "Chat client returned an empty response (no text and no structured result).");
        }

        if (!hasResult)
        {
            // DR-3 / DR-10 no-silent-fallback: throw with AGAG002 and the raw payload.
            // The exception ctor handles ≤4 KB truncation; apply-result hook is NOT invoked.
            throw new AgentStructuredOutputException(response.Text);
        }

        return await _configuration.ApplyResult(state, typedResult!, cancellationToken).ConfigureAwait(false);
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
