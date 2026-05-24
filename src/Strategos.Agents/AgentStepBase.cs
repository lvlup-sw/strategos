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

    /// <summary>
    /// Test-only accessor for the underlying configuration. Internal-visible so
    /// <c>Strategos.Agents.Tests</c> can inspect builder-applied configuration
    /// without reaching into private fields via reflection (white-box test smell).
    /// </summary>
    internal AgentStepConfiguration<TState, TResult> Configuration => _configuration;

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
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);

        var messages = BuildMessages(state);

        // DR-1/DR-2: when a streaming observer is configured, drive the streaming chat
        // path. Streaming is purely an observability layer — it funnels into the SAME
        // terminal checks (FinalizeAsync) as the buffered path and does NOT change the
        // step's typed return shape.
        if (_configuration.StreamingHandler is not null)
        {
            var streamedResponse = await DriveStreamingAsync(
                    messages,
                    _configuration.StreamingHandler,
                    context,
                    cancellationToken)
                .ConfigureAwait(false);
            return await FinalizeAsync(streamedResponse, state, cancellationToken).ConfigureAwait(false);
        }

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

        return await FinalizeAsync(response, state, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Terminal contract block shared by the buffered and streaming paths (DR-1). Runs the
    /// DR-8 tool-loop-cap check, the DR-10 empty-payload check (AGAG006), the AGAG002
    /// structured-output check, and finally the apply-result hook — exactly once.
    /// </summary>
    private async Task<StepResult<TState>> FinalizeAsync(
        ChatResponse<TResult> response,
        TState state,
        CancellationToken cancellationToken)
    {
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
    /// Drives the streaming chat path (DR-1/DR-3/DR-4). Each update with non-empty text is
    /// forwarded to the handler in arrival order; after the stream completes the full
    /// accumulated text is delivered once via <c>OnResponseCompletedAsync</c>. The accumulated
    /// updates are then materialized into a <see cref="ChatResponse{TResult}"/> the SAME way the
    /// buffered typed extension does, so the shared <see cref="FinalizeAsync"/> contract applies
    /// identically (FinishReason / TryGetResult / empty-payload).
    /// </summary>
    /// <remarks>
    /// INV-1: tokens are a non-durable side-channel — this path references neither a progress
    /// event store nor any durable streaming-token record. The only durable artifact is the
    /// terminal StepResult produced by <see cref="FinalizeAsync"/>.
    /// </remarks>
    private async Task<ChatResponse<TResult>> DriveStreamingAsync(
        IList<ChatMessage> messages,
        IStreamingHandler handler,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var updates = new List<ChatResponseUpdate>();

        // Enumerate the streaming response. OperationCanceledException is NOT a domain failure
        // (DR-11) and must propagate unwrapped; a handler fault is wrapped as AGAG009 (DR-4).
        var enumerator = _chatClient
            .GetStreamingResponseAsync(messages, _configuration.ChatOptions, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                // MoveNext faults (e.g. cancellation, transport errors) surface here; we do
                // NOT wrap them as streaming-handler failures.
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                var update = enumerator.Current;
                updates.Add(update);

                if (!string.IsNullOrEmpty(update.Text))
                {
                    try
                    {
                        await handler
                            .OnTokenReceivedAsync(update.Text, context.WorkflowId, context.StepName, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation flowing through the handler is still cancellation — unwrapped.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new AgentStreamingException(
                            "Streaming token handler failed mid-stream.", ex);
                    }
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        // Materialize the accumulated updates into a typed response, mirroring the buffered
        // GetResponseAsync<TResult> path (which wraps a ChatResponse via the AIJsonUtilities
        // default serializer options).
        var aggregate = updates.ToChatResponse();
        var fullText = aggregate.Text;

        try
        {
            await handler
                .OnResponseCompletedAsync(fullText, context.WorkflowId, context.StepName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AgentStreamingException(
                "Streaming completion handler failed.", ex);
        }

        return new ChatResponse<TResult>(aggregate, AIJsonUtilities.DefaultOptions);
    }

    /// <summary>
    /// Builds the message sequence sent to the chat client for a given state.
    /// </summary>
    /// <param name="state">The current workflow state.</param>
    /// <returns>The ordered system + user messages.</returns>
    internal IList<ChatMessage> BuildMessages(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new List<ChatMessage>
        {
            new(ChatRole.System, _configuration.SystemPrompt(state)),
            new(ChatRole.User, _configuration.UserPrompt(state)),
        };
    }
}
