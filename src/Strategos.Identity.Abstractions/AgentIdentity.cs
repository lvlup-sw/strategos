// -----------------------------------------------------------------------
// <copyright file="AgentIdentity.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions;

/// <summary>
/// An opaque, header-safe identifier for the agent attributed to a single
/// saga step execution.
/// </summary>
/// <remarks>
/// <para>
/// Stored as a string so it can ride on a Wolverine envelope header
/// (<c>x-strategos-agent-identity</c>) across the outbox and every supported
/// transport without additional encoding. Strategos does not inspect the value;
/// the basileus SPIFFE adapter shapes it as
/// <c>spiffe://td/workflow/&lt;id&gt;/step/&lt;phase&gt;</c>.
/// </para>
/// <para>
/// Sealed per INV-6. Immutable per INV-7. Unlike workflow identity, agent
/// identity is per-message-derived; each handler stamps its own and the
/// header is NOT propagated to outgoing messages.
/// </para>
/// </remarks>
public sealed record AgentIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentIdentity"/> class.
    /// </summary>
    /// <param name="value">
    /// The header-safe identifier value. Must be non-null, non-empty, and contain
    /// only printable ASCII (0x20..0x7E).
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, whitespace-only, or contains
    /// non-printable-ASCII characters.
    /// </exception>
    public AgentIdentity(string value)
    {
        IdentityValueValidator.Validate(value, nameof(value));
        this.Value = value;
    }

    /// <summary>
    /// Gets the header-safe identifier value.
    /// </summary>
    public string Value { get; init; }
}
