// -----------------------------------------------------------------------
// <copyright file="FluentDslParser.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;

namespace Strategos.Generators;

/// <summary>
/// Parses the fluent DSL workflow definition to extract step names.
/// This is a thin facade that delegates to specialized extractor classes.
/// </summary>
internal static class FluentDslParser
{
    /// <summary>
    /// Finds all step names defined in the workflow DSL within the given type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of step names (with loop prefixes) in the order they appear in the workflow.</returns>
    public static IReadOnlyList<string> ExtractStepNames(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        return [.. StepExtractor.ExtractStepInfos(context).Select(s => s.PhaseName)];
    }

    /// <summary>
    /// Extracts the state type name from the workflow definition (e.g., "OrderState" from Workflow&lt;OrderState&gt;).
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The state type name, or null if not found.</returns>
    public static string? ExtractStateTypeName(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        return StateTypeExtractor.Extract(context);
    }

    /// <summary>
    /// Extracts step information including loop context from the workflow DSL.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of step information in the order they appear in the workflow.</returns>
    public static IReadOnlyList<StepInfo> ExtractStepInfos(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        return StepExtractor.ExtractStepInfos(context);
    }

    /// <summary>
    /// Extracts raw step information WITHOUT deduplication for context-aware duplicate detection.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of step information with execution context, preserving duplicates.</returns>
    /// <remarks>
    /// Unlike <see cref="ExtractStepInfos"/>, this method does NOT deduplicate steps.
    /// Each step includes its execution context (Linear, ForkPath, or BranchPath).
    /// Use this for detecting duplicate steps that would cause runtime issues.
    /// </remarks>
    public static IReadOnlyList<StepInfo> ExtractRawStepInfos(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        return StepExtractor.ExtractRawStepInfos(context);
    }

    /// <summary>
    /// Extracts loop models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for condition ID generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of loop models in the order they appear in the workflow.</returns>
    public static IReadOnlyList<LoopModel> ExtractLoopModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));
        ThrowHelper.ThrowIfNullOrWhiteSpace(workflowName, nameof(workflowName));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, cancellationToken);
        return LoopExtractor.Extract(context);
    }

    /// <summary>
    /// Extracts step models with full type information for DI registration and handler generation.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of step models with step name, fully qualified type name, and loop context.</returns>
    public static IReadOnlyList<StepModel> ExtractStepModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        return StepExtractor.ExtractStepModels(context);
    }

    /// <summary>
    /// Extracts per-step context models from the workflow DSL so the
    /// <c>.WithContext(...)</c> declaration can be lowered into a generated
    /// <c>{Step}ContextAssembler</c> (DR-6).
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The (step name, context model) pairs for every step that declared
    /// <c>.WithContext(...)</c>. Steps without context are omitted.
    /// </returns>
    public static IReadOnlyList<(string StepName, ContextModel Context)> ExtractContextModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        return ContextModelExtractor.Extract(context);
    }

    /// <summary>
    /// Extracts branch models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for branch ID generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of branch models in the order they appear in the workflow.</returns>
    public static IReadOnlyList<BranchModel> ExtractBranchModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));
        ThrowHelper.ThrowIfNullOrWhiteSpace(workflowName, nameof(workflowName));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, cancellationToken);
        return BranchExtractor.Extract(context);
    }

    /// <summary>
    /// Extracts failure handler models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for handler ID generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of failure handler models in the order they appear in the workflow.</returns>
    public static IReadOnlyList<FailureHandlerModel> ExtractFailureHandlerModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));
        ThrowHelper.ThrowIfNullOrWhiteSpace(workflowName, nameof(workflowName));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, cancellationToken);
        return FailureHandlerExtractor.Extract(context);
    }

    /// <summary>
    /// Extracts approval models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for approval ID generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of approval models in the order they appear in the workflow.</returns>
    public static IReadOnlyList<ApprovalModel> ExtractApprovalModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));
        ThrowHelper.ThrowIfNullOrWhiteSpace(workflowName, nameof(workflowName));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, cancellationToken);
        return ApprovalExtractor.Extract(context);
    }

    /// <summary>
    /// Extracts fork models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for fork ID generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of fork models in the order they appear in the workflow.</returns>
    public static IReadOnlyList<ForkModel> ExtractForkModels(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));
        ThrowHelper.ThrowIfNullOrWhiteSpace(workflowName, nameof(workflowName));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, cancellationToken);
        return ForkExtractor.Extract(context);
    }

    /// <summary>
    /// Validates that all loops have non-empty bodies.
    /// Returns a list of loop names that have empty bodies.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of loop names that have empty bodies.</returns>
    public static IReadOnlyList<string> FindEmptyLoops(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);
        var emptyLoops = new List<string>();

        // Find all RepeatUntil invocations
        foreach (var invocation in context.AllInvocations)
        {
            if (!SyntaxHelper.IsMethodCall(invocation, "RepeatUntil"))
            {
                continue;
            }

            // RepeatUntil(condition, "name", body)
            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count < 3)
            {
                continue;
            }

            // Get loop name from second argument
            var loopNameArg = arguments[1];
            if (loopNameArg.Expression is not LiteralExpressionSyntax literal)
            {
                continue;
            }

            var loopName = literal.Token.ValueText;

            // Get body lambda from third argument
            var bodyArg = arguments[2];
            LambdaExpressionSyntax? bodyLambda = bodyArg.Expression switch
            {
                SimpleLambdaExpressionSyntax simple => simple,
                ParenthesizedLambdaExpressionSyntax parens => parens,
                _ => null
            };

            if (bodyLambda is null)
            {
                continue;
            }

            // Check if body has any step invocations (StartWith, Then, Finally)
            var bodyInvocations = InvocationChainWalker.CollectInvocationsInLambda(bodyLambda);
            var hasSteps = bodyInvocations.Any(inv =>
                SyntaxHelper.IsMethodCall(inv, "StartWith")
                || SyntaxHelper.IsMethodCall(inv, "Then")
                || SyntaxHelper.IsMethodCall(inv, "Finally"));

            if (!hasSteps)
            {
                emptyLoops.Add(loopName);
            }
        }

        return emptyLoops;
    }

    /// <summary>
    /// Validates that the workflow ends with Finally.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple of (HasFinally, HasSteps) where:
    /// - HasFinally is true if the workflow contains a Finally call
    /// - HasSteps is true if there are any step methods in the workflow.
    /// </returns>
    public static (bool HasFinally, bool HasSteps) ValidateEndsWith(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);

        // Check if there's a Finally invocation
        var hasFinally = context.FinallyInvocation is not null;

        // Check if there are any step method invocations (StartWith, Then, or Finally)
        var hasSteps = context.AllInvocations.Any(inv =>
            SyntaxHelper.IsMethodCall(inv, "StartWith")
            || SyntaxHelper.IsMethodCall(inv, "Then")
            || SyntaxHelper.IsMethodCall(inv, "Finally"));

        return (hasFinally, hasSteps);
    }

    /// <summary>
    /// Validates that the workflow starts with StartWith and returns the first method name found.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A tuple of (HasStartWith, FirstMethodName) where:
    /// - HasStartWith is true if the first step method is StartWith
    /// - FirstMethodName is the name of the first step method found, or null if none.
    /// </returns>
    public static (bool HasStartWith, string? FirstMethodName) ValidateStartsWith(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        var context = FluentDslParseContext.Create(typeDeclaration, semanticModel, null, cancellationToken);

        // Walk the chain and find the first step method
        var nodes = InvocationChainWalker.WalkChain(context);
        var firstStepNode = nodes.FirstOrDefault(n => n.IsStepMethod);

        if (firstStepNode is null)
        {
            return (false, null);
        }

        // Get the method name from the invocation's member access expression
        if (firstStepNode.Invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = SyntaxHelper.GetMethodName(memberAccess);
            return (methodName == "StartWith", methodName);
        }

        return (false, null);
    }
}
