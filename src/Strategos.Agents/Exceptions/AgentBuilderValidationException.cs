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
    public override string Diagnostic => AgentDiagnostics.AGAG001;

    /// <summary>Name of the missing required hook (SystemPrompt / UserPrompt / ApplyResult).</summary>
    public string MissingHook { get; }

    public AgentBuilderValidationException(string missingHook)
        : base($"AgentStepBuilder.Build() failed: required hook '{missingHook}' was not configured. Call .With{missingHook}(...) before .Build().")
    {
        MissingHook = missingHook;
    }
}
