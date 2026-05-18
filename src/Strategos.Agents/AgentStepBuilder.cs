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
    private readonly List<AIFunction> _tools = new();
    private IMcpToolSource? _mcpToolSource;
    private ChatOptions? _chatOptions;
    private bool _chatOptionsSet;
    private int? _maxToolIterations;

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
    /// Register an <see cref="AIFunction"/> tool with the agent (DR-4). Multiple calls accumulate;
    /// duplicate-name collision detection is deferred to <see cref="Build"/> so that fluent
    /// reuse (e.g. conditional/builder-pipeline reconfiguration) remains ergonomic.
    /// </summary>
    /// <param name="tool">The AIFunction to register.</param>
    public AgentStepBuilder<TState, TResult> WithTool(AIFunction tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools.Add(tool);
        return this;
    }

    /// <summary>
    /// Supply explicit <see cref="ChatOptions"/> for the agent (DR-2). May only be called once;
    /// a second call throws <see cref="InvalidOperationException"/> so that downstream defaults
    /// (tool wiring, response-format injection) are not silently overwritten by reconfiguration.
    /// </summary>
    /// <param name="options">The ChatOptions to apply.</param>
    public AgentStepBuilder<TState, TResult> WithChatOptions(ChatOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_chatOptionsSet)
        {
            throw new InvalidOperationException("WithChatOptions has already been called on this builder.");
        }

        _chatOptions = options;
        _chatOptionsSet = true;
        return this;
    }

    /// <summary>
    /// Register an <see cref="IMcpToolSource"/> port (DR-5). The configured source is stored
    /// on the produced <see cref="AgentStepConfiguration{TState, TResult}"/> and resolved lazily
    /// at first execution by the runtime.
    /// </summary>
    /// <param name="source">The MCP tool source port.</param>
    public AgentStepBuilder<TState, TResult> WithMcpToolSource(IMcpToolSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _mcpToolSource = source;
        return this;
    }

    /// <summary>
    /// Override the tool-iteration bound (DR-8). Must be strictly positive; zero or negative values
    /// are rejected with <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="max">The maximum tool-call iterations to allow per agent invocation.</param>
    public AgentStepBuilder<TState, TResult> WithMaxToolIterations(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        _maxToolIterations = max;
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

        var firstDuplicate = _tools
            .GroupBy(t => t.Name)
            .FirstOrDefault(g => g.Count() > 1);
        if (firstDuplicate is not null)
        {
            throw new AgentDuplicateToolException(firstDuplicate.Key);
        }

        var configuration = new AgentStepConfiguration<TState, TResult>(
            SystemPrompt: _systemPrompt,
            UserPrompt: _userPrompt,
            ApplyResult: _applyResult,
            Tools: _tools.ToArray(),
            McpToolSource: _mcpToolSource,
            ChatOptions: _chatOptions,
            ChatClientConfigurator: null,
            MaxToolIterations: _maxToolIterations);

        return new AgentStepBase<TState, TResult>(chatClient, configuration);
    }
}
