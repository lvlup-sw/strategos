// -----------------------------------------------------------------------
// <copyright file="InvocationChainWalker.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Walks invocation chains in fluent DSL workflow definitions.
/// Provides shared traversal logic for step and model extraction.
/// </summary>
internal static class InvocationChainWalker
{
    /// <summary>
    /// Step method names that define workflow phases.
    /// </summary>
    private static readonly HashSet<string> StepMethodNames = new(StringComparer.Ordinal)
    {
        "StartWith",
        "Then",
        "Finally",
    };

    /// <summary>
    /// Represents a node in the invocation chain with loop context.
    /// </summary>
    /// <param name="Invocation">The invocation expression syntax.</param>
    /// <param name="LoopPrefix">The loop prefix for nested step naming, or null if top-level.</param>
    /// <param name="IsStepMethod">True if this is a step method (StartWith, Then, Finally).</param>
    /// <param name="IsValidateStateMethod">True if this is a ValidateState call.</param>
    internal sealed record InvocationNode(
        InvocationExpressionSyntax Invocation,
        string? LoopPrefix,
        bool IsStepMethod,
        bool IsValidateStateMethod);

    /// <summary>
    /// Walks the invocation chain from the Finally call backwards,
    /// yielding all invocation nodes with their loop context.
    /// </summary>
    /// <param name="context">The parse context with pre-computed lookups.</param>
    /// <returns>An enumerable of invocation nodes in source order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IEnumerable<InvocationNode> WalkChain(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        if (context.FinallyInvocation is null)
        {
            return [];
        }

        // Collect nodes by walking backwards, then return in source order
        var nodes = new List<InvocationNode>();
        WalkChainRecursive(context.FinallyInvocation, nodes, currentLoopPrefix: null, context.CancellationToken);

        return nodes;
    }

    /// <summary>
    /// Tries to parse a RepeatUntil invocation and extract its loop name and body lambda.
    /// </summary>
    /// <param name="invocation">The invocation to check.</param>
    /// <param name="parentLoopPrefix">The parent loop prefix, if any.</param>
    /// <param name="effectivePrefix">The computed effective prefix for this loop.</param>
    /// <param name="bodyLambda">The body lambda expression, if extraction succeeds.</param>
    /// <returns>True if this is a valid RepeatUntil; otherwise, false.</returns>
    internal static bool TryParseRepeatUntil(
        InvocationExpressionSyntax invocation,
        string? parentLoopPrefix,
        out string effectivePrefix,
        out LambdaExpressionSyntax? bodyLambda)
    {
        effectivePrefix = string.Empty;
        bodyLambda = null;

        if (!SyntaxHelper.IsMethodCall(invocation, "RepeatUntil"))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 3)
        {
            return false;
        }

        // Extract loop name from argument index 1 (second argument is the loop name string)
        var loopNameArg = arguments[1];
        if (loopNameArg.Expression is not LiteralExpressionSyntax literal
            || literal.Kind() != SyntaxKind.StringLiteralExpression)
        {
            return false;
        }

        var loopName = literal.Token.ValueText;

        // Compute effective prefix: {ParentPrefix}_{LoopName} or just {LoopName}
        effectivePrefix = parentLoopPrefix is null
            ? loopName
            : $"{parentLoopPrefix}_{loopName}";

        // Extract body lambda from argument index 2
        var bodyArg = arguments[2];
        bodyLambda = bodyArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };

        return bodyLambda is not null;
    }

    /// <summary>
    /// Collects invocations from a lambda body, excluding those in nested lambdas.
    /// </summary>
    /// <param name="lambda">The lambda expression to search.</param>
    /// <returns>Invocations in source order.</returns>
    internal static IReadOnlyList<InvocationExpressionSyntax> CollectInvocationsInLambda(
        LambdaExpressionSyntax lambda)
    {
        // Get all invocations and nested lambdas
        var allInvocations = lambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        var nestedLambdas = lambda
            .DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Where(l => l != lambda)
            .ToList();

        // Filter to invocations owned by this lambda and order by completion position. Fluent
        // receiver calls share the same start token, but the inner (earlier) call ends first;
        // separate expression statements also end in authored order. Reversing the syntax walk
        // fixes the first shape while inverting the second.
        return allInvocations
            .Where(inv => !nestedLambdas.Any(nested => nested.Span.Contains(inv.Span)))
            .OrderBy(inv => inv.Span.End)
            .ThenBy(inv => inv.SpanStart)
            .ToList();
    }

    private static void WalkChainRecursive(
        InvocationExpressionSyntax invocation,
        List<InvocationNode> nodes,
        string? currentLoopPrefix,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if this is a RepeatUntil call - need to process its body
        if (TryParseRepeatUntil(invocation, currentLoopPrefix, out var effectivePrefix, out var bodyLambda))
        {
            // Process the loop body - collect direct invocations
            var bodyInvocations = CollectInvocationsInLambda(bodyLambda!);

            foreach (var bodyInv in bodyInvocations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check for nested RepeatUntil
                if (TryParseRepeatUntil(bodyInv, effectivePrefix, out var nestedPrefix, out var nestedLambda))
                {
                    // Recursively process nested loop
                    var nestedInvocations = CollectInvocationsInLambda(nestedLambda!);
                    foreach (var nestedInv in nestedInvocations)
                    {
                        var nestedNode = CreateNode(nestedInv, nestedPrefix);
                        nodes.Insert(0, nestedNode);
                    }
                }
                else
                {
                    // Regular invocation in loop body
                    var node = CreateNode(bodyInv, effectivePrefix);
                    nodes.Insert(0, node);
                }
            }
        }
        else
        {
            // Regular invocation - add to front (we're walking backwards)
            var node = CreateNode(invocation, currentLoopPrefix);
            nodes.Insert(0, node);
        }

        // Walk to the receiver (previous call in the chain)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && SyntaxHelper.StripTransparent(memberAccess.Expression) is InvocationExpressionSyntax previousInvocation)
        {
            WalkChainRecursive(previousInvocation, nodes, currentLoopPrefix, cancellationToken);
        }
    }

    private static InvocationNode CreateNode(InvocationExpressionSyntax invocation, string? loopPrefix)
    {
        var methodName = GetMethodName(invocation);
        var isStepMethod = methodName is not null && StepMethodNames.Contains(methodName);
        var isValidateStateMethod = methodName == "ValidateState";

        return new InvocationNode(invocation, loopPrefix, isStepMethod, isValidateStateMethod);
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        return SyntaxHelper.GetMethodName(memberAccess);
    }
}
