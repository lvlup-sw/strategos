// =============================================================================
// <copyright file="StrategosFunctionsChatClient.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Exceptions;
using Strategos.Agents.Extensions;

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
/// Tool-source resolution (DR-9): each registered <see cref="IToolSource"/> is
/// resolved lazily on the first per-request call to <c>GetResponseAsync</c> /
/// <c>GetStreamingResponseAsync</c>. Each source is resolved at most once and the
/// aggregated <see cref="AIFunction"/>s are cached for the lifetime of this
/// middleware instance, then merged into <see cref="ChatOptions.Tools"/> after the
/// host-supplied and Strategos-registered tools. If resolution fails, the cache
/// stays empty and the next request retries. Conforming adapters (those that throw
/// an <see cref="AgentException"/> subtype) propagate unchanged; foreign exceptions
/// are wrapped in <see cref="AgentToolSourceException"/> (AGAG007) after URI
/// user-info redaction (DR-10 / #85). Cancellation flows through unwrapped.
/// </para>
/// <para>
/// Name-collision precedence (host &gt; Strategos &gt; tool-sources): the host's
/// per-request <see cref="ChatOptions.Tools"/> express the most specific intent
/// and win. Strategos in-process tools win over source-discovered tools because the
/// agent owns its own contract while sources are fallback skill providers. Among
/// tool-sources, registration order decides.
/// </para>
/// </remarks>
internal sealed class StrategosFunctionsChatClient : DelegatingChatClient
{
    private readonly IReadOnlyList<IToolSource> _toolSources;
    private readonly SemaphoreSlim _resolveLock = new(1, 1);
    private IReadOnlyList<AIFunction>? _resolvedSourceTools;

    /// <summary>Initializes a new instance of the <see cref="StrategosFunctionsChatClient"/> class.</summary>
    /// <param name="innerClient">The wrapped inner chat client.</param>
    /// <param name="tools">The Strategos-registered <see cref="AIFunction"/> tools.</param>
    /// <param name="toolSources">Registered tool sources (DR-9); each resolved lazily on first request. Never null; may be empty.</param>
    public StrategosFunctionsChatClient(
        IChatClient innerClient,
        IReadOnlyList<AIFunction> tools,
        IReadOnlyList<IToolSource> toolSources)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(toolSources);
        Tools = tools;
        _toolSources = toolSources;
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
    /// and (3) tool-source-discovered tools (resolved lazily on first call and
    /// cached). Merge order is [host, Strategos, sources]; tools whose
    /// <see cref="AITool.Name"/> is already present are skipped (host wins, then
    /// Strategos, then sources in registration order).
    /// </summary>
    private async Task<ChatOptions> MergeToolsAsync(ChatOptions? options, CancellationToken cancellationToken)
    {
        // Clone so we never mutate caller state — MEAI 10.5 exposes Clone() on ChatOptions.
        var clone = options?.Clone() ?? new ChatOptions();

        var sourceTools = await ResolveToolsAsync(cancellationToken).ConfigureAwait(false);

        if (Tools.Count == 0 && sourceTools.Count == 0)
        {
            return clone;
        }

        clone.Tools ??= new List<AITool>();
        var (existingRefs, existingNames) = BuildExistingToolSet(clone.Tools);

        // Source 2: Strategos-registered tools (wins over tool-sources, loses to host).
        AppendIfAbsent(clone.Tools, Tools, existingRefs, existingNames);

        // Source 3: tool-source-discovered tools (lowest precedence, registration order).
        AppendIfAbsent(clone.Tools, sourceTools, existingRefs, existingNames);

        return clone;
    }

    private static (HashSet<AITool> Refs, HashSet<string> Names) BuildExistingToolSet(IList<AITool> tools)
    {
        var refs = new HashSet<AITool>(ReferenceEqualityComparer.Instance);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in tools)
        {
            refs.Add(t);
            if (!string.IsNullOrEmpty(t.Name))
            {
                names.Add(t.Name);
            }
        }

        return (refs, names);
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
    /// Resolves every registered tool source lazily on first call, each at most once,
    /// aggregating their <see cref="AIFunction"/>s in registration order. Subsequent
    /// successful calls reuse the cached list. On failure the cache stays empty so the
    /// next call retries.
    /// </summary>
    /// <remarks>
    /// Exception-propagation contract at this boundary (catch order is significant):
    /// <list type="number">
    /// <item><description>
    /// <see cref="OperationCanceledException"/> — propagated unwrapped. Cancellation is not a
    /// domain failure and must never be classified as <see cref="AgentToolSourceException"/>.
    /// </description></item>
    /// <item><description>
    /// <see cref="AgentException"/> subtypes — propagated unchanged. Conforming adapters
    /// (e.g. <see cref="AgentMcpException"/> AGAG004, <see cref="AgentToolSourceException"/>
    /// AGAG007) have already applied their own redaction; re-wrapping them would produce
    /// nested AGAG007 payloads.
    /// </description></item>
    /// <item><description>
    /// Any other exception — wrapped in <see cref="AgentToolSourceException"/> (AGAG007) with
    /// <see cref="UriRedaction.RedactUserInfo"/> applied to the message. This guards against
    /// third-party adapters that embed raw credentials in exception messages (DR-10 / #85).
    /// </description></item>
    /// </list>
    /// </remarks>
    private async Task<IReadOnlyList<AIFunction>> ResolveToolsAsync(CancellationToken cancellationToken)
    {
        if (_toolSources.Count == 0)
        {
            return Array.Empty<AIFunction>();
        }

        if (_resolvedSourceTools is not null)
        {
            return _resolvedSourceTools;
        }

        await _resolveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resolvedSourceTools is not null)
            {
                return _resolvedSourceTools;
            }

            var aggregated = new List<AIFunction>();
            foreach (var source in _toolSources)
            {
                IReadOnlyList<AIFunction>? resolved;
                try
                {
                    resolved = await source.GetToolsAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is not a domain failure — propagate unwrapped.
                    throw;
                }
                catch (AgentException)
                {
                    // Conforming adapter: already self-redacted; propagate unchanged.
                    throw;
                }
                catch (Exception ex)
                {
                    // Foreign (non-conforming) adapter: redact URI user-info and wrap.
                    var redacted = UriRedaction.RedactUserInfo(ex.Message);
                    throw new AgentToolSourceException(redacted, source.GetType().FullName, ex);
                }

                if (resolved is not null)
                {
                    aggregated.AddRange(resolved);
                }
            }

            _resolvedSourceTools = aggregated;
            return _resolvedSourceTools;
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
    /// <see cref="ChatOptions.Tools"/>. Each source in <paramref name="toolSources"/>
    /// is resolved lazily on first request and merged after the host- and
    /// Strategos-supplied tools (DR-9).
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/>.</param>
    /// <param name="tools">The accumulated AIFunction tools from the builder.</param>
    /// <param name="toolSources">Registered tool sources; resolved lazily. Never null; may be empty.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    public static ChatClientBuilder UseStrategosFunctions(
        this ChatClientBuilder builder,
        IReadOnlyList<AIFunction> tools,
        IReadOnlyList<IToolSource> toolSources)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(toolSources);

        return builder.Use(inner => new StrategosFunctionsChatClient(inner, tools, toolSources));
    }
}
