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
    /// <summary>Gets the stable diagnostic identifier (<see cref="AgentDiagnostics.AGAG006"/>).</summary>
    public override string Diagnostic => AgentDiagnostics.AGAG006;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentChatResponseException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the null or empty chat response.</param>
    public AgentChatResponseException(string message)
        : base($"{message} Diagnostic: {AgentDiagnostics.AGAG006}.")
    {
    }
}
