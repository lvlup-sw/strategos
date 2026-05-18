// =============================================================================
// <copyright file="AgentChatResponseException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Agents.Diagnostics;

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Thrown when the chat client returns null or an empty ChatResponse&lt;T&gt; (DR-10).
/// </summary>
public sealed class AgentChatResponseException : AgentException
{
    public override string Diagnostic => AgentDiagnostics.AGAG006;

    public AgentChatResponseException(string message)
        : base($"{message} Diagnostic: {AgentDiagnostics.AGAG006}.")
    {
    }
}
