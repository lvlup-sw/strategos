using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql.Internal;

/// <summary>
/// Translates ObjectSetExpression trees into SQL WHERE clauses with parameters.
/// </summary>
internal static class ExpressionTranslator
{
    /// <summary>
    /// Result of translating an expression tree into SQL.
    /// </summary>
    internal sealed record TranslationResult(string? WhereClause, IReadOnlyList<SqlParameter> Parameters);

    /// <summary>
    /// A named SQL parameter with its value.
    /// </summary>
    internal sealed record SqlParameter(string Name, object Value);

    /// <summary>
    /// Translates an ObjectSetExpression into a SQL WHERE clause and parameters.
    /// Returns null WhereClause for RootExpression (no filter).
    /// </summary>
    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    internal static TranslationResult Translate(ObjectSetExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            FilterExpression filter => TranslateFilter(filter),
            IncludeExpression include => Translate(include.Source),
            RootExpression => new TranslationResult(null, []),
            _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported for SQL translation."),
        };
    }

    /// <summary>
    /// Whether <paramref name="expression"/> is a link TRAVERSAL — i.e. its
    /// outermost producing node (walking past <see cref="FilterExpression"/> /
    /// <see cref="IncludeExpression"/>) is a <see cref="TraverseLinkExpression"/>
    /// (DR-12). A traversal is NOT a plain WHERE-over-one-table query, so the
    /// provider must route it through
    /// <c>PgVectorObjectSetProvider.LowerTraversalExpression</c> rather than the
    /// single-table <see cref="Translate(ObjectSetExpression)"/> path: <c>Translate</c>
    /// throws <see cref="NotSupportedException"/> on a traversal because the
    /// vertex ⋈ junction ⋈ vertex lowering is junction-aware and graph-driven.
    /// </summary>
    internal static bool IsTraversal(ObjectSetExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            TraverseLinkExpression => true,
            FilterExpression filter => IsTraversal(filter.Source),
            IncludeExpression include => IsTraversal(include.Source),
            _ => false,
        };
    }

    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static TranslationResult TranslateFilter(FilterExpression filter)
    {
        // Translate the source first so we know the parameter offset.
        var sourceResult = Translate(filter.Source);

        // Start with source parameters so the current filter's parameter numbering continues from there.
        var parameters = new List<SqlParameter>(sourceResult.Parameters);
        var whereClause = TranslateLambda(filter.Predicate, parameters);

        // If the source had a WHERE clause, chain them with AND.
        if (sourceResult.WhereClause is not null)
        {
            whereClause = $"{sourceResult.WhereClause} AND {whereClause}";
        }

        return new TranslationResult(whereClause, parameters);
    }

    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static string TranslateLambda(LambdaExpression lambda, List<SqlParameter> parameters)
    {
        return TranslateExpression(lambda.Body, lambda.Parameters[0], parameters);
    }

    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static string TranslateExpression(Expression expression, ParameterExpression param, List<SqlParameter> parameters)
    {
        return expression switch
        {
            BinaryExpression binary => TranslateBinary(binary, param, parameters),
            MethodCallExpression methodCall => TranslateMethodCall(methodCall, param, parameters),
            _ => throw new NotSupportedException($"Expression type {expression.NodeType} is not supported for SQL translation."),
        };
    }

    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static string TranslateBinary(BinaryExpression binary, ParameterExpression param, List<SqlParameter> parameters)
    {
        // Handle logical AND/OR
        if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            var left = TranslateExpression(binary.Left, param, parameters);
            var right = TranslateExpression(binary.Right, param, parameters);
            var op = binary.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
            return $"({left} {op} {right})";
        }

        // Handle comparison operators
        var sqlOp = binary.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "!=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw new NotSupportedException($"Binary operator {binary.NodeType} is not supported."),
        };

        var propertyName = ExtractPropertyName(binary.Left, param);
        var value = ExtractConstantValue(binary.Right);
        var paramName = $"@p{parameters.Count}";
        parameters.Add(new SqlParameter(paramName, value));

        // Use JSONB accessor for property access: data->>'PropertyName'
        // Cast to appropriate type for correct comparison semantics (e.g., numeric ordering).
        // The key is interpolated into a single-quoted JSON-path literal (NOT a bindable
        // parameter), so it must be escaped the same way the rest of the generator routes
        // descriptor-derived keys — single-quote doubling via SqlGenerator.EscapeStringLiteral
        // (review F4). No-op for legal member names; keeps the SQL safe regardless of caller.
        var jsonbAccessor = $"data->>'{SqlGenerator.EscapeStringLiteral(propertyName)}'";
        var cast = GetJsonbCast(value);
        var lhs = cast.Length > 0 ? $"({jsonbAccessor}){cast}" : jsonbAccessor;
        return $"{lhs} {sqlOp} {paramName}";
    }

    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static string TranslateMethodCall(MethodCallExpression methodCall, ParameterExpression param, List<SqlParameter> parameters)
    {
        if (methodCall.Method.Name == "Contains" && methodCall.Object is MemberExpression member)
        {
            var propertyName = ExtractPropertyName(member, param);
            var value = ExtractConstantValue(methodCall.Arguments[0]);
            var paramName = $"@p{parameters.Count}";

            // Escape LIKE wildcards in the search value to prevent unintended pattern matching
            var escapedValue = value is string s
                ? s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")
                : value;
            parameters.Add(new SqlParameter(paramName, escapedValue));

            // The key is interpolated into a single-quoted JSON-path literal (NOT a bindable
            // parameter), so escape it via SqlGenerator.EscapeStringLiteral exactly as the
            // binary-comparison accessor does (review F4).
            return $"data->>'{SqlGenerator.EscapeStringLiteral(propertyName)}' LIKE '%' || {paramName} || '%' ESCAPE '\\'";
        }

        throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported for SQL translation.");
    }

    private static string ExtractPropertyName(Expression expression, ParameterExpression param)
    {
        if (expression is MemberExpression memberExpr && memberExpr.Expression == param)
        {
            return memberExpr.Member.Name;
        }

        // Handle conversion expressions (e.g., (object)x.Property)
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            return ExtractPropertyName(unary.Operand, param);
        }

        throw new NotSupportedException($"Cannot extract property name from expression: {expression}");
    }

    [RequiresDynamicCode("Compiling expressions requires dynamic code generation.")]
    private static object ExtractConstantValue(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            return constant.Value!;
        }

        // Handle captured variables (member access on a closure)
        if (expression is MemberExpression memberExpr && memberExpr.Expression is ConstantExpression closureConstant)
        {
            var field = memberExpr.Member as System.Reflection.FieldInfo;
            if (field is not null)
            {
                return field.GetValue(closureConstant.Value)!;
            }

            var prop = memberExpr.Member as System.Reflection.PropertyInfo;
            if (prop is not null)
            {
                return prop.GetValue(closureConstant.Value)!;
            }
        }

        // Handle conversion expressions
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            return ExtractConstantValue(unary.Operand);
        }

        // Try to compile and evaluate
        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke()!;
    }

    /// <summary>
    /// Returns a PostgreSQL type cast suffix for JSONB text accessor values
    /// to ensure correct comparison semantics (e.g., numeric ordering instead of lexicographic).
    /// </summary>
    private static string GetJsonbCast(object value) => value switch
    {
        int or long or short or byte or sbyte or
        uint or ulong or ushort or
        float or double or decimal => "::numeric",
        bool => "::boolean",
        DateTime or DateTimeOffset => "::timestamptz",
        _ => string.Empty,
    };
}
