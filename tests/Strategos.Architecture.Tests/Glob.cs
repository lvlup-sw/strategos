// =============================================================================
// <copyright file="Glob.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text;
using System.Text.RegularExpressions;

namespace Strategos.Architecture.Tests;

/// <summary>
/// The subset of glob syntax the catalog's <c>applies-to</c> entries use, compiled to
/// a regex over forward-slash repo-relative paths: <c>**</c> spans any number of
/// segments (including none), <c>*</c> and <c>?</c> stay inside one segment.
/// Implemented here because Microsoft.Extensions.FileSystemGlobbing is not in
/// Directory.Packages.props and the catalog needs nothing more.
/// </summary>
internal static class Glob
{
    /// <summary>Compiles <paramref name="glob"/> to an anchored regex.</summary>
    public static Regex ToRegex(string glob)
    {
        var pattern = new StringBuilder("^");
        var segments = glob.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var last = i == segments.Length - 1;
            if (segment == "**")
            {
                pattern.Append(last ? ".*" : "(?:.*/)?");
                continue;
            }

            foreach (var c in segment)
            {
                pattern.Append(c switch
                {
                    '*' => "[^/]*",
                    '?' => "[^/]",
                    _ => Regex.Escape(c.ToString()),
                });
            }

            if (!last)
            {
                pattern.Append('/');
            }
        }

        pattern.Append('$');
        return new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
    }
}
