// =============================================================================
// <copyright file="AgentDuplicateToolException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when <c>AgentStepBuilder.Build()</c> detects two AIFunctions with the same name (DR-4).
/// </summary>
public sealed class AgentDuplicateToolException : AgentException
{
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG003"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG003;

    /// <summary>The colliding tool name.</summary>
    public string ToolName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentDuplicateToolException"/> class.
    /// </summary>
    /// <param name="toolName">The duplicate AIFunction tool name that was registered.</param>
    public AgentDuplicateToolException(string toolName)
        : base($"Duplicate AIFunction tool name '{toolName}' registered on AgentStepBuilder. Diagnostic: {AgentDiagnostics.AGAG003}.")
    {
        ToolName = toolName;
    }
}
