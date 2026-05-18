// =============================================================================
// <copyright file="StrategosFunctionsChatClient.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents.Configuration;

/// <summary>
/// Thin delegating chat client that surfaces the accumulated <see cref="AIFunction"/>
/// tools (registered via <c>AgentStepBuilder.WithTool</c>) as a named, inspectable
/// chain step (DR-4, DR-6) AND injects them into the per-request
/// <see cref="ChatOptions.Tools"/> so a subsequent
/// <see cref="FunctionInvokingChatClient"/> (added via
/// <c>FunctionInvokingChatClientBuilderExtensions.UseFunctionInvocation</c>) can
/// look up requested tool names. Without the injection, the downstream invoker
/// observes <c>options.Tools == null</c>, reports <c>FunctionStatus.NotFound</c>,
/// and emits a synthetic tool-error message instead of running the tool.
/// </summary>
/// <remarks>
/// <para>
/// Marked <c>internal</c> — this type is an implementation detail of
/// <c>AgentStepBuilder.Build</c> and not part of the public surface. Tests in
/// <c>Strategos.Agents.Tests</c> see it through <c>InternalsVisibleTo</c>.
/// </para>
/// <para>
/// MCP source resolution (DR-5): when an <see cref="IMcpToolSource"/> is supplied,
/// the resolver is invoked lazily on the first per-request call to
/// <c>GetResponseAsync</c> / <c>GetStreamingResponseAsync</c>. The resolved
/// <see cref="AIFunction"/>s are cached for the lifetime of this middleware
/// instance and merged into <see cref="ChatOptions.Tools"/> as a third source
/// (after host-supplied and Strategos-registered tools). If resolution fails,
/// the cache stays empty and the next request retries; the failure is rethrown
/// wrapped as an <see cref="AgentMcpException"/> (AGAG004). Cancellation flows
/// through unwrapped.
/// </para>
/// <para>
/// Name-collision precedence (host &gt; Strategos &gt; MCP): the host's
/// per-request <see cref="ChatOptions.Tools"/> express the most specific intent
/// and win. Strategos in-process tools win over externally-discovered MCP tools
/// because the agent owns its own contract while MCP is a fallback skill source.
/// </para>
/// </remarks>
internal sealed class StrategosFunctionsChatClient : DelegatingChatClient
{
    private readonly IMcpToolSource? _mcpToolSource;
    private readonly SemaphoreSlim _resolveLock = new(1, 1);
    private IReadOnlyList<AIFunction>? _resolvedMcpTools;

    /// <summary>Initializes a new instance of the <see cref="StrategosFunctionsChatClient"/> class.</summary>
    /// <param name="innerClient">The wrapped inner chat client.</param>
    /// <param name="tools">The Strategos-registered <see cref="AIFunction"/> tools.</param>
    /// <param name="mcpToolSource">Optional MCP tool source (DR-5); resolved lazily on first request.</param>
    public StrategosFunctionsChatClient(
        IChatClient innerClient,
        IReadOnlyList<AIFunction> tools,
        IMcpToolSource? mcpToolSource = null)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(tools);
        Tools = tools;
        _mcpToolSource = mcpToolSource;
    }

    /// <summary>Gets the Strategos-registered AIFunction tools carried by this chain step.</summary>
    public IReadOnlyList<AIFunction> Tools { get; }

    /// <inheritdoc/>
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is null && serviceType.IsInstanceOfType(this))
        {
            return this;
        }

        return base.GetService(serviceType, serviceKey);
    }

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var merged = await MergeToolsAsync(options, cancellationToken).ConfigureAwait(false);
        return await base.GetResponseAsync(messages, merged, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var merged = await MergeToolsAsync(options, cancellationToken).ConfigureAwait(false);
        await foreach (var update in base.GetStreamingResponseAsync(messages, merged, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Produces a per-request <see cref="ChatOptions"/> that merges three tool
    /// sources into the caller-supplied <paramref name="options"/> without
    /// mutating the original: (1) host-supplied tools already on
    /// <see cref="ChatOptions.Tools"/>, (2) Strategos-registered <see cref="Tools"/>,
    /// and (3) MCP-discovered tools (resolved lazily on first call and cached).
    /// Merge order is [host, Strategos, MCP]; tools whose <see cref="AITool.Name"/>
    /// is already present are skipped (host wins, then Strategos, then MCP).
    /// </summary>
    private async Task<ChatOptions> MergeToolsAsync(ChatOptions? options, CancellationToken cancellationToken)
    {
        // Clone so we never mutate caller state — MEAI 10.5 exposes Clone() on ChatOptions.
        var clone = options?.Clone() ?? new ChatOptions();

        var mcpTools = await ResolveMcpToolsAsync(cancellationToken).ConfigureAwait(false);

        if (Tools.Count == 0 && mcpTools.Count == 0)
        {
            return clone;
        }

        clone.Tools ??= new List<AITool>();

        // Build the existing-name / existing-ref sets from whatever the caller supplied.
        var existingNames = new HashSet<string>(StringComparer.Ordinal);
        var existingRefs = new HashSet<AITool>(ReferenceEqualityComparer.Instance);
        foreach (var existing in clone.Tools)
        {
            existingRefs.Add(existing);
            if (!string.IsNullOrEmpty(existing.Name))
            {
                existingNames.Add(existing.Name);
            }
        }

        // Source 2: Strategos-registered tools (wins over MCP, loses to host).
        AppendIfAbsent(clone.Tools, Tools, existingRefs, existingNames);

        // Source 3: MCP-discovered tools (lowest precedence).
        AppendIfAbsent(clone.Tools, mcpTools, existingRefs, existingNames);

        return clone;
    }

    private static void AppendIfAbsent(
        IList<AITool> target,
        IReadOnlyList<AIFunction> source,
        HashSet<AITool> existingRefs,
        HashSet<string> existingNames)
    {
        foreach (var tool in source)
        {
            if (existingRefs.Contains(tool))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(tool.Name) && existingNames.Contains(tool.Name))
            {
                continue;
            }

            target.Add(tool);
            existingRefs.Add(tool);
            if (!string.IsNullOrEmpty(tool.Name))
            {
                existingNames.Add(tool.Name);
            }
        }
    }

    /// <summary>
    /// Resolves MCP tools lazily on first call. Subsequent successful calls reuse
    /// the cached list. On failure the cache stays empty so the next call retries;
    /// the failure is wrapped as an <see cref="AgentMcpException"/>. Cancellation
    /// is propagated unwrapped.
    /// </summary>
    private async Task<IReadOnlyList<AIFunction>> ResolveMcpToolsAsync(CancellationToken cancellationToken)
    {
        if (_mcpToolSource is null)
        {
            return Array.Empty<AIFunction>();
        }

        if (_resolvedMcpTools is not null)
        {
            return _resolvedMcpTools;
        }

        await _resolveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resolvedMcpTools is not null)
            {
                return _resolvedMcpTools;
            }

            IReadOnlyList<AIFunction> resolved;
            try
            {
                resolved = await _mcpToolSource.GetToolsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AgentMcpException(
                    "MCP tool source failed to resolve tools.",
                    redactedEndpoint: null,
                    innerException: ex);
            }

            _resolvedMcpTools = resolved ?? Array.Empty<AIFunction>();
            return _resolvedMcpTools;
        }
        finally
        {
            _resolveLock.Release();
        }
    }
}

/// <summary>
/// <see cref="ChatClientBuilder"/> extension for registering the Strategos
/// AIFunction tool list as a named, inspectable chain step (DR-6).
/// </summary>
internal static class StrategosFunctionsChatClientBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="StrategosFunctionsChatClient"/> to the chat pipeline. The
    /// wrapper carries the registered <paramref name="tools"/> forward so a
    /// subsequent <c>UseFunctionInvocation</c> sees them on per-request
    /// <see cref="ChatOptions.Tools"/>. When <paramref name="mcpToolSource"/> is
    /// non-null, its tools are resolved lazily on first request and merged as
    /// a third source (DR-5).
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/>.</param>
    /// <param name="tools">The accumulated AIFunction tools from the builder.</param>
    /// <param name="mcpToolSource">Optional MCP tool source; resolved lazily.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    public static ChatClientBuilder UseStrategosFunctions(
        this ChatClientBuilder builder,
        IReadOnlyList<AIFunction> tools,
        IMcpToolSource? mcpToolSource = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tools);

        return builder.Use(inner => new StrategosFunctionsChatClient(inner, tools, mcpToolSource));
    }
}
