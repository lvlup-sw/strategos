// =============================================================================
// <copyright file="StrategosFunctionsChatClient.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;

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
/// Marked <c>internal</c> — this type is an implementation detail of
/// <c>AgentStepBuilder.Build</c> and not part of the public surface. Tests in
/// <c>Strategos.Agents.Tests</c> see it through <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class StrategosFunctionsChatClient : DelegatingChatClient
{
    /// <summary>Initializes a new instance of the <see cref="StrategosFunctionsChatClient"/> class.</summary>
    /// <param name="innerClient">The wrapped inner chat client.</param>
    /// <param name="tools">The Strategos-registered <see cref="AIFunction"/> tools.</param>
    public StrategosFunctionsChatClient(IChatClient innerClient, IReadOnlyList<AIFunction> tools)
        : base(innerClient)
    {
        ArgumentNullException.ThrowIfNull(tools);
        Tools = tools;
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
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(messages, MergeTools(options), cancellationToken);
    }

    /// <inheritdoc/>
    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetStreamingResponseAsync(messages, MergeTools(options), cancellationToken);
    }

    /// <summary>
    /// Produces a per-request <see cref="ChatOptions"/> that merges the
    /// Strategos-registered <see cref="Tools"/> into the caller-supplied
    /// <paramref name="options"/> without mutating the original or overwriting
    /// host-supplied tools. Host tools are preserved; Strategos tools are
    /// appended only when no tool with the same <see cref="AITool.Name"/> is
    /// already present (host wins on name collision).
    /// </summary>
    /// <param name="options">The caller-supplied options (may be <see langword="null"/>).</param>
    /// <returns>A cloned <see cref="ChatOptions"/> with the merged <see cref="ChatOptions.Tools"/>.</returns>
    private ChatOptions MergeTools(ChatOptions? options)
    {
        // Clone so we never mutate caller state — MEAI 10.5 exposes Clone() on ChatOptions.
        var clone = options?.Clone() ?? new ChatOptions();

        if (Tools.Count == 0)
        {
            return clone;
        }

        if (clone.Tools is null)
        {
            // Seed with the Strategos tools.
            clone.Tools = new List<AITool>(Tools);
            return clone;
        }

        // Append Strategos tools that aren't already present (by name OR reference).
        // Reference comparison handles re-registration; name comparison is the
        // contract FunctionInvokingChatClient uses for lookup.
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

        foreach (var tool in Tools)
        {
            if (existingRefs.Contains(tool))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(tool.Name) && existingNames.Contains(tool.Name))
            {
                continue;
            }

            clone.Tools.Add(tool);
        }

        return clone;
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
    /// <see cref="ChatOptions.Tools"/>.
    /// </summary>
    /// <param name="builder">The <see cref="ChatClientBuilder"/>.</param>
    /// <param name="tools">The accumulated AIFunction tools from the builder.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    public static ChatClientBuilder UseStrategosFunctions(
        this ChatClientBuilder builder,
        IReadOnlyList<AIFunction> tools)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tools);

        return builder.Use(inner => new StrategosFunctionsChatClient(inner, tools));
    }
}
