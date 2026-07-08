// -----------------------------------------------------------------------
// <copyright file="ApprovalExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts approval models from a workflow definition.
/// </summary>
/// <remarks>
/// This extractor parses <c>AwaitApproval&lt;TApprover&gt;()</c> calls from the fluent DSL
/// and produces <see cref="ApprovalModel"/> instances for saga code generation.
/// </remarks>
internal static class ApprovalExtractor
{
    /// <summary>
    /// Extracts approval models from the workflow DSL for saga handler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of approval models in the order they appear in the workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<ApprovalModel> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Find all AwaitApproval() method calls
        var awaitApprovalInvocations = context.AllInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "AwaitApproval"))
            .ToList();

        if (awaitApprovalInvocations.Count == 0)
        {
            return [];
        }

        var approvals = new List<ApprovalModel>();
        var approvalIndex = 0;

        foreach (var invocation in awaitApprovalInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryParseApproval(invocation, context.SemanticModel, context.WorkflowName ?? string.Empty, approvalIndex, out var approvalModel, context.CancellationToken))
            {
                approvals.Add(approvalModel);
                approvalIndex++;
            }
        }

        return approvals;
    }

    private static bool TryParseApproval(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string workflowName,
        int approvalIndex,
        out ApprovalModel approvalModel,
        CancellationToken cancellationToken)
    {
        approvalModel = default!;

        // Extract the approver type from the generic type argument
        if (!TryGetApproverTypeName(invocation, semanticModel, out var approverTypeName))
        {
            return false;
        }

        // Find the preceding step by walking the invocation chain backwards
        if (!TryFindPrecedingStepName(invocation, semanticModel, out var precedingStepName, cancellationToken))
        {
            return false;
        }

        // Generate approval point name based on approver type (without "Approver" suffix). The SAME
        // derivation is used by the JSON-import path (WireToModelBridge.MapApprovals) via the shared
        // ApprovalPointNaming.Derive, so the two authoring channels cannot drift.
        var approvalPointName = ApprovalPointNaming.Derive(approverTypeName, approvalIndex);

        // Parse Phase 2 configuration: OnTimeout and OnRejection
        IReadOnlyList<StepModel>? escalationSteps = null;
        IReadOnlyList<StepModel>? rejectionSteps = null;
        IReadOnlyList<ApprovalModel>? nestedApprovals = null;
        var isEscalationTerminal = false;
        var isRejectionTerminal = false;

        // Get the configuration lambda from AwaitApproval<T>(a => a...)
        var configLambda = GetConfigurationLambda(invocation);
        if (configLambda is not null)
        {
            TryParseOnRejection(configLambda, semanticModel, out rejectionSteps, out isRejectionTerminal, cancellationToken);
            TryParseOnTimeout(configLambda, semanticModel, workflowName, out escalationSteps, out nestedApprovals, out isEscalationTerminal, cancellationToken);
        }

        approvalModel = ApprovalModel.Create(
            approvalPointName: approvalPointName,
            approverTypeName: approverTypeName,
            precedingStepName: precedingStepName,
            escalationSteps: escalationSteps,
            rejectionSteps: rejectionSteps,
            nestedEscalationApprovals: nestedApprovals,
            isEscalationTerminal: isEscalationTerminal,
            isRejectionTerminal: isRejectionTerminal);

        return true;
    }

    /// <summary>
    /// Extracts the configuration lambda from an AwaitApproval invocation.
    /// </summary>
    /// <param name="awaitApprovalInvocation">The AwaitApproval invocation.</param>
    /// <returns>The lambda expression, or null if not found.</returns>
    private static LambdaExpressionSyntax? GetConfigurationLambda(InvocationExpressionSyntax awaitApprovalInvocation)
    {
        var arguments = awaitApprovalInvocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        return arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };
    }

    /// <summary>
    /// Parses OnRejection configuration from the approval lambda.
    /// </summary>
    private static void TryParseOnRejection(
        LambdaExpressionSyntax configLambda,
        SemanticModel semanticModel,
        out IReadOnlyList<StepModel>? rejectionSteps,
        out bool isTerminal,
        CancellationToken cancellationToken)
    {
        rejectionSteps = null;
        isTerminal = false;

        // Find OnRejection invocations within the config lambda
        var onRejectionInvocations = configLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "OnRejection"))
            .ToList();

        if (onRejectionInvocations.Count == 0)
        {
            return;
        }

        var onRejectionInvocation = onRejectionInvocations[0];
        var handlerLambda = GetHandlerLambda(onRejectionInvocation);
        if (handlerLambda is null)
        {
            return;
        }

        var steps = new List<StepModel>();
        ParseHandlerBody(handlerLambda, semanticModel, steps, ref isTerminal, cancellationToken);

        if (steps.Count > 0)
        {
            rejectionSteps = steps;
        }
    }

    /// <summary>
    /// Parses OnTimeout configuration from the approval lambda.
    /// </summary>
    private static void TryParseOnTimeout(
        LambdaExpressionSyntax configLambda,
        SemanticModel semanticModel,
        string workflowName,
        out IReadOnlyList<StepModel>? escalationSteps,
        out IReadOnlyList<ApprovalModel>? nestedApprovals,
        out bool isTerminal,
        CancellationToken cancellationToken)
    {
        escalationSteps = null;
        nestedApprovals = null;
        isTerminal = false;

        // Find OnTimeout invocations within the config lambda
        var onTimeoutInvocations = configLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "OnTimeout"))
            .ToList();

        if (onTimeoutInvocations.Count == 0)
        {
            return;
        }

        var onTimeoutInvocation = onTimeoutInvocations[0];
        var handlerLambda = GetHandlerLambda(onTimeoutInvocation);
        if (handlerLambda is null)
        {
            return;
        }

        var steps = new List<StepModel>();
        var nested = new List<ApprovalModel>();
        ParseEscalationHandlerBody(handlerLambda, semanticModel, workflowName, steps, nested, ref isTerminal, cancellationToken);

        if (steps.Count > 0)
        {
            escalationSteps = steps;
        }

        if (nested.Count > 0)
        {
            nestedApprovals = nested;
        }
    }

    /// <summary>
    /// Gets the handler lambda from an OnRejection or OnTimeout invocation.
    /// </summary>
    private static LambdaExpressionSyntax? GetHandlerLambda(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return null;
        }

        return arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };
    }

    /// <summary>
    /// Parses a handler body for Then steps and Complete() calls.
    /// </summary>
    private static void ParseHandlerBody(
        LambdaExpressionSyntax handlerLambda,
        SemanticModel semanticModel,
        List<StepModel> steps,
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
                if (TryGetStepFromThenCall(inv, semanticModel, out var stepModel))
                {
                    steps.Add(stepModel);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Complete"))
            {
                isTerminal = true;
            }
        }
    }

    /// <summary>
    /// Parses an escalation handler body for Then steps, EscalateTo, and Complete() calls.
    /// </summary>
    private static void ParseEscalationHandlerBody(
        LambdaExpressionSyntax handlerLambda,
        SemanticModel semanticModel,
        string workflowName,
        List<StepModel> steps,
        List<ApprovalModel> nestedApprovals,
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
                if (TryGetStepFromThenCall(inv, semanticModel, out var stepModel))
                {
                    steps.Add(stepModel);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "EscalateTo"))
            {
                if (TryParseEscalateTo(inv, semanticModel, workflowName, nestedApprovals.Count, out var nestedApproval, cancellationToken))
                {
                    nestedApprovals.Add(nestedApproval);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Complete"))
            {
                isTerminal = true;
            }
        }
    }

    /// <summary>
    /// Parses an EscalateTo call to create a nested ApprovalModel.
    /// </summary>
    private static bool TryParseEscalateTo(
        InvocationExpressionSyntax escalateToInvocation,
        SemanticModel semanticModel,
        string workflowName,
        int nestedIndex,
        out ApprovalModel nestedApproval,
        CancellationToken cancellationToken)
    {
        nestedApproval = default!;

        // Extract the approver type from EscalateTo<TApprover>
        if (!TryGetApproverTypeName(escalateToInvocation, semanticModel, out var approverTypeName))
        {
            return false;
        }

        var approvalPointName = ApprovalPointNaming.Derive(approverTypeName, nestedIndex);

        // For nested approvals, the preceding step is the parent approval context
        // We use "Escalation" as a placeholder since the actual preceding step depends on runtime
        nestedApproval = ApprovalModel.Create(
            approvalPointName: approvalPointName,
            approverTypeName: approverTypeName,
            precedingStepName: "Escalation");

        return true;
    }

    /// <summary>
    /// Extracts a StepModel from a Then&lt;TStep&gt;() invocation.
    /// </summary>
    private static bool TryGetStepFromThenCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out StepModel stepModel)
    {
        stepModel = default!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArgument is null)
        {
            return false;
        }

        string stepName;
        string stepTypeName;

        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            stepName = namedType.Name;
            stepTypeName = namedType.ToDisplayString();
        }
        else
        {
            stepName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
            stepTypeName = stepName;
            if (string.IsNullOrEmpty(stepName))
            {
                return false;
            }
        }

        stepModel = StepModel.Create(stepName, stepTypeName);
        return true;
    }

    private static bool TryGetApproverTypeName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string approverTypeName)
    {
        approverTypeName = string.Empty;

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

        // Try to get the symbol for better naming
        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            approverTypeName = namedType.Name;
            return true;
        }

        // Fallback to syntax-based name
        approverTypeName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
        return !string.IsNullOrEmpty(approverTypeName);
    }

    private static bool TryFindPrecedingStepName(
        InvocationExpressionSyntax awaitApprovalInvocation,
        SemanticModel semanticModel,
        out string precedingStepName,
        CancellationToken cancellationToken)
    {
        precedingStepName = string.Empty;

        // Walk backwards through the invocation chain to find the preceding step
        // AwaitApproval is called on the result of a previous method (StartWith, Then, etc.)
        if (awaitApprovalInvocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // The expression part is the previous invocation in the chain
        var previousExpression = memberAccess.Expression;

        // Walk back until we find a StartWith or Then call
        while (previousExpression is InvocationExpressionSyntax previousInvocation)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(previousInvocation, "StartWith") ||
                SyntaxHelper.IsMethodCall(previousInvocation, "Then"))
            {
                // Found the preceding step - extract its type name
                if (TryGetStepTypeName(previousInvocation, semanticModel, out var stepName))
                {
                    precedingStepName = stepName;
                    return true;
                }
            }

            // Continue walking back
            if (previousInvocation.Expression is MemberAccessExpressionSyntax prevMemberAccess)
            {
                previousExpression = prevMemberAccess.Expression;
            }
            else
            {
                break;
            }
        }

        // Handle approvals inside branch lambdas:
        // When AwaitApproval is called on a lambda parameter (e.g., path.AwaitApproval),
        // there is no preceding step in the invocation chain.
        // Use "BranchPath" as a placeholder for the preceding step.
        if (previousExpression is IdentifierNameSyntax)
        {
            precedingStepName = "BranchPath";
            return true;
        }

        return false;
    }

    private static bool TryGetStepTypeName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string stepName)
    {
        stepName = string.Empty;

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

        // Try to get the symbol for better naming
        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            stepName = namedType.Name;
            return true;
        }

        // Fallback to syntax-based name
        stepName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
        return !string.IsNullOrEmpty(stepName);
    }
}
