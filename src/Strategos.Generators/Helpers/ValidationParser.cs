// -----------------------------------------------------------------------
// <copyright file="ValidationParser.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts validation information from ValidateState() invocations in fluent DSL.
/// </summary>
internal static class ValidationParser
{
    /// <summary>
    /// Extracts validation info from a ValidateState() invocation.
    /// </summary>
    /// <param name="invocation">The invocation expression to analyze.</param>
    /// <returns>
    /// A tuple containing the predicate expression string and error message,
    /// or (null, null) if the invocation is not a valid ValidateState call.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="invocation"/> is null.</exception>
    public static (string? Predicate, string? ErrorMessage) Extract(
        InvocationExpressionSyntax invocation)
    {
        ThrowHelper.ThrowIfNull(invocation, nameof(invocation));

        // Check if this is a ValidateState call
        if (!SyntaxHelper.IsMethodCall(invocation, "ValidateState"))
        {
            return (null, null);
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return (null, null);
        }

        string? predicate = null;
        string? errorMessage = null;

        // First argument: predicate lambda
        var predicateArg = arguments[0];
        if (predicateArg.Expression is LambdaExpressionSyntax lambdaExpression)
        {
            // Normalize the lambda parameter to the canonical name "state" so the
            // downstream emitter's "state." -> "State." rewrite works regardless of
            // the parameter name the author chose (e.g. s, st, x). Without this, a
            // predicate like `s => s.IsAuthorized` would be emitted verbatim and fail
            // to compile ("s" is not in scope in the generated saga handler).
            predicate = NormalizePredicateParameter(lambdaExpression);
        }

        // Second argument: error message
        var errorArg = arguments[1];
        if (errorArg.Expression is LiteralExpressionSyntax literalExpression
            && literalExpression.Kind() == SyntaxKind.StringLiteralExpression)
        {
            errorMessage = literalExpression.Token.ValueText;
        }

        return (predicate, errorMessage);
    }

    /// <summary>
    /// Returns the lambda body text with the lambda's parameter identifier rewritten to
    /// the canonical name <c>state</c>, so the emitter's <c>state.</c> -&gt; <c>State.</c>
    /// rewrite applies regardless of the author's chosen parameter name.
    /// </summary>
    /// <param name="lambda">The predicate lambda.</param>
    /// <returns>The normalized predicate body text.</returns>
    /// <remarks>
    /// Rewrites only identifier tokens whose text equals the parameter name, so a member
    /// access that happens to share the name (e.g. <c>x.x</c>) only rewrites the receiver,
    /// not the member. If the parameter is already named <c>state</c> the body is returned
    /// unchanged.
    /// </remarks>
    private static string NormalizePredicateParameter(LambdaExpressionSyntax lambda)
    {
        var parameterName = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parens =>
                parens.ParameterList.Parameters.Count == 1
                    ? parens.ParameterList.Parameters[0].Identifier.ValueText
                    : null,
            _ => null,
        };

        var body = lambda.Body;

        if (string.IsNullOrEmpty(parameterName) || parameterName == "state")
        {
            return body.ToString();
        }

        // Rewrite only standalone references to the parameter (IdentifierName nodes whose
        // text equals the parameter name), NOT the member-name side of a member access —
        // so `ctx.ctx` rewrites the receiver only, yielding `state.ctx`.
        var parameterReferences = body.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.ValueText == parameterName && !IsMemberName(id));

        var rewritten = body.ReplaceNodes(
            parameterReferences,
            (original, _) => SyntaxFactory.IdentifierName(
                SyntaxFactory.Identifier("state").WithTriviaFrom(original.Identifier)));

        return rewritten.ToString();
    }

    /// <summary>
    /// Returns true if the identifier is the member-name (right-hand) side of a member
    /// access — e.g. the second <c>ctx</c> in <c>ctx.ctx</c> — so it is NOT a reference to
    /// the lambda parameter and must not be rewritten.
    /// </summary>
    /// <param name="identifier">The identifier node to test.</param>
    /// <returns>True if the identifier is a member name; otherwise false.</returns>
    private static bool IsMemberName(IdentifierNameSyntax identifier) =>
        identifier.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == identifier;
}
