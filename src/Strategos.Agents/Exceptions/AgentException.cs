// =============================================================================
// <copyright file="AgentException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Exceptions;

/// <summary>
/// Base type for all Strategos.Agents runtime exceptions. Every concrete subclass
/// declares a stable <see cref="Diagnostic"/> identifier in the AGAG### family (INV-5).
/// </summary>
public abstract class AgentException : Exception
{
    /// <summary>Stable diagnostic identifier (AGAG###).</summary>
    public abstract string Diagnostic { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    protected AgentException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    protected AgentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
