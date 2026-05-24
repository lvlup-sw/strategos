// =============================================================================
// <copyright file="AgentToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents;

/// <summary>
/// In-process <see cref="IToolSource"/> adapter (DR-8). Builds <see cref="AIFunction"/>s
/// from ordinary CLR members rather than an external protocol: either by reflecting the
/// <c>[AgentTool]</c>-annotated methods of an instance (<see cref="FromObject"/>) or by
/// wrapping explicit delegates (<see cref="FromDelegates"/>). Each
/// <see cref="System.ComponentModel.DescriptionAttribute"/> on a method flows into the
/// resulting AIFunction, and <see cref="AgentToolAttribute.Name"/> overrides the tool
/// name. The adapter carries no <c>ModelContextProtocol</c> dependency (INV-3) and
/// surfaces any reflection/factory failure as an <see cref="AgentToolSourceException"/>
/// (AGAG007) naming the source type.
/// </summary>
public sealed class AgentToolSource : IToolSource
{
    private readonly Func<IReadOnlyList<AIFunction>> _build;
    private readonly string _sourceType;

    private AgentToolSource(string sourceType, Func<IReadOnlyList<AIFunction>> build)
    {
        _sourceType = sourceType;
        _build = build;
    }

    /// <summary>
    /// Creates a tool source from the <c>[AgentTool]</c>-annotated public and non-public
    /// instance methods declared on <paramref name="instance"/>'s runtime type. Methods
    /// without the attribute are ignored; a type with no annotated methods yields an
    /// empty (non-null) tool list.
    /// </summary>
    /// <param name="instance">The object whose annotated methods become tools.</param>
    /// <returns>An in-process tool source over the annotated methods.</returns>
    public static AgentToolSource FromObject(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var type = instance.GetType();
        return new AgentToolSource(type.FullName ?? type.Name, () =>
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var tools = new List<AIFunction>();
            foreach (var method in type.GetMethods(flags))
            {
                var attribute = method.GetCustomAttribute<AgentToolAttribute>(inherit: false);
                if (attribute is null)
                {
                    continue;
                }

                tools.Add(AIFunctionFactory.Create(
                    method,
                    instance,
                    new AIFunctionFactoryOptions { Name = attribute.Name }));
            }

            return tools;
        });
    }

    /// <summary>
    /// Creates a tool source from explicit delegates. Each delegate becomes one
    /// <see cref="AIFunction"/> via <see cref="AIFunctionFactory"/>.
    /// </summary>
    /// <param name="delegates">The delegates to expose as tools.</param>
    /// <returns>An in-process tool source over the supplied delegates.</returns>
    public static AgentToolSource FromDelegates(params Delegate[] delegates)
    {
        ArgumentNullException.ThrowIfNull(delegates);

        return new AgentToolSource(nameof(AgentToolSource), () =>
        {
            var tools = new List<AIFunction>(delegates.Length);
            foreach (var d in delegates)
            {
                ArgumentNullException.ThrowIfNull(d, nameof(delegates));
                tools.Add(AIFunctionFactory.Create(d));
            }

            return tools;
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return Task.FromResult(_build());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AgentToolSourceException(
                "In-process tool source failed to resolve its AIFunctions.",
                _sourceType,
                ex);
        }
    }
}
