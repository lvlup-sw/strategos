// =============================================================================
// <copyright file="AgentStepBuilder.cs" company="Levelup Software">
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
/// Fluent builder that configures and produces an <see cref="IAgentStep{TState, TResult}"/>.
/// The sealed builder is the only sanctioned construction path for <see cref="AgentStepBase{TState, TResult}"/>.
/// </summary>
/// <typeparam name="TState">Workflow state type.</typeparam>
/// <typeparam name="TResult">Typed structured result produced by the configured agent.</typeparam>
public sealed class AgentStepBuilder<TState, TResult>
    where TState : class, IWorkflowState
{
    private Func<TState, string>? _systemPrompt;
    private Func<TState, string>? _userPrompt;
    private Func<TState, TResult, CancellationToken, Task<StepResult<TState>>>? _applyResult;

    /// <summary>Configure the system prompt hook (required).</summary>
    public AgentStepBuilder<TState, TResult> WithSystemPrompt(Func<TState, string> systemPrompt)
    {
        _systemPrompt = systemPrompt ?? throw new ArgumentNullException(nameof(systemPrompt));
        return this;
    }

    /// <summary>Configure the user prompt hook (required).</summary>
    public AgentStepBuilder<TState, TResult> WithUserPrompt(Func<TState, string> userPrompt)
    {
        _userPrompt = userPrompt ?? throw new ArgumentNullException(nameof(userPrompt));
        return this;
    }

    /// <summary>Configure the apply-result hook (required).</summary>
    public AgentStepBuilder<TState, TResult> WithApplyResult(
        Func<TState, TResult, CancellationToken, Task<StepResult<TState>>> applyResult)
    {
        _applyResult = applyResult ?? throw new ArgumentNullException(nameof(applyResult));
        return this;
    }

    /// <summary>
    /// Construct the configured <see cref="IAgentStep{TState, TResult}"/>. Throws
    /// <see cref="AgentBuilderValidationException"/> (AGAG001) if any required hook is missing.
    /// </summary>
    /// <param name="chatClient">The MEAI chat client to invoke.</param>
    /// <returns>The configured agent step.</returns>
    public IAgentStep<TState, TResult> Build(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        if (_systemPrompt is null)
        {
            throw new AgentBuilderValidationException("SystemPrompt");
        }

        if (_userPrompt is null)
        {
            throw new AgentBuilderValidationException("UserPrompt");
        }

        if (_applyResult is null)
        {
            throw new AgentBuilderValidationException("ApplyResult");
        }

        var configuration = new AgentStepConfiguration<TState, TResult>(
            SystemPrompt: _systemPrompt,
            UserPrompt: _userPrompt,
            ApplyResult: _applyResult,
            Tools: Array.Empty<AIFunction>(),
            McpToolSource: null,
            ChatOptions: null,
            ChatClientConfigurator: null,
            MaxToolIterations: null);

        return new AgentStepBase<TState, TResult>(chatClient, configuration);
    }
}
