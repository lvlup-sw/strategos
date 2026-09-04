using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;

namespace Strategos.Ontology.Builder;

internal static class ExpressionHelper
{
    public static string ExtractMemberName<T, TResult>(Expression<Func<T, TResult>> expression)
    {
        var member = ExtractMemberExpression(expression.Body);
        return member.Member.Name;
    }

    public static Type ExtractMemberType<T, TResult>(Expression<Func<T, TResult>> expression)
    {
        var member = ExtractMemberExpression(expression.Body);
        return member.Member switch
        {
            PropertyInfo prop => prop.PropertyType,
            FieldInfo field => field.FieldType,
            _ => typeof(object),
        };
    }

    public static string ExtractPredicateString<T>(Expression<Func<T, bool>> predicate)
    {
        var body = predicate.Body.ToString();

        // Strip the parameter prefix (e.g., "p." or "p => p.")
        var paramName = predicate.Parameters[0].Name ?? "p";
        body = body.Replace($"{paramName}.", string.Empty);

        return body;
    }

    public static string ExtractMethodName<TTool>(Expression<Func<TTool, Delegate>> methodSelector)
    {
        // Method group expressions compile to a unary Convert wrapping a CreateDelegate call:
        //   Convert(MethodInfo.CreateDelegate(DelegateType, target), Delegate)
        // The MethodInfo is the Object of the MethodCallExpression.
        if (methodSelector.Body is UnaryExpression { Operand: MethodCallExpression methodCall }
            && methodCall.Object is ConstantExpression { Value: MethodInfo mi })
        {
            return mi.Name;
        }

        throw new ArgumentException(
            $"Expression '{methodSelector}' does not refer to a method. " +
            "Use a method group expression like: t => t.MethodName");
    }

    public static LambdaExpression? CreateIdentifierPredicate<T>(
        Expression<Func<T, object>> keySelector,
        string expectedId)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(expectedId);

        var member = ExtractMemberExpression(keySelector.Body);
        var keyType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        object expectedValue;
        try
        {
            expectedValue = ConvertIdentifier(expectedId, keyType);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException
            or ArgumentException or OverflowException)
        {
            return null;
        }

        Expression expected = Expression.Constant(expectedValue, keyType);
        if (member.Type != keyType)
        {
            expected = Expression.Convert(expected, member.Type);
        }

        return Expression.Lambda<Func<T, bool>>(
            Expression.Equal(member, expected),
            keySelector.Parameters);
    }

    private static object ConvertIdentifier(string expectedId, Type keyType)
    {
        if (keyType == typeof(string))
        {
            return expectedId;
        }

        if (keyType == typeof(Guid))
        {
            return Guid.Parse(expectedId);
        }

        if (keyType.IsEnum)
        {
            return Enum.Parse(keyType, expectedId, ignoreCase: false);
        }

        return Convert.ChangeType(expectedId, keyType, CultureInfo.InvariantCulture);
    }

    private static MemberExpression ExtractMemberExpression(Expression expression) =>
        expression switch
        {
            MemberExpression member => member,
            UnaryExpression { Operand: MemberExpression member } => member,
            _ => throw new ArgumentException(
                $"Expression '{expression}' does not refer to a member."),
        };
}
