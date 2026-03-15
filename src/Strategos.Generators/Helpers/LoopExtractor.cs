// -----------------------------------------------------------------------
// <copyright file="LoopExtractor.cs" company="Levelup Software">
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
/// Extracts loop models from a workflow definition.
/// </summary>
internal static class LoopExtractor
{
    /// <summary>
    /// Extracts loop models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of loop models in the order they appear in the workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<LoopModel> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.FinallyInvocation is null)
        {
            return [];
        }

        // Walk the invocation chain and collect loop models
        var loops = new List<LoopModel>();
        var allSteps = new List<(string PhaseName, int Order)>();

        // First pass: collect all steps with their order
        var stepInfos = StepExtractor.ExtractStepInfos(context);
        for (var i = 0; i < stepInfos.Count; i++)
        {
            allSteps.Add((stepInfos[i].PhaseName, i));
        }

        WalkInvocationChainForLoopModels(context.FinallyInvocation, loops, context.SemanticModel, context.WorkflowName ?? string.Empty, null, allSteps, context.CancellationToken);

        return loops;
    }

    private static void WalkInvocationChainForLoopModels(
        InvocationExpressionSyntax invocation,
        List<LoopModel> loops,
        SemanticModel semanticModel,
        string workflowName,
        string? parentLoopName,
        List<(string PhaseName, int Order)> allSteps,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if this is a RepeatUntil call
        if (TryParseRepeatUntilForLoopModel(invocation, semanticModel, workflowName, parentLoopName, allSteps, out var loopModel, out var nestedPrefix, cancellationToken))
        {
            loops.Insert(0, loopModel);

            // Parse nested loops within this loop's body
            var bodyArg = invocation.ArgumentList.Arguments[2];
            var bodyLambda = bodyArg.Expression switch
            {
                SimpleLambdaExpressionSyntax simple => simple,
                ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
                _ => null
            };

            if (bodyLambda is not null)
            {
                ParseLoopBodyForLoopModels(bodyLambda, loops, semanticModel, workflowName, loopModel.LoopName, allSteps, cancellationToken);
            }
        }

        // Walk to the receiver (previous call in the chain)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax previousInvocation)
            {
                WalkInvocationChainForLoopModels(previousInvocation, loops, semanticModel, workflowName, parentLoopName, allSteps, cancellationToken);
            }
        }
    }

    private static void ParseLoopBodyForLoopModels(
        LambdaExpressionSyntax bodyLambda,
        List<LoopModel> loops,
        SemanticModel semanticModel,
        string workflowName,
        string currentLoopName,
        List<(string PhaseName, int Order)> allSteps,
        CancellationToken cancellationToken)
    {
        // Reverse to process in source order
        var allInvocations = bodyLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Reverse()
            .ToList();

        var nestedLambdas = bodyLambda
            .DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Where(l => l != bodyLambda)
            .ToList();

        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isInNestedLambda = nestedLambdas.Any(lambda => lambda.Span.Contains(inv.Span));

            if (isInNestedLambda)
            {
                continue;
            }

            if (SyntaxHelper.IsMethodCall(inv, "RepeatUntil"))
            {
                if (TryParseRepeatUntilForLoopModel(inv, semanticModel, workflowName, currentLoopName, allSteps, out var nestedLoopModel, out _, cancellationToken))
                {
                    loops.Add(nestedLoopModel);

                    // Recursively process nested loop bodies
                    var nestedBodyArg = inv.ArgumentList.Arguments[2];
                    var nestedBodyLambda = nestedBodyArg.Expression switch
                    {
                        SimpleLambdaExpressionSyntax simple => simple,
                        ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
                        _ => null
                    };

                    if (nestedBodyLambda is not null)
                    {
                        ParseLoopBodyForLoopModels(nestedBodyLambda, loops, semanticModel, workflowName, nestedLoopModel.LoopName, allSteps, cancellationToken);
                    }
                }
            }
        }
    }

    private static bool TryParseRepeatUntilForLoopModel(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string workflowName,
        string? parentLoopName,
        List<(string PhaseName, int Order)> allSteps,
        out LoopModel loopModel,
        out string effectivePrefix,
        CancellationToken cancellationToken)
    {
        loopModel = default!;
        effectivePrefix = string.Empty;

        if (!SyntaxHelper.IsMethodCall(invocation, "RepeatUntil"))
        {
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 3)
        {
            return false;
        }

        // Extract loop name from argument index 1
        if (!TryExtractLoopName(arguments[1], out var loopName))
        {
            return false;
        }

        // Compute effective prefix
        var computedPrefix = parentLoopName is null
            ? loopName
            : $"{parentLoopName}_{loopName}";
        effectivePrefix = computedPrefix;

        // Extract max iterations from argument index 3 (if present), default to 10
        var maxIterations = ExtractMaxIterations(arguments, semanticModel);

        // Find loop body steps that are direct children of this loop
        var bodySteps = FindDirectBodySteps(computedPrefix, allSteps);

        if (bodySteps.Count == 0)
        {
            return false;
        }

        // Find continuation step (first step after the loop body that is not a child of this loop)
        var continuationStepName = FindContinuationStepName(computedPrefix, bodySteps, allSteps);

        // Build condition ID
        var conditionId = $"{workflowName}-{loopName}";

        // Check if a Branch immediately follows this RepeatUntil
        var branchOnExit = ExtractBranchOnExit(invocation, semanticModel, workflowName, cancellationToken);

        loopModel = new LoopModel(
            LoopName: loopName,
            ConditionId: conditionId,
            MaxIterations: maxIterations,
            FirstBodyStepName: bodySteps.First().PhaseName,
            LastBodyStepName: bodySteps.Last().PhaseName,
            ContinuationStepName: continuationStepName,
            ParentLoopName: parentLoopName,
            BranchOnExitId: branchOnExit?.BranchId,
            BranchOnExit: branchOnExit);

        return true;
    }

    /// <summary>
    /// Tries to extract the loop name from a RepeatUntil's second argument (index 1).
    /// </summary>
    private static bool TryExtractLoopName(ArgumentSyntax loopNameArg, out string loopName)
    {
        loopName = string.Empty;

        if (loopNameArg.Expression is LiteralExpressionSyntax literal &&
            literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            loopName = literal.Token.ValueText;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the max iterations value from a RepeatUntil's fourth argument (index 3).
    /// Returns the default of 10 if not specified or not parseable.
    /// </summary>
    private static int ExtractMaxIterations(SeparatedSyntaxList<ArgumentSyntax> arguments, SemanticModel semanticModel)
    {
        const int defaultMaxIterations = 10;

        if (arguments.Count <= 3)
        {
            return defaultMaxIterations;
        }

        var maxIterArg = arguments[3];
        if (maxIterArg.Expression is LiteralExpressionSyntax maxLiteral &&
            maxLiteral.Kind() == SyntaxKind.NumericLiteralExpression)
        {
            if (int.TryParse(maxLiteral.Token.ValueText, out var parsed))
            {
                return parsed;
            }
        }
        else
        {
            // Try to resolve constant references via semantic model
            var constantValue = semanticModel.GetConstantValue(maxIterArg.Expression);
            if (constantValue.HasValue && constantValue.Value is int intValue)
            {
                return intValue;
            }
        }

        return defaultMaxIterations;
    }

    /// <summary>
    /// Finds the direct body steps of a loop (steps prefixed with the loop name but not nested deeper).
    /// </summary>
    private static List<(string PhaseName, int Order)> FindDirectBodySteps(
        string computedPrefix,
        List<(string PhaseName, int Order)> allSteps)
    {
        var prefixWithUnderscore = $"{computedPrefix}_";

        return allSteps
            .Where(s =>
            {
                if (!s.PhaseName.StartsWith(prefixWithUnderscore))
                {
                    return false;
                }

                var remainder = s.PhaseName.Substring(prefixWithUnderscore.Length);
                return !remainder.Contains('_');
            })
            .OrderBy(s => s.Order)
            .ToList();
    }

    /// <summary>
    /// Finds the continuation step name (the first step after the loop body that is not a child of this loop).
    /// </summary>
    private static string? FindContinuationStepName(
        string computedPrefix,
        List<(string PhaseName, int Order)> bodySteps,
        List<(string PhaseName, int Order)> allSteps)
    {
        var lastBodyOrder = bodySteps.Max(s => s.Order);
        var continuationStep = allSteps
            .Where(s => s.Order > lastBodyOrder && !s.PhaseName.StartsWith($"{computedPrefix}_"))
            .OrderBy(s => s.Order)
            .FirstOrDefault();

        return continuationStep.PhaseName;
    }

    /// <summary>
    /// Extracts the full BranchModel for a Branch that immediately follows the RepeatUntil.
    /// </summary>
    /// <param name="repeatUntilInvocation">The RepeatUntil invocation to check.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for constructing the branch ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The full BranchModel if a Branch follows the loop, or null otherwise.</returns>
    private static BranchModel? ExtractBranchOnExit(
        InvocationExpressionSyntax repeatUntilInvocation,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        // Walk up the AST to find if this RepeatUntil is used as the receiver of a Branch call
        var parent = repeatUntilInvocation.Parent;
        while (parent is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression == repeatUntilInvocation)
            {
                // Found a call that chains off this RepeatUntil
                if (memberAccess.Parent is InvocationExpressionSyntax nextInvocation)
                {
                    if (SyntaxHelper.IsMethodCall(nextInvocation, "Branch"))
                    {
                        // Found the Branch - extract the full model
                        return ExtractFullBranchModel(nextInvocation, semanticModel, workflowName, cancellationToken);
                    }
                }
            }

            parent = parent.Parent;
        }

        return null;
    }

    /// <summary>
    /// Extracts the full BranchModel from a Branch invocation.
    /// </summary>
    private static BranchModel? ExtractFullBranchModel(
        InvocationExpressionSyntax branchInvocation,
        SemanticModel semanticModel,
        string workflowName,
        CancellationToken cancellationToken)
    {
        var arguments = branchInvocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return null;
        }

        // Extract discriminator info
        var discriminatorArg = arguments[0];
        if (!TryExtractDiscriminatorInfo(discriminatorArg, semanticModel, out var propertyPath, out var typeName, out var isEnum, out var isMethod))
        {
            return null;
        }

        // Build branch ID
        var branchIndex = FindBranchIndexInWorkflow(branchInvocation);
        var branchId = $"{workflowName}-Branch{branchIndex}-{propertyPath}";

        // Parse branch cases from remaining arguments
        var cases = new List<BranchCaseModel>();
        for (var i = 1; i < arguments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryParseBranchCase(arguments[i], semanticModel, propertyPath, cancellationToken, out var caseModel))
            {
                cases.Add(caseModel);
            }
        }

        if (cases.Count == 0)
        {
            return null;
        }

        // Find rejoin step (step after this branch in the chain)
        var rejoinStepName = FindRejoinStepName(branchInvocation, semanticModel);

        return new BranchModel(
            BranchId: branchId,
            PreviousStepName: string.Empty, // Not needed for loop exit branches
            DiscriminatorPropertyPath: propertyPath,
            DiscriminatorTypeName: typeName,
            IsEnumDiscriminator: isEnum,
            IsMethodDiscriminator: isMethod,
            Cases: cases,
            RejoinStepName: rejoinStepName);
    }

    /// <summary>
    /// Extracts discriminator information from a Branch's first argument.
    /// </summary>
    private static bool TryExtractDiscriminatorInfo(
        ArgumentSyntax discriminatorArg,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum,
        out bool isMethod)
    {
        propertyPath = string.Empty;
        typeName = string.Empty;
        isEnum = false;
        isMethod = false;

        // Try to extract lambda: state => state.Property
        var lambda = discriminatorArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
            _ => null
        };

        if (lambda is not null)
        {
            return TryExtractLambdaDiscriminator(lambda, semanticModel, out propertyPath, out typeName, out isEnum);
        }

        // Try to extract method reference: DetermineOutcome (IdentifierNameSyntax)
        if (discriminatorArg.Expression is IdentifierNameSyntax identifier)
        {
            return TryExtractMethodReferenceDiscriminator(identifier, semanticModel, out propertyPath, out typeName, out isEnum, out isMethod);
        }

        return false;
    }

    /// <summary>
    /// Extracts discriminator info from a lambda expression (state => state.Property).
    /// </summary>
    private static bool TryExtractLambdaDiscriminator(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum)
    {
        propertyPath = string.Empty;
        typeName = string.Empty;
        isEnum = false;

        var body = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax parens => parens.Body,
            _ => null
        };

        if (body is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        propertyPath = SyntaxHelper.ExtractPropertyPath(memberAccess);

        var typeInfo = semanticModel.GetTypeInfo(memberAccess);
        if (typeInfo.Type is INamedTypeSymbol namedType)
        {
            typeName = namedType.Name;
            isEnum = namedType.TypeKind == TypeKind.Enum;
        }
        else
        {
            typeName = "Object";
        }

        return !string.IsNullOrEmpty(propertyPath);
    }

    /// <summary>
    /// Extracts discriminator info from a method reference identifier (e.g., DetermineOutcome).
    /// </summary>
    private static bool TryExtractMethodReferenceDiscriminator(
        IdentifierNameSyntax identifier,
        SemanticModel semanticModel,
        out string propertyPath,
        out string typeName,
        out bool isEnum,
        out bool isMethod)
    {
        propertyPath = identifier.Identifier.Text;
        typeName = string.Empty;
        isEnum = false;
        isMethod = true;

        var symbolInfo = semanticModel.GetSymbolInfo(identifier);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            var returnType = methodSymbol.ReturnType;
            if (returnType is INamedTypeSymbol returnNamedType)
            {
                typeName = returnNamedType.Name;
                isEnum = returnNamedType.TypeKind == TypeKind.Enum;
            }
            else
            {
                typeName = returnType.Name;
            }
        }
        else
        {
            typeName = "Object";
        }

        return !string.IsNullOrEmpty(propertyPath);
    }

    /// <summary>
    /// Parses a branch case argument into a BranchCaseModel.
    /// </summary>
    private static bool TryParseBranchCase(
        ArgumentSyntax caseArg,
        SemanticModel semanticModel,
        string branchPropertyPath,
        CancellationToken cancellationToken,
        out BranchCaseModel caseModel)
    {
        caseModel = default!;

        if (caseArg.Expression is not InvocationExpressionSyntax caseInvocation)
        {
            return false;
        }

        // Extract the case value and path builder argument from When() or Otherwise()
        if (!TryExtractCaseArguments(caseInvocation, out var caseValueLiteral, out var pathBuilderArg))
        {
            return false;
        }

        // Extract path builder lambda
        var pathLambda = pathBuilderArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => (LambdaExpressionSyntax)parens,
            _ => null
        };

        if (pathLambda is null)
        {
            return false;
        }

        // Extract steps from the path builder
        var stepNames = new List<string>();
        var isTerminal = false;
        ParseBranchPathBody(pathLambda, semanticModel, stepNames, ref isTerminal, cancellationToken);

        if (stepNames.Count == 0)
        {
            return false;
        }

        var branchPathPrefix = $"{branchPropertyPath}_{caseValueLiteral.Replace(".", "_").Replace(" ", "_")}";

        caseModel = new BranchCaseModel(
            CaseValueLiteral: caseValueLiteral,
            BranchPathPrefix: branchPathPrefix,
            StepNames: stepNames,
            IsTerminal: isTerminal);

        return true;
    }

    /// <summary>
    /// Extracts the case value literal and path builder argument from a When() or Otherwise() invocation.
    /// </summary>
    private static bool TryExtractCaseArguments(
        InvocationExpressionSyntax caseInvocation,
        out string caseValueLiteral,
        out ArgumentSyntax pathBuilderArg)
    {
        caseValueLiteral = string.Empty;
        pathBuilderArg = default!;

        var isOtherwise = SyntaxHelper.IsMethodCall(caseInvocation, "Otherwise");
        var isWhen = SyntaxHelper.IsMethodCall(caseInvocation, "When");

        if (!isWhen && !isOtherwise)
        {
            return false;
        }

        var caseArgs = caseInvocation.ArgumentList.Arguments;

        if (isOtherwise)
        {
            if (caseArgs.Count < 1)
            {
                return false;
            }

            caseValueLiteral = "default";
            pathBuilderArg = caseArgs[0];
            return true;
        }

        // isWhen
        if (caseArgs.Count < 2)
        {
            return false;
        }

        caseValueLiteral = ExtractCaseValueLiteral(caseArgs[0]);
        pathBuilderArg = caseArgs[1];
        return true;
    }

    /// <summary>
    /// Extracts the case value literal from a When() argument.
    /// </summary>
    private static string ExtractCaseValueLiteral(ArgumentSyntax valueArg)
    {
        var expression = valueArg.Expression;

        return expression switch
        {
            // Enum value: ClaimType.Auto
            MemberAccessExpressionSyntax memberAccess => memberAccess.ToString(),
            // String literal: "pdf"
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.StringLiteralExpression => literal.Token.ValueText,
            // Boolean: true/false
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.TrueLiteralExpression => "true",
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.FalseLiteralExpression => "false",
            // Numeric: 1, 2, etc.
            LiteralExpressionSyntax literal when literal.Kind() == SyntaxKind.NumericLiteralExpression => literal.Token.ValueText,
            // Default fallback
            _ => expression.ToString()
        };
    }

    /// <summary>
    /// Parses the branch path body to extract step names and terminal flag.
    /// </summary>
    /// <remarks>
    /// This method only extracts steps from the direct path, not from nested lambdas
    /// such as approval OnTimeout/OnRejection handlers. This ensures that branch
    /// routing goes to the correct first step of each case path.
    /// </remarks>
    private static void ParseBranchPathBody(
        LambdaExpressionSyntax pathLambda,
        SemanticModel semanticModel,
        List<string> stepNames,
        ref bool isTerminal,
        CancellationToken cancellationToken)
    {
        // Find all nested lambdas (approval handlers, etc.) to exclude their contents
        var nestedLambdas = pathLambda
            .DescendantNodes()
            .OfType<LambdaExpressionSyntax>()
            .Where(l => l != pathLambda)
            .ToList();

        // Find all invocations in the path body, reversed for correct order
        var allInvocations = pathLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Reverse()
            .ToList();

        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip invocations that are inside nested lambdas (approval handlers, etc.)
            var isInNestedLambda = nestedLambdas.Any(lambda => lambda.Span.Contains(inv.Span));
            if (isInNestedLambda)
            {
                continue;
            }

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                if (StepExtractor.TryGetStepName(inv, semanticModel, out var stepName))
                {
                    stepNames.Add(stepName);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Complete"))
            {
                isTerminal = true;
            }
        }
    }

    /// <summary>
    /// Finds the rejoin step name after a Branch.
    /// </summary>
    private static string? FindRejoinStepName(
        InvocationExpressionSyntax branchInvocation,
        SemanticModel semanticModel)
    {
        // Look for calls that use this branch as their receiver
        var parent = branchInvocation.Parent;
        while (parent is not null)
        {
            if (parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression == branchInvocation)
            {
                // Found a call that chains off this branch
                if (memberAccess.Parent is InvocationExpressionSyntax nextInvocation)
                {
                    if (StepExtractor.TryGetStepName(nextInvocation, semanticModel, out var stepName))
                    {
                        return stepName;
                    }
                }
            }

            parent = parent.Parent;
        }

        return null;
    }

    /// <summary>
    /// Finds the index of this branch invocation among all branches in the workflow.
    /// </summary>
    private static int FindBranchIndexInWorkflow(InvocationExpressionSyntax branchInvocation)
    {
        // Find the root syntax (usually the class declaration)
        var root = branchInvocation.Ancestors().LastOrDefault() ?? branchInvocation;

        // Find all Branch calls in order
        var allBranches = root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "Branch"))
            .ToList();

        return allBranches.IndexOf(branchInvocation);
    }
}
