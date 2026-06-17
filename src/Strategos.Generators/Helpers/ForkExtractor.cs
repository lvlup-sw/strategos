// -----------------------------------------------------------------------
// <copyright file="ForkExtractor.cs" company="Levelup Software">
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
/// Extracts fork models from a workflow definition.
/// </summary>
/// <remarks>
/// <para>
/// This extractor parses Fork/Join constructs from the fluent DSL syntax.
/// It identifies parallel execution paths, their steps, failure handlers,
/// and the join step where paths converge.
/// </para>
/// <para>
/// The expected DSL pattern is:
/// <code>
/// .Fork(
///     path => path.Then&lt;Step1&gt;().Then&lt;Step2&gt;(),
///     path => path.Then&lt;Step3&gt;().OnFailure(f => f.Then&lt;Recovery&gt;()))
/// .Join&lt;SynthesizeStep&gt;()
/// </code>
/// </para>
/// </remarks>
internal static class ForkExtractor
{
    /// <summary>
    /// Extracts fork models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of fork models in the order they appear in the workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<ForkModel> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Find all Fork() method calls
        var forkInvocations = context.AllInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "Fork"))
            .ToList();

        if (forkInvocations.Count == 0)
        {
            return [];
        }

        var forks = new List<ForkModel>();
        var forkIndex = 0;

        foreach (var forkInvocation in forkInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Determine loop context for this fork invocation
            var loopPrefix = DetermineLoopPrefix(forkInvocation);

            if (TryParseFork(forkInvocation, context.SemanticModel, context.WorkflowName ?? string.Empty, forkIndex, context.AllInvocations, loopPrefix, out var forkModel, context.CancellationToken))
            {
                forks.Add(forkModel);
                forkIndex++;
            }
        }

        return forks;
    }

    private static bool TryParseFork(
        InvocationExpressionSyntax forkInvocation,
        SemanticModel semanticModel,
        string workflowName,
        int forkIndex,
        IReadOnlyList<InvocationExpressionSyntax> allInvocations,
        string? loopPrefix,
        out ForkModel forkModel,
        CancellationToken cancellationToken)
    {
        forkModel = default!;

        var arguments = forkInvocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            // Fork requires at least 2 paths
            return false;
        }

        // Find previous step (the receiver of the Fork call) and apply loop prefix
        var previousStepName = ApplyPrefix(FindPreviousStepName(forkInvocation, semanticModel), loopPrefix);

        // Find join step (the Join call after this Fork) and apply loop prefix
        var joinStepName = ApplyPrefix(FindJoinStepName(forkInvocation, allInvocations, semanticModel), loopPrefix);

        // Parse fork paths from arguments, passing loop prefix for step names
        var paths = new List<ForkPathModel>();
        for (var i = 0; i < arguments.Count; i++)
        {
            if (TryParseForkPath(arguments[i], i, semanticModel, loopPrefix, cancellationToken, out var pathModel))
            {
                paths.Add(pathModel);
            }
        }

        if (paths.Count < 2)
        {
            // Fork must have at least 2 paths
            return false;
        }

        var forkId = $"{workflowName}-Fork{forkIndex}";

        forkModel = new ForkModel(
            ForkId: forkId,
            PreviousStepName: previousStepName ?? string.Empty,
            Paths: paths,
            JoinStepName: joinStepName ?? string.Empty);

        return true;
    }

    /// <summary>
    /// Determines the loop prefix for a fork invocation by walking up the syntax tree
    /// to find parent RepeatUntil lambda bodies.
    /// </summary>
    /// <param name="forkInvocation">The fork invocation to check.</param>
    /// <returns>The combined loop prefix (e.g., "OuterLoop_InnerLoop"), or null if not inside a loop.</returns>
    private static string? DetermineLoopPrefix(InvocationExpressionSyntax forkInvocation)
    {
        // Walk up ancestors to find all containing RepeatUntil lambda bodies
        var loopNames = new List<string>();

        var current = forkInvocation.Parent;
        while (current is not null)
        {
            // Check if we're inside a lambda that's an argument to RepeatUntil
            if (current is LambdaExpressionSyntax lambda)
            {
                var loopName = FindContainingLoopName(lambda);
                if (loopName is not null)
                {
                    loopNames.Insert(0, loopName); // Insert at beginning for correct order
                }
            }

            current = current.Parent;
        }

        if (loopNames.Count == 0)
        {
            return null;
        }

        // Join with underscore: OuterLoop_InnerLoop
        return string.Join("_", loopNames);
    }

    /// <summary>
    /// Finds the loop name if this lambda is the body argument of a RepeatUntil call.
    /// </summary>
    /// <param name="lambda">The lambda to check.</param>
    /// <returns>The loop name, or null if not a RepeatUntil body.</returns>
    private static string? FindContainingLoopName(LambdaExpressionSyntax lambda)
    {
        // The lambda should be inside an argument
        if (lambda.Parent is not ArgumentSyntax arg)
        {
            return null;
        }

        // The argument should be inside an argument list
        if (arg.Parent is not ArgumentListSyntax argList)
        {
            return null;
        }

        // The argument list should be part of an invocation
        if (argList.Parent is not InvocationExpressionSyntax invocation)
        {
            return null;
        }

        // Check if this is a RepeatUntil call
        if (!SyntaxHelper.IsMethodCall(invocation, "RepeatUntil"))
        {
            return null;
        }

        // RepeatUntil has format: .RepeatUntil(condition, "LoopName", body, maxIterations?)
        // The loop name is the second argument (index 1)
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 3)
        {
            return null;
        }

        // Check if our lambda is the body argument (third argument, index 2)
        var bodyArgIndex = 2;
        if (arguments.Count <= bodyArgIndex || arguments[bodyArgIndex] != arg)
        {
            return null;
        }

        // Extract loop name from second argument
        var loopNameArg = arguments[1];
        if (loopNameArg.Expression is LiteralExpressionSyntax literal
            && literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Applies the loop prefix to a step name.
    /// </summary>
    /// <param name="stepName">The step name to prefix.</param>
    /// <param name="prefix">The loop prefix, or null if no prefix should be applied.</param>
    /// <returns>The prefixed step name, or the original name if no prefix.</returns>
    private static string? ApplyPrefix(string? stepName, string? prefix)
    {
        if (prefix is null || stepName is null)
        {
            return stepName;
        }

        return $"{prefix}_{stepName}";
    }

    private static string? FindPreviousStepName(InvocationExpressionSyntax forkInvocation, SemanticModel semanticModel)
    {
        // Walk backwards to find the previous step
        if (forkInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax previousInvocation)
            {
                if (StepExtractor.TryGetStepName(previousInvocation, semanticModel, out var stepName))
                {
                    return stepName;
                }

                // Recurse backwards if the previous call isn't a step
                return FindPreviousStepName(previousInvocation, semanticModel);
            }
        }

        return null;
    }

    private static string? FindJoinStepName(
        InvocationExpressionSyntax forkInvocation,
        IReadOnlyList<InvocationExpressionSyntax> allInvocations,
        SemanticModel semanticModel)
    {
        // Find the Join call that chains off this Fork
        foreach (var inv in allInvocations)
        {
            if (!SyntaxHelper.IsMethodCall(inv, "Join"))
            {
                continue;
            }

            // Check if this Join's receiver is our Fork
            if (inv.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Expression == forkInvocation ||
                    IsChainedAfter(forkInvocation, inv))
                {
                    // Extract step name from Join<TStep>()
                    if (inv.Expression is MemberAccessExpressionSyntax joinMemberAccess &&
                        joinMemberAccess.Name is GenericNameSyntax genericName)
                    {
                        var typeArg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
                        if (typeArg is not null)
                        {
                            return typeArg.ToString();
                        }
                    }
                }
            }
        }

        return null;
    }

    private static bool IsChainedAfter(InvocationExpressionSyntax forkInvocation, InvocationExpressionSyntax joinInvocation)
    {
        // Check if joinInvocation appears after forkInvocation in the chain
        var current = joinInvocation.Expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression == forkInvocation)
            {
                return true;
            }

            current = memberAccess.Expression switch
            {
                InvocationExpressionSyntax inv => inv.Expression,
                _ => null
            };
        }

        return false;
    }

    private static bool TryParseForkPath(
        ArgumentSyntax pathArg,
        int pathIndex,
        SemanticModel semanticModel,
        string? loopPrefix,
        CancellationToken cancellationToken,
        out ForkPathModel pathModel)
    {
        pathModel = default!;

        // Extract path builder lambda: path => path.Then<Step>()
        var pathLambda = pathArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
            _ => null
        };

        if (pathLambda is null)
        {
            return false;
        }

        // Extract steps and failure handler from the path body. Steps are carried as configured
        // StepModel records (mirroring the top-level/loop emitters' step model) so per-step
        // configuration such as ValidateState is preserved on the fork path, not just the names.
        var steps = new List<StepModel>();
        var hasFailureHandler = false;
        var isTerminalOnFailure = false;
        var failureHandlerStepNames = new List<string>();

        ParseForkPathBody(pathLambda, semanticModel, loopPrefix, steps, ref hasFailureHandler, ref isTerminalOnFailure, failureHandlerStepNames, cancellationToken);

        if (steps.Count == 0)
        {
            return false;
        }

        pathModel = new ForkPathModel(
            PathIndex: pathIndex,
            Steps: steps,
            HasFailureHandler: hasFailureHandler,
            IsTerminalOnFailure: isTerminalOnFailure,
            FailureHandlerStepNames: failureHandlerStepNames.Count > 0 ? failureHandlerStepNames : null);

        return true;
    }

    private static void ParseForkPathBody(
        LambdaExpressionSyntax pathLambda,
        SemanticModel semanticModel,
        string? loopPrefix,
        List<StepModel> steps,
        ref bool hasFailureHandler,
        ref bool isTerminalOnFailure,
        List<string> failureHandlerStepNames,
        CancellationToken cancellationToken)
    {
        // Find all invocations in the path body
        // Reverse to get source order (outermost call first in chain)
        var allInvocations = pathLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Reverse()
            .ToList();

        // Find nested lambdas (for OnFailure handlers) to exclude their invocations from main path
        var failureLambdas = new List<LambdaExpressionSyntax>();
        var onFailureInvocations = allInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "OnFailure"))
            .ToList();

        foreach (var onFailure in onFailureInvocations)
        {
            hasFailureHandler = true;
            var failureArg = onFailure.ArgumentList.Arguments.FirstOrDefault();
            if (failureArg?.Expression is SimpleLambdaExpressionSyntax simpleLambda)
            {
                failureLambdas.Add(simpleLambda);
                ParseFailureHandlerBody(simpleLambda, semanticModel, ref isTerminalOnFailure, failureHandlerStepNames, cancellationToken);
            }
            else if (failureArg?.Expression is ParenthesizedLambdaExpressionSyntax parensLambda)
            {
                failureLambdas.Add(parensLambda);
                ParseFailureHandlerBody(parensLambda, semanticModel, ref isTerminalOnFailure, failureHandlerStepNames, cancellationToken);
            }
        }

        // Process Then calls in the main path (excluding those in failure handlers)
        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip invocations inside failure handler lambdas
            if (failureLambdas.Any(lambda => lambda.Span.Contains(inv.Span)))
            {
                continue;
            }

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                // Build a configured StepModel for this fork-path step. The loop prefix is threaded
                // into the StepModel as its LoopName, so StepModel.PhaseName yields the same prefixed
                // phase name the emitters previously consumed, while per-step configuration
                // (e.g. ValidateState) is preserved on the step rather than discarded.
                if (StepExtractor.TryBuildConfiguredForkPathStepModel(inv, semanticModel, loopPrefix, out var stepModel))
                {
                    // Avoid duplicates by phase name (can happen with nested calls)
                    if (!steps.Any(s => string.Equals(s.PhaseName, stepModel.PhaseName, StringComparison.Ordinal)))
                    {
                        steps.Add(stepModel);
                    }
                }
            }
        }
    }

    private static void ParseFailureHandlerBody(
        LambdaExpressionSyntax failureLambda,
        SemanticModel semanticModel,
        ref bool isTerminal,
        List<string> failureStepNames,
        CancellationToken cancellationToken)
    {
        var allInvocations = failureLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                if (StepExtractor.TryGetStepName(inv, semanticModel, out var stepName))
                {
                    if (!failureStepNames.Contains(stepName))
                    {
                        failureStepNames.Add(stepName);
                    }
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Complete"))
            {
                isTerminal = true;
            }
        }
    }
}
