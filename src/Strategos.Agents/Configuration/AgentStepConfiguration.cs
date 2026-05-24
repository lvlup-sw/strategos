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

    /// <summary>Registered tool sources (DR-9); each resolved lazily at first execution. Never null; may be empty.</summary>
    public IReadOnlyList<IToolSource> ToolSources { get; }

    /// <summary>Optional explicit ChatOptions. The builder still applies defaults if null.</summary>
    public ChatOptions? ChatOptions { get; }

    /// <summary>Optional host-side ChatClientBuilder configurator (logging, OTel, distributed cache, etc.).</summary>
    public Action<ChatClientBuilder>? ChatClientConfigurator { get; }

    /// <summary>Optional override for the tool-iteration bound (default 8; see AgentStepBase.DefaultMaxToolIterations).</summary>
    public int? MaxToolIterations { get; }

    /// <summary>
    /// Optional streaming observer (DR-2). When present, the orchestrator drives the
    /// streaming chat path and forwards tokens to this handler as a non-durable
    /// side-channel; the durable artifact remains the terminal StepResult. Null means
    /// the buffered (non-streaming) path is used unchanged.
    /// </summary>
    public IStreamingHandler? StreamingHandler { get; }

    internal AgentStepConfiguration(
        Func<TState, string> SystemPrompt,
        Func<TState, string> UserPrompt,
        Func<TState, TResult, CancellationToken, Task<StepResult<TState>>> ApplyResult,
        IReadOnlyList<AIFunction> Tools,
        IReadOnlyList<IToolSource> ToolSources,
        ChatOptions? ChatOptions,
        Action<ChatClientBuilder>? ChatClientConfigurator,
        int? MaxToolIterations,
        IStreamingHandler? StreamingHandler = null)
    {
        ArgumentNullException.ThrowIfNull(SystemPrompt);
        ArgumentNullException.ThrowIfNull(UserPrompt);
        ArgumentNullException.ThrowIfNull(ApplyResult);
        ArgumentNullException.ThrowIfNull(Tools);
        if (Tools.Any(static tool => tool is null))
        {
            throw new ArgumentException("Tools cannot contain null entries.", nameof(Tools));
        }

        ArgumentNullException.ThrowIfNull(ToolSources);
        if (ToolSources.Any(static source => source is null))
        {
            throw new ArgumentException("ToolSources cannot contain null entries.", nameof(ToolSources));
        }

        // MaxToolIterations is optional (null = use the default), but when specified it
        // must be a positive bound. The builder's WithMaxToolIterations guards this too;
        // enforcing it here keeps the configuration self-validating on any construction path.
        if (MaxToolIterations is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxToolIterations),
                MaxToolIterations,
                "MaxToolIterations must be greater than zero when specified.");
        }

        this.SystemPrompt = SystemPrompt;
        this.UserPrompt = UserPrompt;
        this.ApplyResult = ApplyResult;
        this.Tools = Tools;
        this.ToolSources = ToolSources;
        this.ChatOptions = ChatOptions;
        this.ChatClientConfigurator = ChatClientConfigurator;
        this.MaxToolIterations = MaxToolIterations;
        this.StreamingHandler = StreamingHandler;
    }
}
