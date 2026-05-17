// -----------------------------------------------------------------------
// <copyright file="IdentityValueValidator.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Strategos.Identity.Abstractions;

/// <summary>
/// Shared validation for identity record values.
/// </summary>
/// <remarks>
/// Anchors DR-8 rows 3 and 6: identity record values must be non-null, non-empty
/// (after trim), and confined to printable ASCII (0x20..0x7E) so they can ride on
/// Wolverine envelope headers without transport-level escaping.
/// </remarks>
internal static class IdentityValueValidator
{
    /// <summary>
    /// Validates an identity value, throwing if it violates the contract.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The parameter name used in the thrown exception.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, whitespace-only, or contains characters
    /// outside the printable ASCII subset.
    /// </exception>
    public static void Validate(string value, string paramName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identity value must be non-empty.", paramName);
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c < 0x20 || c > 0x7E)
            {
                throw new ArgumentException(
                    "Identity value must contain only printable ASCII characters (0x20..0x7E).",
                    paramName);
            }
        }
    }
}
