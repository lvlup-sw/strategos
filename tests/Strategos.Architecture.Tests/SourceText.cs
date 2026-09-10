// =============================================================================
// <copyright file="SourceText.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text;

namespace Strategos.Architecture.Tests;

/// <summary>
/// Blanks out the parts of a source file a code-coupling scan must ignore (comments and
/// string literals) while preserving every line break, so a hit can still be reported
/// against the original line. A blanked region keeps its length as spaces.
/// </summary>
internal static class SourceText
{
    /// <summary>
    /// Blanks C# comments (<c>//</c>, <c>/* */</c>), string literals (regular, verbatim,
    /// raw and their interpolated forms) and char literals. Interpolation holes are
    /// blanked with the string that holds them, which errs toward fewer hits.
    /// </summary>
    public static string StripCSharpCommentsAndStrings(string source)
    {
        var output = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                i = Blank(source, i, IndexOfOrEnd(source, "\n", i), output);
            }
            else if (c == '/' && next == '*')
            {
                var close = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = Blank(source, i, close < 0 ? source.Length : close + 2, output);
            }
            else if (c == '"' && next == '"' && i + 2 < source.Length && source[i + 2] == '"')
            {
                i = Blank(source, i, EndOfRawString(source, i), output);
            }
            else if (c == '"')
            {
                i = Blank(source, i, EndOfRegularString(source, i), output);
            }
            else if (c == '@' && next == '"')
            {
                i = Blank(source, i, EndOfVerbatimString(source, i + 1), output);
            }
            else if (c == '\'')
            {
                i = Blank(source, i, EndOfCharLiteral(source, i), output);
            }
            else
            {
                output.Append(c);
                i++;
            }
        }

        return output.ToString();
    }

    /// <summary>Blanks XML comments (<c>&lt;!-- --&gt;</c>).</summary>
    public static string StripXmlComments(string source)
    {
        var output = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var open = source.IndexOf("<!--", i, StringComparison.Ordinal);
            if (open < 0)
            {
                output.Append(source, i, source.Length - i);
                break;
            }

            output.Append(source, i, open - i);
            var close = source.IndexOf("-->", open + 4, StringComparison.Ordinal);
            i = Blank(source, open, close < 0 ? source.Length : close + 3, output);
        }

        return output.ToString();
    }

    private static int Blank(string source, int start, int end, StringBuilder output)
    {
        for (var i = start; i < end; i++)
        {
            output.Append(source[i] == '\n' ? '\n' : ' ');
        }

        return end;
    }

    private static int IndexOfOrEnd(string source, string value, int start)
    {
        var index = source.IndexOf(value, start, StringComparison.Ordinal);
        return index < 0 ? source.Length : index;
    }

    private static int EndOfRegularString(string source, int openQuote)
    {
        var i = openQuote + 1;
        while (i < source.Length)
        {
            if (source[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (source[i] == '"' || source[i] == '\n')
            {
                return i + 1;
            }

            i++;
        }

        return source.Length;
    }

    private static int EndOfVerbatimString(string source, int openQuote)
    {
        var i = openQuote + 1;
        while (i < source.Length)
        {
            if (source[i] == '"')
            {
                if (i + 1 < source.Length && source[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return source.Length;
    }

    private static int EndOfRawString(string source, int openQuote)
    {
        var quotes = 0;
        while (openQuote + quotes < source.Length && source[openQuote + quotes] == '"')
        {
            quotes++;
        }

        var fence = new string('"', quotes);
        var close = source.IndexOf(fence, openQuote + quotes, StringComparison.Ordinal);
        return close < 0 ? source.Length : close + quotes;
    }

    private static int EndOfCharLiteral(string source, int openQuote)
    {
        var i = openQuote + 1;
        if (i < source.Length && source[i] == '\\')
        {
            i++;
        }

        var close = source.IndexOf('\'', Math.Min(i + 1, source.Length));
        return close < 0 || close - openQuote > 12 ? openQuote + 1 : close + 1;
    }
}
