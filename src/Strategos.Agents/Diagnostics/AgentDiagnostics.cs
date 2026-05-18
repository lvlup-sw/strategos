// =============================================================================
// <copyright file="AgentDiagnostics.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Diagnostics;

/// <summary>
/// Stable diagnostic identifiers for the Strategos.Agents runtime (INV-5 family AGAG###).
/// </summary>
public static class AgentDiagnostics
{
    /// <summary>Builder validation failure — a required hook delegate was missing at Build() time.</summary>
    public const string AGAG001 = "AGAG001";

    /// <summary>Structured output deserialization failed — ChatResponse&lt;T&gt;.TryGetResult returned false.</summary>
    public const string AGAG002 = "AGAG002";

    /// <summary>Duplicate tool name registered on an AgentStepBuilder.</summary>
    public const string AGAG003 = "AGAG003";

    /// <summary>MCP client handshake or tool-discovery failure.</summary>
    public const string AGAG004 = "AGAG004";

    /// <summary>Tool-invocation iteration count exceeded the configured maximum.</summary>
    public const string AGAG005 = "AGAG005";

    /// <summary>Chat client returned a null or empty ChatResponse&lt;T&gt;.</summary>
    public const string AGAG006 = "AGAG006";
}
