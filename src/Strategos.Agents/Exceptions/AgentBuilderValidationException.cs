// =============================================================================
// <copyright file="AgentBuilderValidationException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when <c>AgentStepBuilder.Build()</c> is invoked with a required hook missing (DR-2).
/// </summary>
public sealed class AgentBuilderValidationException : AgentException
{
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG001"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG001;

    /// <summary>Name of the missing required hook (SystemPrompt / UserPrompt / ApplyResult).</summary>
    public string MissingHook { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentBuilderValidationException"/> class.
    /// </summary>
    /// <param name="missingHook">The name of the required hook that was not configured.</param>
    public AgentBuilderValidationException(string missingHook)
        : base($"AgentStepBuilder.Build() failed: required hook '{missingHook}' was not configured. Call .With{missingHook}(...) before .Build().")
    {
        MissingHook = missingHook;
    }
}
