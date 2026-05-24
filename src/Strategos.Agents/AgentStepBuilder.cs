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
    private readonly List<IToolSource> _toolSources = new();
    private ChatOptions? _chatOptions;
    private bool _chatOptionsSet;
    private int? _maxToolIterations;
    private Action<ChatClientBuilder>? _chatClientConfigurator;
    private IStreamingHandler? _streamingHandler;
    private bool _streamingHandlerSet;

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
    /// Register an <see cref="IToolSource"/> port (DR-9). Multiple calls accumulate in
    /// registration order; each configured source is stored on the produced
    /// <see cref="AgentStepConfiguration{TState, TResult}"/> and resolved lazily at first
    /// execution by the runtime.
    /// </summary>
    /// <param name="source">The tool source port (MCP, in-process reflection, etc.).</param>
    public AgentStepBuilder<TState, TResult> WithToolSource(IToolSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _toolSources.Add(source);
        return this;
    }

    /// <summary>
    /// Register an <see cref="IStreamingHandler"/> streaming observer (DR-2). May only be called
    /// once; a second call throws <see cref="InvalidOperationException"/> (mirroring
    /// <see cref="WithChatOptions"/>) so that an observer is never silently replaced. When set,
    /// the orchestrator drives the streaming chat path and forwards tokens to the handler as a
    /// non-durable side-channel; the terminal typed result contract is unchanged.
    /// </summary>
    /// <param name="handler">The streaming handler to receive token and completion callbacks.</param>
    public AgentStepBuilder<TState, TResult> WithStreaming(IStreamingHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (_streamingHandlerSet)
        {
            throw new InvalidOperationException("WithStreaming has already been called on this builder.");
        }

        _streamingHandler = handler;
        _streamingHandlerSet = true;
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
    /// Host-composition escape hatch (DR-6). The supplied configurator runs FIRST when
    /// the IChatClient pipeline is composed in <see cref="Build"/> — its added middleware
    /// (typically <c>UseLogging</c>, <c>UseOpenTelemetry</c>, <c>UseDistributedCache</c>)
    /// wraps the Strategos-internal stages (<c>UseStrategosFunctions</c> →
    /// <c>UseFunctionInvocation</c>), which Strategos always applies in fixed order.
    /// </summary>
    /// <param name="configurator">The host's <see cref="ChatClientBuilder"/> configurator.</param>
    public AgentStepBuilder<TState, TResult> ConfigureChatClient(Action<ChatClientBuilder> configurator)
    {
        _chatClientConfigurator = configurator ?? throw new ArgumentNullException(nameof(configurator));
        return this;
    }

    /// <summary>
    /// Construct the configured <see cref="IAgentStep{TState, TResult}"/>. Throws
    /// <see cref="AgentBuilderValidationException"/> (AGAG001) if any required hook is missing.
    /// </summary>
    /// <param name="chatClient">
    /// The MEAI chat client placed at the bottom of the composed pipeline. The builder
    /// wraps it with <c>UseStrategosFunctions</c> (tool-list injection) and
    /// <c>UseFunctionInvocation</c> (bounded tool-call loop) in that fixed order, then
    /// applies the optional host configurator outermost. Tool names registered via
    /// <see cref="WithTool"/> must be unique at this point — duplicate names cause
    /// <see cref="AgentDuplicateToolException"/> (AGAG003) before the pipeline is built.
    /// </param>
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

        var toolList = _tools.ToArray();
        var toolSources = _toolSources.ToArray();
        var configuration = new AgentStepConfiguration<TState, TResult>(
            SystemPrompt: _systemPrompt,
            UserPrompt: _userPrompt,
            ApplyResult: _applyResult,
            Tools: toolList,
            ToolSources: toolSources,
            ChatOptions: _chatOptions,
            MaxToolIterations: _maxToolIterations,
            StreamingHandler: _streamingHandler);

        var composedChatClient = ComposeChatClient(chatClient, toolList, toolSources);
        return new AgentStepBase<TState, TResult>(composedChatClient, configuration);
    }

    /// <summary>
    /// Composes the IChatClient pipeline in fixed order (DR-6):
    /// host configurator → <c>UseStrategosFunctions</c> → <c>UseFunctionInvocation</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="ChatClientBuilder"/> directly rather than a DI container so
    /// that the builder remains dependency-injection-agnostic (DR-6 escape hatch).
    /// The host supplies the outermost middleware via <see cref="ConfigureChatClient"/>
    /// and the <paramref name="innerClient"/> at the bottom via <see cref="Build"/>;
    /// everything in between is Strategos-owned and applied here in a fixed sequence.
    /// </para>
    /// <para>
    /// Pipeline order (outermost → innermost, matching call order on the builder):
    /// <list type="number">
    /// <item><description>Host configurator (logging, OTel, distributed cache, etc.).</description></item>
    /// <item><description><c>UseStrategosFunctions</c> — injects the accumulated tool list and
    /// resolves tool sources into <c>ChatOptions.Tools</c> on each request.</description></item>
    /// <item><description><c>UseFunctionInvocation</c> — runs the tool-call loop bounded by
    /// <c>MaxToolIterations</c> (default: <see cref="AgentStepBase{TState, TResult}.DefaultMaxToolIterations"/>).</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private IChatClient ComposeChatClient(
        IChatClient innerClient,
        IReadOnlyList<AIFunction> tools,
        IReadOnlyList<IToolSource> toolSources)
    {
        var maxIterations = _maxToolIterations ?? AgentStepBase<TState, TResult>.DefaultMaxToolIterations;
        var builder = new ChatClientBuilder(innerClient);

        // 1. Host configurator runs FIRST so its middleware ends up OUTERMOST in the chain.
        _chatClientConfigurator?.Invoke(builder);

        // 2. Surface the Strategos-registered AIFunctions as an inspectable chain step.
        //    Registered tool sources (if any) are resolved lazily on first request by the
        //    middleware itself — Build() stays sync (T-016 invariant).
        builder.UseStrategosFunctions(tools, toolSources);

        // 3. Automatic function-call invocation, bounded by _maxToolIterations (DR-8).
        builder.UseFunctionInvocation(
            loggerFactory: null,
            configure: client => client.MaximumIterationsPerRequest = maxIterations);

        return builder.Build();
    }
}
