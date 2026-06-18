// -----------------------------------------------------------------------
// <copyright file="FailureHandlerExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts failure handler models from a workflow definition.
/// </summary>
internal static class FailureHandlerExtractor
{
    /// <summary>
    /// Extracts failure handler models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of failure handler models in the order they appear in the workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<FailureHandlerModel> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Find all OnFailure() method calls
        var onFailureInvocations = context.AllInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "OnFailure"))
            .ToList();

        if (onFailureInvocations.Count == 0)
        {
            return [];
        }

        var handlers = new List<FailureHandlerModel>();
        var handlerIndex = 0;

        foreach (var onFailureInvocation in onFailureInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryParseFailureHandler(onFailureInvocation, context.SemanticModel, context.WorkflowName ?? string.Empty, handlerIndex, out var handlerModel, context.CancellationToken))
            {
                handlers.Add(handlerModel);
                handlerIndex++;
            }
        }

        return handlers;
    }

    /// <summary>
    /// Symbol display format that produces Namespace.TypeName without the global:: prefix.
    /// </summary>
    private static readonly SymbolDisplayFormat NamespacedTypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    private static bool TryParseFailureHandler(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string workflowName,
        int handlerIndex,
        out FailureHandlerModel handlerModel,
        CancellationToken cancellationToken)
    {
        handlerModel = default!;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 1)
        {
            return false;
        }

        // First argument: failure handler lambda (f => f.Then<LogFailure>().Complete())
        var handlerArg = arguments[0];
        var handlerLambda = handlerArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };

        if (handlerLambda is null)
        {
            return false;
        }

        // Parse handler body - extract both step names and step models
        var stepNames = new List<string>();
        var stepModels = new List<StepModel>();
        var isTerminal = false;
        ParseFailureHandlerBody(handlerLambda, semanticModel, stepNames, stepModels, ref isTerminal, cancellationToken);

        if (stepNames.Count == 0)
        {
            return false;
        }

        // Determine scope - for now, all OnFailure() calls are workflow-scoped
        // Step-scoped handlers will use a different syntax (e.g., Then<Step>(config => config.OnFailure(...)))
        var scope = FailureHandlerScope.Workflow;
        var handlerId = $"{workflowName}-FailureHandler{handlerIndex}";

        handlerModel = FailureHandlerModel.Create(
            handlerId,
            scope,
            stepNames,
            isTerminal,
            triggerStepName: null,
            steps: stepModels);

        return true;
    }

    private static void ParseFailureHandlerBody(
        LambdaExpressionSyntax handlerLambda,
        SemanticModel semanticModel,
        List<string> stepNames,
        List<StepModel> stepModels,
        ref bool isTerminal,
        CancellationToken cancellationToken)
    {
        // Find all invocations in the handler body, reversed for correct order
        var allInvocations = handlerLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Reverse()
            .ToList();

        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                if (TryGetStepNameAndModel(inv, semanticModel, out var stepName, out var stepModel))
                {
                    stepNames.Add(stepName);
                    if (stepModel is not null)
                    {
                        stepModels.Add(stepModel);
                    }
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Complete"))
            {
                isTerminal = true;
            }
        }
    }

    private static bool TryGetStepNameAndModel(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string stepName,
        out StepModel? stepModel)
    {
        stepName = string.Empty;
        stepModel = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Check if it's generic (has type argument)
        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        // Get the type argument
        var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArgument is null)
        {
            return false;
        }

        // Prefer the shared configured-step builder so any per-step resilience
        // (WithRetry/WithTimeout/Compensate/confidence) and ValidateState guard declared via the
        // Then<TStep>(step => step...) configure-lambda overload (DR-7) threads into the StepModel,
        // bringing failure-handler steps to parity with the top-level/loop/fork parse paths.
        if (StepExtractor.TryBuildConfiguredStepModel(invocation, semanticModel, out var configuredStepModel))
        {
            stepName = configuredStepModel.StepName;
            stepModel = configuredStepModel;
            return true;
        }

        // Try to get the symbol for better naming and full type information
        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            stepName = namedType.Name;
            var stepTypeName = namedType.ToDisplayString(NamespacedTypeFormat);
            stepModel = StepModel.Create(stepName, stepTypeName);
            return true;
        }

        // Fallback to syntax-based name
        stepName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
        if (!string.IsNullOrEmpty(stepName))
        {
            // Create StepModel with syntax-based type name as fallback
            var stepTypeName = typeArgument.ToString();
            stepModel = StepModel.Create(stepName, stepTypeName);
            return true;
        }

        return false;
    }
}
