// -----------------------------------------------------------------------
// <copyright file="SyntaxHelper.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Provides helper methods for common Roslyn syntax operations.
/// </summary>
internal static class SyntaxHelper
{
    /// <summary>
    /// Removes syntax wrappers that do not change the fluent receiver represented by an expression.
    /// </summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-transparent expression.</returns>
    public static ExpressionSyntax StripTransparent(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;
                case CheckedExpressionSyntax checkedExpression:
                    expression = checkedExpression.Expression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    /// <summary>
    /// Walks outward through syntax wrappers that do not change the fluent receiver.
    /// </summary>
    /// <param name="expression">The expression whose transparent parents should be included.</param>
    /// <returns>The outermost transparently wrapped expression.</returns>
    public static ExpressionSyntax IncludeTransparentParents(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesized
                    when parenthesized.Expression == expression:
                    expression = parenthesized;
                    continue;
                case CastExpressionSyntax cast
                    when cast.Expression == expression:
                    expression = cast;
                    continue;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.Operand == expression
                        && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix;
                    continue;
                case CheckedExpressionSyntax checkedExpression
                    when checkedExpression.Expression == expression:
                    expression = checkedExpression;
                    continue;
                default:
                    return expression;
            }
        }
    }

    /// <summary>
    /// Checks if the invocation is a method call with the specified name.
    /// </summary>
    /// <param name="invocation">The invocation expression to check.</param>
    /// <param name="methodName">The expected method name.</param>
    /// <returns>True if the invocation calls the specified method; otherwise, false.</returns>
    public static bool IsMethodCall(InvocationExpressionSyntax invocation, string methodName)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return GetMethodName(memberAccess) == methodName;
        }

        return false;
    }

    /// <summary>
    /// Gets the method name from a member access expression.
    /// </summary>
    /// <param name="memberAccess">The member access expression.</param>
    /// <returns>The method name, or empty string if not identifiable.</returns>
    public static string GetMethodName(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Name switch
        {
            GenericNameSyntax genericName => genericName.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Gets the type name from a type syntax node.
    /// </summary>
    /// <param name="typeSyntax">The type syntax node.</param>
    /// <returns>The simple type name.</returns>
    public static string GetTypeNameFromSyntax(TypeSyntax typeSyntax)
    {
        return typeSyntax switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => typeSyntax.ToString()
        };
    }

    /// <summary>
    /// Extracts the property path from a member access expression, excluding the parameter name.
    /// </summary>
    /// <param name="memberAccess">The member access expression (e.g., state.Claim.Type).</param>
    /// <returns>The property path excluding the parameter (e.g., "Claim.Type").</returns>
    public static string ExtractPropertyPath(MemberAccessExpressionSyntax memberAccess)
    {
        var parts = new List<string>();

        var current = memberAccess;
        while (current is not null)
        {
            parts.Insert(0, current.Name.Identifier.Text);
            current = current.Expression as MemberAccessExpressionSyntax;
        }

        // The first part is the parameter (state), skip it if it starts with lowercase
        // E.g., state.Type -> ["state", "Type"] -> "Type"
        // E.g., state.Claim.Type -> ["state", "Claim", "Type"] -> "Claim.Type"
        if (parts.Count > 0 && parts[0].Length > 0 && char.IsLower(parts[0][0]))
        {
            return string.Join(".", parts.Skip(1));
        }

        return string.Join(".", parts);
    }
}
