using System.Text;

namespace Strategos.Ontology.Npgsql.Internal;

/// <summary>
/// Maps CLR type names to PostgreSQL-friendly snake_case identifiers.
/// </summary>
internal static class TypeMapper
{
    /// <summary>
    /// Converts a PascalCase type name to snake_case.
    /// Examples: "DocumentChunk" -> "document_chunk", "HTTPClient" -> "http_client".
    /// </summary>
    internal static string ToSnakeCase(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return typeName;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < typeName.Length; i++)
        {
            var c = typeName[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    // Insert underscore before an uppercase letter that is preceded by a lowercase letter,
                    // or that starts a new word after an acronym (e.g., "HTTPClient" -> "http_client").
                    var prevIsLower = char.IsLower(typeName[i - 1]);
                    var nextIsLower = i + 1 < typeName.Length && char.IsLower(typeName[i + 1]);
                    if (prevIsLower || (char.IsUpper(typeName[i - 1]) && nextIsLower))
                    {
                        sb.Append('_');
                    }
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the snake_case table name for a CLR type.
    /// </summary>
    internal static string GetTableName<T>() => ToSnakeCase(typeof(T).Name);
}
