// -----------------------------------------------------------------------
// <copyright file="WorkflowIdentity.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions;

/// <summary>
/// An opaque, header-safe identifier for a Strategos workflow instance.
/// </summary>
/// <remarks>
/// <para>
/// Stored as a string so it can ride on a Wolverine envelope header
/// (<c>x-strategos-workflow-identity</c>) across the outbox and every
/// supported transport without additional encoding. Strategos does not inspect
/// the value; the basileus SPIFFE adapter shapes it as
/// <c>spiffe://td/workflow/&lt;id&gt;</c>.
/// </para>
/// <para>
/// Sealed per INV-6. Immutable per INV-7.
/// </para>
/// <para>
/// Declared as a positional record with a custom <c>init</c>-setter that
/// re-runs the DR-2 validator on every assignment. This closes the
/// <c>with</c>-clone bypass that a body-syntax record exposes: the synthetic
/// copy constructor used by <c>with</c> invokes the property's <c>init</c>
/// setter, so the validator runs there too. Without the custom <c>init</c>
/// setter, <c>existing with { Value = "bad-é-value" }</c> would silently
/// produce an invalid record.
/// </para>
/// </remarks>
/// <param name="Value">
/// The header-safe identifier value. Must be non-null, non-empty, and contain
/// only printable ASCII (0x20..0x7E).
/// </param>
public sealed record WorkflowIdentity(string Value)
{
    private readonly string value = ValidateAndReturn(Value);

    /// <summary>
    /// Gets the header-safe identifier value.
    /// </summary>
    public string Value
    {
        get => this.value;
        init => this.value = ValidateAndReturn(value);
    }

    private static string ValidateAndReturn(string value)
    {
        IdentityValueValidator.Validate(value, nameof(Value));
        return value;
    }
}
