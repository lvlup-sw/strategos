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

    protected AgentException(string message) : base(message)
    {
    }

    protected AgentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
