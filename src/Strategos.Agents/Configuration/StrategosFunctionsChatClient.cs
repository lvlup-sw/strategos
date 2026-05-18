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
/// chain step (DR-4, DR-6). Behaviorally a pass-through: a subsequent
/// <see cref="FunctionInvokingChatClient"/> (added via
/// <c>FunctionInvokingChatClientBuilderExtensions.UseFunctionInvocation</c>) reads
/// the per-request <see cref="ChatOptions.Tools"/>, so this client's primary job is
/// to make the "register Strategos tools" step explicit and discoverable in the
/// composed pipeline (via <see cref="IChatClient.GetService"/>).
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
