// =============================================================================
// <copyright file="AgentStepConfiguration.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Abstractions;
using Strategos.Agents.Abstractions;
using Strategos.Steps;

namespace Strategos.Agents.Configuration;

/// <summary>
/// Immutable configuration consumed by <c>AgentStepBase&lt;TState, TResult&gt;</c>.
/// Built exclusively by <c>AgentStepBuilder&lt;TState, TResult&gt;</c>; not intended for direct construction.
/// </summary>
/// <typeparam name="TState">Workflow state type.</typeparam>
/// <typeparam name="TResult">Typed structured result produced by the agent.</typeparam>
public sealed record AgentStepConfiguration<TState, TResult>
    where TState : class, IWorkflowState
{
    /// <summary>Required hook: produce the system prompt from current state.</summary>
    public Func<TState, string> SystemPrompt { get; }

    /// <summary>Required hook: produce the user prompt from current state.</summary>
    public Func<TState, string> UserPrompt { get; }

    /// <summary>Required hook: apply the typed structured result and produce the next state.</summary>
    public Func<TState, TResult, CancellationToken, Task<StepResult<TState>>> ApplyResult { get; }

    /// <summary>AIFunction tools registered via <c>AgentStepBuilder.WithTool</c>.</summary>
    public IReadOnlyList<AIFunction> Tools { get; }

    /// <summary>Optional MCP tool source (lazy resolution at first execution).</summary>
    public IMcpToolSource? McpToolSource { get; }

    /// <summary>Optional explicit ChatOptions. The builder still applies defaults if null.</summary>
    public ChatOptions? ChatOptions { get; }

    /// <summary>Optional host-side ChatClientBuilder configurator (logging, OTel, distributed cache, etc.).</summary>
    public Action<ChatClientBuilder>? ChatClientConfigurator { get; }

    /// <summary>Optional override for the tool-iteration bound (default 8; see AgentStepBase.DefaultMaxToolIterations).</summary>
    public int? MaxToolIterations { get; }

    internal AgentStepConfiguration(
        Func<TState, string> SystemPrompt,
        Func<TState, string> UserPrompt,
        Func<TState, TResult, CancellationToken, Task<StepResult<TState>>> ApplyResult,
        IReadOnlyList<AIFunction> Tools,
        IMcpToolSource? McpToolSource,
        ChatOptions? ChatOptions,
        Action<ChatClientBuilder>? ChatClientConfigurator,
        int? MaxToolIterations)
    {
        ArgumentNullException.ThrowIfNull(SystemPrompt);
        ArgumentNullException.ThrowIfNull(UserPrompt);
        ArgumentNullException.ThrowIfNull(ApplyResult);
        ArgumentNullException.ThrowIfNull(Tools);

        this.SystemPrompt = SystemPrompt;
        this.UserPrompt = UserPrompt;
        this.ApplyResult = ApplyResult;
        this.Tools = Tools;
        this.McpToolSource = McpToolSource;
        this.ChatOptions = ChatOptions;
        this.ChatClientConfigurator = ChatClientConfigurator;
        this.MaxToolIterations = MaxToolIterations;
    }
}
