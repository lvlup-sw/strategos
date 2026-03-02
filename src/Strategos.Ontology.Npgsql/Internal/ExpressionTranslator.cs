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
            RootExpression => new TranslationResult(null, []),
            _ => throw new NotSupportedException($"Expression type {expression.GetType().Name} is not supported for SQL translation."),
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
        return $"data->>'{propertyName}' {sqlOp} {paramName}";
    }

    [RequiresDynamicCode("Expression translation may compile expressions dynamically.")]
    private static string TranslateMethodCall(MethodCallExpression methodCall, ParameterExpression param, List<SqlParameter> parameters)
    {
        if (methodCall.Method.Name == "Contains" && methodCall.Object is MemberExpression member)
        {
            var propertyName = ExtractPropertyName(member, param);
            var value = ExtractConstantValue(methodCall.Arguments[0]);
            var paramName = $"@p{parameters.Count}";
            parameters.Add(new SqlParameter(paramName, value));
            return $"data->>'{propertyName}' LIKE '%' || {paramName} || '%'";
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
}
