// =============================================================================
// <copyright file="AgentToolAttribute.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Abstractions;

/// <summary>
/// Marks a method as a tool an agent step may call during a turn (DR-8). An optional
/// <see cref="Name"/> overrides the tool name the agent sees; otherwise the method name is used.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgentToolAttribute : Attribute
{
    /// <summary>Optional tool name override; when null the declaring method's name is used.</summary>
    public string? Name { get; init; }
}
