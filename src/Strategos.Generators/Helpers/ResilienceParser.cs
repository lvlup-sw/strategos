// -----------------------------------------------------------------------
// <copyright file="ResilienceParser.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts per-step resilience configuration
/// (<see cref="RetryModel"/>, <see cref="TimeoutModel"/>) from the
/// <c>WithRetry</c>/<c>WithTimeout</c> invocations declared inside a step's
/// <c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(...).WithTimeout(...))</c> configure lambda.
/// </summary>
/// <remarks>
/// Mirrors <see cref="ValidationParser"/>: a stateless syntax-only extractor that
/// produces generator IR for one configuration concern. Symbol-resolving concerns
/// (e.g. <c>Compensate&lt;T&gt;</c>) live alongside the chain walk in
/// <see cref="StepExtractor"/>, where the semantic model is in scope.
/// </remarks>
internal static class ResilienceParser
{
    /// <summary>
    /// Extracts a <see cref="RetryModel"/> from a <c>WithRetry(...)</c> invocation.
    /// </summary>
    /// <param name="invocation">The <c>WithRetry</c> invocation to analyze.</param>
    /// <returns>
    /// The retry model carrying the parsed <c>maxAttempts</c> and optional
    /// <c>initialDelay</c>, or <c>null</c> if the invocation is not a recognizable
    /// <c>WithRetry</c> call.
    /// </returns>
    /// <remarks>
    /// Supports both the <c>WithRetry(int)</c> and <c>WithRetry(int, TimeSpan)</c> overloads.
    /// The remaining <see cref="RetryModel"/> delay-shaping members are left at their IR
    /// defaults: the DSL surface does not yet expose them, so only what was configured is carried.
    /// </remarks>
    public static RetryModel? ExtractRetry(InvocationExpressionSyntax invocation)
    {
        ThrowHelper.ThrowIfNull(invocation, nameof(invocation));

        if (!SyntaxHelper.IsMethodCall(invocation, "WithRetry"))
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        if (!TryGetIntLiteral(arguments[0].Expression, out var maxAttempts))
        {
            return null;
        }

        TimeSpan? initialDelay = null;
        if (arguments.Count >= 2 && TryGetTimeSpan(arguments[1].Expression, out var delay))
        {
            initialDelay = delay;
        }

        return new RetryModel(MaxAttempts: maxAttempts, InitialDelay: initialDelay);
    }

    /// <summary>
    /// Extracts a <see cref="TimeoutModel"/> from a <c>WithTimeout(TimeSpan)</c> invocation.
    /// </summary>
    /// <param name="invocation">The <c>WithTimeout</c> invocation to analyze.</param>
    /// <returns>
    /// The timeout model, or <c>null</c> if the invocation is not a recognizable
    /// <c>WithTimeout</c> call with a parseable <see cref="TimeSpan"/> argument.
    /// </returns>
    public static TimeoutModel? ExtractTimeout(InvocationExpressionSyntax invocation)
    {
        ThrowHelper.ThrowIfNull(invocation, nameof(invocation));

        if (!SyntaxHelper.IsMethodCall(invocation, "WithTimeout"))
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        if (!TryGetTimeSpan(arguments[0].Expression, out var timeout))
        {
            return null;
        }

        return new TimeoutModel(timeout);
    }

    private static bool TryGetIntLiteral(ExpressionSyntax expression, out int value)
    {
        value = 0;

        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.NumericLiteralExpression)
            && literal.Token.Value is int intValue)
        {
            value = intValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a syntactic <c>TimeSpan.From*(...)</c> factory call into a concrete
    /// <see cref="TimeSpan"/> for the IR.
    /// </summary>
    /// <remarks>
    /// Supported forms mirror the DSL examples: <c>TimeSpan.FromSeconds(n)</c>,
    /// <c>TimeSpan.FromMinutes(n)</c>, <c>TimeSpan.FromHours(n)</c>,
    /// <c>TimeSpan.FromMilliseconds(n)</c>, and <c>TimeSpan.FromDays(n)</c>, with a
    /// numeric-literal argument. Unrecognized expressions yield <c>false</c> so the
    /// caller can skip the unparseable value rather than emit an incorrect delay.
    /// </remarks>
    private static bool TryGetTimeSpan(ExpressionSyntax expression, out TimeSpan value)
    {
        value = default;

        if (expression is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Receiver must be the TimeSpan type (e.g. TimeSpan.FromSeconds(...)).
        if (memberAccess.Expression is not IdentifierNameSyntax receiver
            || !receiver.Identifier.ValueText.Equals("TimeSpan", StringComparison.Ordinal))
        {
            return false;
        }

        var factory = memberAccess.Name.Identifier.ValueText;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0 || !TryGetDoubleLiteral(arguments[0].Expression, out var amount))
        {
            return false;
        }

        switch (factory)
        {
            case "FromMilliseconds":
                value = TimeSpan.FromMilliseconds(amount);
                return true;
            case "FromSeconds":
                value = TimeSpan.FromSeconds(amount);
                return true;
            case "FromMinutes":
                value = TimeSpan.FromMinutes(amount);
                return true;
            case "FromHours":
                value = TimeSpan.FromHours(amount);
                return true;
            case "FromDays":
                value = TimeSpan.FromDays(amount);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetDoubleLiteral(ExpressionSyntax expression, out double value)
    {
        value = 0;

        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.NumericLiteralExpression))
        {
            switch (literal.Token.Value)
            {
                case double d:
                    value = d;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case float f:
                    value = f;
                    return true;
            }
        }

        return false;
    }
}
