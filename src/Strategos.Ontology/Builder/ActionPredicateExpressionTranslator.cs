using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal static class ActionPredicateExpressionTranslator
{
    public static ActionPredicate Translate<T>(Expression<Func<T, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression.Parameters.Count != 1)
        {
            throw Unsupported(expression.Body, "A predicate must have exactly one subject parameter.");
        }

        if (expression.Body is ConstantExpression { Value: true })
        {
            throw Unsupported(
                expression.Body,
                "Use ActionPredicate.True to declare an explicit wildcard; '_ => true' is not accepted.");
        }

        return TranslateNode(expression.Body, expression.Parameters[0]);
    }

    private static ActionPredicate TranslateNode(Expression expression, ParameterExpression parameter)
    {
        return expression switch
        {
            BinaryExpression binary when binary.NodeType is ExpressionType.AndAlso or ExpressionType.And =>
                ActionPredicate.All(TranslateNode(binary.Left, parameter), TranslateNode(binary.Right, parameter)),
            BinaryExpression binary when binary.NodeType is ExpressionType.OrElse or ExpressionType.Or =>
                ActionPredicate.Any(TranslateNode(binary.Left, parameter), TranslateNode(binary.Right, parameter)),
            UnaryExpression { NodeType: ExpressionType.Not } not =>
                ActionPredicate.Not(TranslateNode(not.Operand, parameter)),
            BinaryExpression binary when IsComparison(binary.NodeType) => TranslateComparison(binary, parameter),
            MemberExpression member when TryGetDirectProperty(member, parameter, out var property)
                && property is { ScalarKind: PredicateScalarKind.Boolean } =>
                ActionPredicate.Property(property, PredicateComparisonOperator.Equal, PredicateLiteral.Boolean(true)),
            ConstantExpression { Value: true } => ActionPredicate.True,
            ConstantExpression { Value: false } => ActionPredicate.False,
            _ => throw Unsupported(
                expression,
                "Supported forms are direct property-to-literal comparisons and AND/OR/NOT over those comparisons."),
        };
    }

    private static ActionPredicate TranslateComparison(BinaryExpression expression, ParameterExpression parameter)
    {
        if (expression.Method is not null
            && expression.Method.DeclaringType != typeof(string)
            && expression.Method.DeclaringType != typeof(decimal)
            && expression.Method.DeclaringType != typeof(BigInteger)
            && expression.Method.DeclaringType != typeof(Guid))
        {
            throw Unsupported(expression, "User-defined comparison operators are not supported.");
        }

        var leftIsProperty = TryGetDirectProperty(
            expression.Left,
            parameter,
            out var leftProperty,
            out var leftType);
        var rightIsProperty = TryGetDirectProperty(
            expression.Right,
            parameter,
            out var rightProperty,
            out var rightType);

        if (leftIsProperty == rightIsProperty)
        {
            throw Unsupported(expression, "Exactly one side of a comparison must be a direct subject property.");
        }

        var property = leftIsProperty ? leftProperty! : rightProperty!;
        var propertyType = leftIsProperty ? leftType! : rightType!;
        var literalExpression = leftIsProperty ? expression.Right : expression.Left;
        if (!TryGetLiteralValue(literalExpression, out var value))
        {
            throw Unsupported(
                literalExpression,
                "The comparison value must be a literal. Captures, member reads, calls, and computed values are not supported.");
        }

        var comparison = MapComparison(expression.NodeType);
        if (!leftIsProperty)
        {
            comparison = Reverse(comparison);
        }

        return ActionPredicate.Property(property, comparison, CreateLiteral(propertyType, property, value));
    }

    private static bool TryGetDirectProperty(
        Expression expression,
        ParameterExpression parameter,
        out PredicatePropertyReference? property) =>
        TryGetDirectProperty(expression, parameter, out property, out _);

    private static bool TryGetDirectProperty(
        Expression expression,
        ParameterExpression parameter,
        out PredicatePropertyReference? property,
        out Type? propertyType)
    {
        if (expression is UnaryExpression conversion
            && conversion.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && conversion.Method is null
            && TryGetDirectProperty(
                conversion.Operand,
                parameter,
                out property,
                out propertyType)
            && IsRepresentationPreservingPropertyConversion(
                propertyType!,
                conversion.Type,
                conversion.NodeType == ExpressionType.ConvertChecked))
        {
            return true;
        }

        if (expression is not MemberExpression { Expression: ParameterExpression owner } member
            || owner != parameter
            || member.Member is not PropertyInfo)
        {
            property = null;
            propertyType = null;
            return false;
        }

        propertyType = member.Type;
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var nullable = IsNullable((PropertyInfo)member.Member, propertyType);
        var scalarKind = ScalarKind(underlying);
        property = new PredicatePropertyReference(
            member.Member.Name,
            scalarKind,
            nullable,
            scalarKind == PredicateScalarKind.Enum ? underlying.Name : null);
        return true;
    }

    private static bool IsEnumUnderlyingConversion(Type source, Type target)
    {
        var sourceNullable = Nullable.GetUnderlyingType(source);
        var targetNullable = Nullable.GetUnderlyingType(target);
        var sourceType = sourceNullable ?? source;
        var targetType = targetNullable ?? target;
        var enumComparisonType = sourceType.IsEnum
            ? Enum.GetUnderlyingType(sourceType)
            : null;
        if (enumComparisonType == typeof(sbyte)
            || enumComparisonType == typeof(byte)
            || enumComparisonType == typeof(short)
            || enumComparisonType == typeof(ushort))
        {
            enumComparisonType = typeof(int);
        }

        return IsCompatibleNullableConversion(sourceNullable, targetNullable)
            && sourceType.IsEnum
            && enumComparisonType == targetType;
    }

    private static bool IsRepresentationPreservingPropertyConversion(
        Type source,
        Type target,
        bool isChecked) =>
        IsValuePreservingIntegralConversion(source, target)
        || (!isChecked
            && (IsNullableLift(source, target)
                || IsEnumUnderlyingConversion(source, target)));

    private static bool IsNullable(PropertyInfo property, Type propertyType)
    {
        if (Nullable.GetUnderlyingType(propertyType) is not null)
        {
            return true;
        }

        if (propertyType.IsValueType)
        {
            return false;
        }

        return new NullabilityInfoContext().Create(property).ReadState != NullabilityState.NotNull;
    }

    private static PredicateScalarKind ScalarKind(Type type)
    {
        if (type == typeof(bool))
        {
            return PredicateScalarKind.Boolean;
        }

        if (type == typeof(decimal))
        {
            return PredicateScalarKind.Decimal;
        }

        if (type == typeof(string))
        {
            return PredicateScalarKind.String;
        }

        if (type.IsEnum)
        {
            return PredicateScalarKind.Enum;
        }

        if (type == typeof(Guid))
        {
            return PredicateScalarKind.Symbol;
        }

        if (type == typeof(BigInteger)
            || type == typeof(sbyte)
            || type == typeof(byte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong))
        {
            return PredicateScalarKind.Integer;
        }

        throw new ArgumentException(
            $"Property type '{type}' is outside the action-predicate scalar grammar. "
            + "Supported types are bool, integral numbers, decimal, string, enum, Guid, and nullable forms.");
    }

    private static PredicateLiteral CreateLiteral(
        Type declaredPropertyType,
        PredicatePropertyReference property,
        object? value)
    {
        if (value is null)
        {
            return PredicateLiteral.Null;
        }

        var propertyType = Nullable.GetUnderlyingType(declaredPropertyType) ?? declaredPropertyType;
        try
        {
            return property.ScalarKind switch
            {
                PredicateScalarKind.Boolean => PredicateLiteral.Boolean(Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture)),
                PredicateScalarKind.Integer => PredicateLiteral.Integer(ToBigInteger(value)),
                PredicateScalarKind.Decimal => PredicateLiteral.Decimal(ToPredicateDecimal(value)),
                PredicateScalarKind.String when value is string text => PredicateLiteral.String(text),
                PredicateScalarKind.Enum => EnumLiteral(propertyType, value),
                PredicateScalarKind.Symbol when propertyType == typeof(Guid) =>
                    PredicateLiteral.Symbol(ToGuid(value).ToString("D")),
                _ => throw new InvalidCastException(),
            };
        }
        catch (Exception exception) when (exception is FormatException
            or InvalidCastException
            or OverflowException
            or ArgumentException)
        {
            throw new ArgumentException(
                $"Literal '{value}' cannot be represented in property '{property.Name}'s {property.ScalarKind} domain.",
                exception);
        }
    }

    private static PredicateLiteral EnumLiteral(Type enumType, object value)
    {
        var enumValue = value.GetType() == enumType ? value : Enum.ToObject(enumType, value);
        var members = Enum.GetNames(enumType)
            .Where(name => Equals(Enum.Parse(enumType, name), enumValue))
            .Take(2)
            .ToArray();
        if (members.Length != 1)
        {
            throw new ArgumentException(
                "Enum predicates require an unaliased named value.",
                nameof(value));
        }

        return PredicateLiteral.Enum(enumType.Name, members[0]);
    }

    private static BigInteger ToBigInteger(object value) => value switch
    {
        BigInteger integer => integer,
        sbyte integer => new BigInteger(integer),
        byte integer => new BigInteger(integer),
        short integer => new BigInteger(integer),
        ushort integer => new BigInteger(integer),
        int integer => new BigInteger(integer),
        uint integer => new BigInteger(integer),
        long integer => new BigInteger(integer),
        ulong integer => new BigInteger(integer),
        _ => throw new InvalidCastException(),
    };

    private static PredicateDecimal ToPredicateDecimal(object value) => value switch
    {
        decimal decimalValue => PredicateDecimal.FromDecimal(decimalValue),
        BigInteger integer => new PredicateDecimal(integer, 0),
        sbyte integer => new PredicateDecimal(new BigInteger(integer), 0),
        byte integer => new PredicateDecimal(new BigInteger(integer), 0),
        short integer => new PredicateDecimal(new BigInteger(integer), 0),
        ushort integer => new PredicateDecimal(new BigInteger(integer), 0),
        int integer => new PredicateDecimal(new BigInteger(integer), 0),
        uint integer => new PredicateDecimal(new BigInteger(integer), 0),
        long integer => new PredicateDecimal(new BigInteger(integer), 0),
        ulong integer => new PredicateDecimal(new BigInteger(integer), 0),
        _ => throw new InvalidCastException(),
    };

    private static Guid ToGuid(object value) => value switch
    {
        Guid guid => guid,
        string text => Guid.Parse(text),
        _ => throw new InvalidCastException(),
    };

    private static bool TryGetLiteralValue(Expression expression, out object? value)
    {
        while (expression is UnaryExpression convert &&
               IsRepresentationPreservingLiteralConversion(convert))
        {
            expression = convert.Operand;
        }

        if (expression is ConstantExpression constant)
        {
            value = constant.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool IsRepresentationPreservingLiteralConversion(UnaryExpression conversion)
    {
        var source = conversion.Operand.Type;
        var target = conversion.Type;
        if (conversion.NodeType == ExpressionType.ConvertChecked)
        {
            return conversion.Method is null
                && IsValuePreservingIntegralConversion(source, target);
        }

        if (conversion.NodeType != ExpressionType.Convert)
        {
            return false;
        }

        if (conversion.Method is null)
        {
            return IsNullableLift(source, target)
                || IsValuePreservingIntegralConversion(source, target)
                || IsEnumUnderlyingConversion(source, target);
        }

        return conversion.Method.Name == "op_Implicit"
            && ((conversion.Method.DeclaringType == typeof(decimal)
                    && IsExactIntegralConversion(source, target, typeof(decimal)))
                || (conversion.Method.DeclaringType == typeof(BigInteger)
                    && IsExactIntegralConversion(source, target, typeof(BigInteger))));
    }

    private static bool IsNullableLift(Type source, Type target)
    {
        var sourceNullable = Nullable.GetUnderlyingType(source);
        var targetNullable = Nullable.GetUnderlyingType(target);
        return sourceNullable is null
            && targetNullable is not null
            && source == targetNullable;
    }

    private static bool IsExactIntegralConversion(Type source, Type target, Type expectedTarget)
    {
        var sourceNullable = Nullable.GetUnderlyingType(source);
        var targetNullable = Nullable.GetUnderlyingType(target);
        return IsCompatibleNullableConversion(sourceNullable, targetNullable)
            && IsIntegralType(sourceNullable ?? source)
            && (targetNullable ?? target) == expectedTarget;
    }

    private static bool IsValuePreservingIntegralConversion(Type source, Type target)
    {
        var sourceNullable = Nullable.GetUnderlyingType(source);
        var targetNullable = Nullable.GetUnderlyingType(target);
        if (!IsCompatibleNullableConversion(sourceNullable, targetNullable))
        {
            return false;
        }

        var sourceType = sourceNullable ?? source;
        var targetType = targetNullable ?? target;
        if (!IsIntegralType(sourceType) || !IsIntegralType(targetType))
        {
            return false;
        }

        var targetCode = Type.GetTypeCode(targetType);
        return Type.GetTypeCode(sourceType) switch
        {
            TypeCode.SByte => targetCode is TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64,
            TypeCode.Byte => targetCode is
                TypeCode.Byte or
                TypeCode.Int16 or
                TypeCode.UInt16 or
                TypeCode.Int32 or
                TypeCode.UInt32 or
                TypeCode.Int64 or
                TypeCode.UInt64,
            TypeCode.Int16 => targetCode is TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64,
            TypeCode.UInt16 => targetCode is
                TypeCode.UInt16 or
                TypeCode.Int32 or
                TypeCode.UInt32 or
                TypeCode.Int64 or
                TypeCode.UInt64,
            TypeCode.Int32 => targetCode is TypeCode.Int32 or TypeCode.Int64,
            TypeCode.UInt32 => targetCode is TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64,
            TypeCode.Int64 => targetCode == TypeCode.Int64,
            TypeCode.UInt64 => targetCode == TypeCode.UInt64,
            _ => false,
        };
    }

    private static bool IsCompatibleNullableConversion(Type? sourceNullable, Type? targetNullable) =>
        (sourceNullable is null) == (targetNullable is null)
        || sourceNullable is null && targetNullable is not null;

    private static bool IsIntegralType(Type type) =>
        type == typeof(sbyte)
        || type == typeof(byte)
        || type == typeof(short)
        || type == typeof(ushort)
        || type == typeof(int)
        || type == typeof(uint)
        || type == typeof(long)
        || type == typeof(ulong);

    private static bool IsComparison(ExpressionType nodeType) => nodeType is
        ExpressionType.Equal or
        ExpressionType.NotEqual or
        ExpressionType.LessThan or
        ExpressionType.LessThanOrEqual or
        ExpressionType.GreaterThan or
        ExpressionType.GreaterThanOrEqual;

    private static PredicateComparisonOperator MapComparison(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => PredicateComparisonOperator.Equal,
        ExpressionType.NotEqual => PredicateComparisonOperator.NotEqual,
        ExpressionType.LessThan => PredicateComparisonOperator.LessThan,
        ExpressionType.LessThanOrEqual => PredicateComparisonOperator.LessThanOrEqual,
        ExpressionType.GreaterThan => PredicateComparisonOperator.GreaterThan,
        ExpressionType.GreaterThanOrEqual => PredicateComparisonOperator.GreaterThanOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(nodeType)),
    };

    private static PredicateComparisonOperator Reverse(PredicateComparisonOperator comparison) => comparison switch
    {
        PredicateComparisonOperator.Equal => PredicateComparisonOperator.Equal,
        PredicateComparisonOperator.NotEqual => PredicateComparisonOperator.NotEqual,
        PredicateComparisonOperator.LessThan => PredicateComparisonOperator.GreaterThan,
        PredicateComparisonOperator.LessThanOrEqual => PredicateComparisonOperator.GreaterThanOrEqual,
        PredicateComparisonOperator.GreaterThan => PredicateComparisonOperator.LessThan,
        PredicateComparisonOperator.GreaterThanOrEqual => PredicateComparisonOperator.LessThanOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
    };

    private static ArgumentException Unsupported(Expression expression, string explanation) =>
        new(
            $"Expression '{expression}' uses unsupported action-predicate node '{expression.NodeType}'. {explanation}",
            nameof(expression));
}
