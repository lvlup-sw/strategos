// =============================================================================
// <copyright file="UriRedaction.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.RegularExpressions;

namespace Strategos.Agents.Extensions;

/// <summary>
/// URI user-info redaction helper (DR-10 / #85). Removes the user-info segment
/// (<c>user:pass@</c>) from any <c>scheme://user:pass@host/…</c> substrings in
/// a message string. Scope is intentionally narrow: URI user-info only.
/// Arbitrary secret scrubbing is outside the contract.
/// </summary>
internal static partial class UriRedaction
{
    // Matches: scheme "://" followed by one or more non-whitespace characters
    // that contain "@", up to and including the "@". Captures the scheme prefix
    // so the replacement keeps it intact.
    // Pattern: (scheme://) <user-info> @
    //   where <user-info> is any sequence of non-whitespace chars with no "@"
    //   before the final "@".
    [GeneratedRegex(@"([a-zA-Z][a-zA-Z0-9+.\-]*://)[^/@\s]*@", RegexOptions.CultureInvariant)]
    private static partial Regex UserInfoPattern();

    /// <summary>
    /// Replaces every <c>scheme://user:pass@</c> substring in <paramref name="message"/>
    /// with <c>scheme://</c>, eliding the user-info segment.
    /// </summary>
    /// <param name="message">The message to redact.</param>
    /// <returns>The redacted message.</returns>
    internal static string RedactUserInfo(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        return UserInfoPattern().Replace(message, "$1");
    }
}
