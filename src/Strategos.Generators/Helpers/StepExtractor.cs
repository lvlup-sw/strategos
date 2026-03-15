// -----------------------------------------------------------------------
// <copyright file="StepExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Represents the execution context in which a step appears.
/// Used for context-aware duplicate detection.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>
/// <c>Linear</c>: Main workflow flow - duplicates NOT allowed
/// </description></item>
/// <item><description>
/// <c>ForkPath</c>: Inside Fork paths (parallel execution) - duplicates NOT allowed
/// </description></item>
/// <item><description>
/// <c>BranchPath</c>: Inside Branch paths (exclusive execution) - duplicates ALLOWED
/// </description></item>
/// </list>
/// </remarks>
internal enum StepContext
{
    /// <summary>
    /// Step is in the main linear flow of the workflow.
    /// Duplicates in linear flow are NOT allowed.
    /// </summary>
    Linear,

    /// <summary>
    /// Step is inside a Fork path (parallel execution).
    /// Duplicates across fork paths are NOT allowed - would cause routing issues.
    /// </summary>
    ForkPath,

    /// <summary>
    /// Step is inside a Branch path (exclusive execution).
    /// Duplicates across branch paths ARE allowed - only one path executes.
    /// </summary>
    BranchPath,
}

/// <summary>
/// Represents a step with optional loop context, instance name, and execution context information.
/// </summary>
/// <param name="StepName">The name of the step type.</param>
/// <param name="InstanceName">The optional instance name for distinguishing same step types.</param>
/// <param name="LoopName">The name of the parent loop, if any.</param>
/// <param name="Context">The execution context (Linear, ForkPath, or BranchPath).</param>
internal sealed record StepInfo(
    string StepName,
    string? InstanceName = null,
    string? LoopName = null,
    StepContext Context = StepContext.Linear)
{
    /// <summary>
    /// Gets the phase name, which includes the loop prefix if this step is inside a loop.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="EffectiveName"/> to support instance-named steps.
    /// </remarks>
    public string PhaseName => LoopName is null ? EffectiveName : $"{LoopName}_{EffectiveName}";

    /// <summary>
    /// Gets the effective name for duplicate detection.
    /// Returns InstanceName if specified, otherwise StepName.
    /// </summary>
    /// <remarks>
    /// Instance names allow the same step type to be used multiple times
    /// with distinct identities for duplicate detection and phase tracking.
    /// </remarks>
    public string EffectiveName => InstanceName ?? StepName;
}

/// <summary>
/// Extracts step information from a workflow definition.
/// </summary>
internal static class StepExtractor
{
    private static readonly HashSet<string> DslMethodNames = new(StringComparer.Ordinal)
    {
        "StartWith",
        "Then",
        "Finally",
        "RepeatUntil",
        "Join", // Fork/Join construct - the join step follows fork paths
    };

    /// <summary>
    /// Symbol display format that produces Namespace.TypeName without the global:: prefix.
    /// </summary>
    private static readonly SymbolDisplayFormat NamespacedTypeFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <summary>
    /// Extracts step information including loop context from the workflow DSL.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of step information in the order they appear in the workflow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<StepInfo> ExtractStepInfos(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.FinallyInvocation is null)
        {
            return [];
        }

        // Walk the invocation chain and collect step information
        var steps = new List<StepInfo>();
        WalkInvocationChainWithLoopsAndContext(context.FinallyInvocation, steps, context.SemanticModel, currentLoopPrefix: null, StepContext.Linear, context.CancellationToken);

        // Deduplicate by PhaseName - same step may appear in multiple branch paths
        return steps.GroupBy(s => s.PhaseName).Select(g => g.First()).ToList();
    }

    /// <summary>
    /// Extracts step information WITHOUT deduplication, including execution context.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>
    /// A list of step information with context markers, preserving duplicates.
    /// Used for context-aware duplicate detection.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <remarks>
    /// Unlike <see cref="ExtractStepInfos"/>, this method does NOT deduplicate steps.
    /// This allows the caller to perform context-aware duplicate detection:
    /// <list type="bullet">
    /// <item><description>Duplicates in Linear context: ERROR</description></item>
    /// <item><description>Duplicates in ForkPath context: ERROR</description></item>
    /// <item><description>Duplicates in BranchPath context: OK (exclusive execution)</description></item>
    /// </list>
    /// </remarks>
    public static IReadOnlyList<StepInfo> ExtractRawStepInfos(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.FinallyInvocation is null)
        {
            return [];
        }

        // Walk the invocation chain and collect step information with context
        var steps = new List<StepInfo>();
        WalkInvocationChainWithLoopsAndContext(context.FinallyInvocation, steps, context.SemanticModel, currentLoopPrefix: null, StepContext.Linear, context.CancellationToken);

        // Return WITHOUT deduplication - caller needs to see duplicates for validation
        return steps;
    }

    /// <summary>
    /// Extracts step models with full type information for DI registration and handler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>A list of step models with step name, fully qualified type name, and loop context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<StepModel> ExtractStepModels(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.FinallyInvocation is null)
        {
            return [];
        }

        // Walk the invocation chain and collect step models
        var steps = new List<StepModel>();
        WalkInvocationChainForStepModels(context.FinallyInvocation, steps, context.SemanticModel, currentLoopPrefix: null, context.CancellationToken);

        // Deduplicate by PhaseName - same step may appear in multiple branch paths
        return steps.GroupBy(s => s.PhaseName).Select(g => g.First()).ToList();
    }

    /// <summary>
    /// Tries to get the step name from an invocation expression.
    /// </summary>
    /// <param name="invocation">The invocation expression to check.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="stepName">The extracted step name, if successful.</param>
    /// <returns>True if the step name was extracted; otherwise, false.</returns>
    internal static bool TryGetStepName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string stepName)
    {
        return TryGetStepNameAndInstanceName(invocation, semanticModel, out stepName, out _);
    }

    /// <summary>
    /// Tries to get the step name and optional instance name from an invocation expression.
    /// </summary>
    /// <param name="invocation">The invocation expression to check.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="stepName">The extracted step name, if successful.</param>
    /// <param name="instanceName">The extracted instance name (e.g., from Then&lt;T&gt;("name")), or null if not specified.</param>
    /// <returns>True if the step name was extracted; otherwise, false.</returns>
    internal static bool TryGetStepNameAndInstanceName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string stepName,
        out string? instanceName)
    {
        stepName = string.Empty;
        instanceName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Check if this is a DSL method
        var methodName = SyntaxHelper.GetMethodName(memberAccess);
        if (!DslMethodNames.Contains(methodName))
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
        }
        else
        {
            // Fallback to syntax-based name
            stepName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
            if (string.IsNullOrEmpty(stepName))
            {
                return false;
            }
        }

        // Check for instance name argument: Then<T>("InstanceName")
        var arguments = invocation.ArgumentList?.Arguments;
        if (arguments is not null && arguments.Value.Count > 0)
        {
            var firstArg = arguments.Value[0];
            if (firstArg.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
            {
                instanceName = literal.Token.ValueText;
            }
        }

        return true;
    }

    /// <summary>
    /// Walks the invocation chain collecting steps with their execution context.
    /// </summary>
    /// <param name="invocation">The invocation to process.</param>
    /// <param name="steps">The list to add steps to.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="currentLoopPrefix">The current loop prefix, if any.</param>
    /// <param name="currentContext">The current execution context (Linear, ForkPath, or BranchPath).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static void WalkInvocationChainWithLoopsAndContext(
        InvocationExpressionSyntax invocation,
        List<StepInfo> steps,
        SemanticModel semanticModel,
        string? currentLoopPrefix,
        StepContext currentContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if this is a RepeatUntil call
        if (TryParseRepeatUntilWithContext(invocation, semanticModel, currentLoopPrefix, currentContext, out var effectivePrefix, out var bodySteps, cancellationToken))
        {
            // Insert body steps (in reverse order since we insert at beginning)
            // Body steps already have their correct StepInfo with full prefix and context
            for (var i = bodySteps.Count - 1; i >= 0; i--)
            {
                steps.Insert(0, bodySteps[i]);
            }
        }
        else if (SyntaxHelper.IsMethodCall(invocation, "Fork"))
        {
            // Process fork path steps with ForkPath context
            ParseForkPathStepsWithContext(invocation, semanticModel, currentLoopPrefix, steps, cancellationToken);
        }
        else if (SyntaxHelper.IsMethodCall(invocation, "Branch"))
        {
            // Process branch path steps with BranchPath context
            ParseBranchPathStepsWithContext(invocation, semanticModel, currentLoopPrefix, steps, cancellationToken);
        }
        else if (SyntaxHelper.IsMethodCall(invocation, "Join"))
        {
            // Add join step - Linear context (after fork paths complete)
            if (TryGetJoinStepName(invocation, semanticModel, out var joinStepName))
            {
                steps.Insert(0, new StepInfo(joinStepName, null, currentLoopPrefix, currentContext));
            }
        }
        else if (TryGetStepNameAndInstanceName(invocation, semanticModel, out var stepName, out var instanceName))
        {
            // Regular step - insert at beginning since we're walking backwards
            steps.Insert(0, new StepInfo(stepName, instanceName, currentLoopPrefix, currentContext));
        }

        // Walk to the receiver (previous call in the chain)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            // The receiver could be another invocation
            if (memberAccess.Expression is InvocationExpressionSyntax previousInvocation)
            {
                WalkInvocationChainWithLoopsAndContext(previousInvocation, steps, semanticModel, currentLoopPrefix, currentContext, cancellationToken);
            }
        }
    }

    private static bool TryParseRepeatUntil(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? parentLoopPrefix,
        out string effectivePrefix,
        out List<StepInfo> bodySteps,
        CancellationToken cancellationToken)
    {
        bodySteps = new List<StepInfo>();

        // Use shared utility for loop parsing
        if (!InvocationChainWalker.TryParseRepeatUntil(invocation, parentLoopPrefix, out effectivePrefix, out var bodyLambda)
            || bodyLambda is null)
        {
            return false;
        }

        // Process the lambda body - collect direct Then<T>() calls and nested RepeatUntil
        ParseLoopBody(bodyLambda, semanticModel, effectivePrefix, bodySteps, cancellationToken);

        return true;
    }

    private static bool TryParseRepeatUntilWithContext(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? parentLoopPrefix,
        StepContext currentContext,
        out string effectivePrefix,
        out List<StepInfo> bodySteps,
        CancellationToken cancellationToken)
    {
        bodySteps = new List<StepInfo>();

        // Use shared utility for loop parsing
        if (!InvocationChainWalker.TryParseRepeatUntil(invocation, parentLoopPrefix, out effectivePrefix, out var bodyLambda)
            || bodyLambda is null)
        {
            return false;
        }

        // Process the lambda body with context tracking
        ParseLoopBodyWithContext(bodyLambda, semanticModel, effectivePrefix, currentContext, bodySteps, cancellationToken);

        return true;
    }

    private static void ParseLoopBody(
        LambdaExpressionSyntax bodyLambda,
        SemanticModel semanticModel,
        string currentPrefix,
        List<StepInfo> steps,
        CancellationToken cancellationToken)
    {
        // Use shared utility to collect invocations (excludes nested lambdas)
        var directInvocations = InvocationChainWalker.CollectInvocationsInLambda(bodyLambda);

        foreach (var inv in directInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                // Check if this is a Then<T>() call
                if (TryGetStepName(inv, semanticModel, out var stepName))
                {
                    steps.Add(new StepInfo(stepName, InstanceName: null, LoopName: currentPrefix));
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "RepeatUntil"))
            {
                if (TryParseRepeatUntil(inv, semanticModel, currentPrefix, out _, out var nestedSteps, cancellationToken))
                {
                    // Add all nested steps (they already have the correct prefix)
                    steps.AddRange(nestedSteps);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Fork"))
            {
                // Process fork path steps - they execute in parallel after the previous step
                ParseForkPathSteps(inv, semanticModel, currentPrefix, steps, cancellationToken);
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Join"))
            {
                // Add join step - executes after all fork paths complete
                if (TryGetJoinStepName(inv, semanticModel, out var joinStepName))
                {
                    steps.Add(new StepInfo(joinStepName, currentPrefix));
                }
            }

            // NOTE: Branch() constructs are NOT parsed here.
            // Branch steps are handled separately by BranchExtractor and
            // emitted by SagaStepHandlersEmitter in a dedicated branch loop.
        }
    }

    private static void ParseLoopBodyWithContext(
        LambdaExpressionSyntax bodyLambda,
        SemanticModel semanticModel,
        string currentPrefix,
        StepContext currentContext,
        List<StepInfo> steps,
        CancellationToken cancellationToken)
    {
        // Use shared utility to collect invocations (excludes nested lambdas)
        var directInvocations = InvocationChainWalker.CollectInvocationsInLambda(bodyLambda);

        foreach (var inv in directInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                // Check if this is a Then<T>() call
                if (TryGetStepNameAndInstanceName(inv, semanticModel, out var stepName, out var instanceName))
                {
                    steps.Add(new StepInfo(stepName, instanceName, currentPrefix, currentContext));
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "RepeatUntil"))
            {
                if (TryParseRepeatUntilWithContext(inv, semanticModel, currentPrefix, currentContext, out _, out var nestedSteps, cancellationToken))
                {
                    // Add all nested steps (they already have the correct prefix and context)
                    steps.AddRange(nestedSteps);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Fork"))
            {
                // Process fork path steps with ForkPath context
                ParseForkPathStepsWithContext(inv, semanticModel, currentPrefix, steps, cancellationToken);
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Branch"))
            {
                // Process branch path steps with BranchPath context
                ParseBranchPathStepsWithContext(inv, semanticModel, currentPrefix, steps, cancellationToken);
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Join"))
            {
                // Add join step - executes after all fork paths complete (Linear context)
                if (TryGetJoinStepName(inv, semanticModel, out var joinStepName))
                {
                    steps.Add(new StepInfo(joinStepName, null, currentPrefix, currentContext));
                }
            }
        }
    }

    /// <summary>
    /// Parses fork path steps from a Fork invocation and adds them to the steps list.
    /// </summary>
    /// <param name="forkInvocation">The Fork invocation expression.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="currentPrefix">The current loop prefix.</param>
    /// <param name="steps">The steps list to add to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static void ParseForkPathSteps(
        InvocationExpressionSyntax forkInvocation,
        SemanticModel semanticModel,
        string currentPrefix,
        List<StepInfo> steps,
        CancellationToken cancellationToken)
    {
        var arguments = forkInvocation.ArgumentList.Arguments;

        foreach (var arg in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Extract path builder lambda: path => path.Then<Step>()
            var pathLambda = arg.Expression switch
            {
                SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
                ParenthesizedLambdaExpressionSyntax parens => parens,
                _ => null
            };

            if (pathLambda is null)
            {
                continue;
            }

            // Find Then<T>() calls in the path lambda
            var pathInvocations = pathLambda
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => SyntaxHelper.IsMethodCall(inv, "Then"))
                .Reverse()
                .ToList();

            foreach (var inv in pathInvocations)
            {
                if (TryGetStepName(inv, semanticModel, out var stepName))
                {
                    steps.Add(new StepInfo(stepName, InstanceName: null, LoopName: currentPrefix));
                }
            }
        }
    }

    /// <summary>
    /// Parses fork path steps with ForkPath context from a Fork invocation.
    /// </summary>
    private static void ParseForkPathStepsWithContext(
        InvocationExpressionSyntax forkInvocation,
        SemanticModel semanticModel,
        string? currentPrefix,
        List<StepInfo> steps,
        CancellationToken cancellationToken)
    {
        var arguments = forkInvocation.ArgumentList.Arguments;

        foreach (var arg in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Extract path builder lambda: path => path.Then<Step>()
            var pathLambda = arg.Expression switch
            {
                SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
                ParenthesizedLambdaExpressionSyntax parens => parens,
                _ => null
            };

            if (pathLambda is null)
            {
                continue;
            }

            // Find Then<T>() calls in the path lambda
            var pathInvocations = pathLambda
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => SyntaxHelper.IsMethodCall(inv, "Then"))
                .Reverse()
                .ToList();

            foreach (var inv in pathInvocations)
            {
                if (TryGetStepNameAndInstanceName(inv, semanticModel, out var stepName, out var instanceName))
                {
                    // Fork path steps have ForkPath context
                    steps.Add(new StepInfo(stepName, instanceName, currentPrefix, StepContext.ForkPath));
                }
            }
        }
    }

    /// <summary>
    /// Parses branch path steps with BranchPath context from a Branch invocation.
    /// </summary>
    private static void ParseBranchPathStepsWithContext(
        InvocationExpressionSyntax branchInvocation,
        SemanticModel semanticModel,
        string? currentPrefix,
        List<StepInfo> steps,
        CancellationToken cancellationToken)
    {
        var arguments = branchInvocation.ArgumentList.Arguments;

        // Skip first argument (discriminator), remaining are BranchCase.When()/Otherwise()
        for (var i = 1; i < arguments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryExtractBranchCasePathLambda(arguments[i], out var pathLambda))
            {
                continue;
            }

            CollectStepsFromPathLambda(pathLambda, semanticModel, currentPrefix, StepContext.BranchPath, steps);
        }
    }

    /// <summary>
    /// Tries to extract the path builder lambda from a branch case argument (When or Otherwise).
    /// </summary>
    private static bool TryExtractBranchCasePathLambda(
        ArgumentSyntax caseArg,
        out LambdaExpressionSyntax pathLambda)
    {
        pathLambda = null!;

        // BranchCase.When() or BranchCase.Otherwise() returns an invocation
        if (caseArg.Expression is not InvocationExpressionSyntax caseInvocation)
        {
            return false;
        }

        // When(value, path => path.Then<Step>()) or Otherwise(path => path.Then<Step>())
        var caseArgs = caseInvocation.ArgumentList.Arguments;
        ArgumentSyntax? pathBuilderArg = null;

        if (SyntaxHelper.IsMethodCall(caseInvocation, "When") && caseArgs.Count >= 2)
        {
            pathBuilderArg = caseArgs[1];
        }
        else if (SyntaxHelper.IsMethodCall(caseInvocation, "Otherwise") && caseArgs.Count >= 1)
        {
            pathBuilderArg = caseArgs[0];
        }

        if (pathBuilderArg is null)
        {
            return false;
        }

        var extracted = pathBuilderArg.Expression switch
        {
            SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };

        if (extracted is null)
        {
            return false;
        }

        pathLambda = extracted;
        return true;
    }

    /// <summary>
    /// Collects Then steps from a path lambda and adds them to the steps list.
    /// </summary>
    private static void CollectStepsFromPathLambda(
        LambdaExpressionSyntax pathLambda,
        SemanticModel semanticModel,
        string? currentPrefix,
        StepContext context,
        List<StepInfo> steps)
    {
        var pathInvocations = pathLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "Then"))
            .Reverse()
            .ToList();

        foreach (var inv in pathInvocations)
        {
            if (TryGetStepNameAndInstanceName(inv, semanticModel, out var stepName, out var instanceName))
            {
                steps.Add(new StepInfo(stepName, instanceName, currentPrefix, context));
            }
        }
    }

    /// <summary>
    /// Tries to get the step name from a Join invocation expression.
    /// </summary>
    /// <param name="invocation">The Join invocation expression.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="stepName">The extracted step name, if successful.</param>
    /// <returns>True if the step name was extracted; otherwise, false.</returns>
    private static bool TryGetJoinStepName(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string stepName)
    {
        stepName = string.Empty;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        // Check if this is Join<T>()
        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        if (!SyntaxHelper.GetMethodName(memberAccess).Equals("Join", StringComparison.Ordinal))
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

    private static void WalkInvocationChainForStepModels(
        InvocationExpressionSyntax invocation,
        List<StepModel> steps,
        SemanticModel semanticModel,
        string? currentLoopPrefix,
        CancellationToken cancellationToken)
    {
        // Track pending validation info that will be applied to the next step found
        string? pendingValidationPredicate = null;
        string? pendingValidationErrorMessage = null;

        WalkInvocationChainForStepModelsInternal(
            invocation,
            steps,
            semanticModel,
            currentLoopPrefix,
            ref pendingValidationPredicate,
            ref pendingValidationErrorMessage,
            cancellationToken);
    }

    private static void WalkInvocationChainForStepModelsInternal(
        InvocationExpressionSyntax invocation,
        List<StepModel> steps,
        SemanticModel semanticModel,
        string? currentLoopPrefix,
        ref string? pendingValidationPredicate,
        ref string? pendingValidationErrorMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check if this is a RepeatUntil call
        if (SyntaxHelper.IsMethodCall(invocation, "RepeatUntil"))
        {
            if (TryParseRepeatUntilForStepModels(invocation, semanticModel, currentLoopPrefix, out var effectivePrefix, out var bodyStepModels, cancellationToken))
            {
                // Insert body steps at the beginning
                for (var i = bodyStepModels.Count - 1; i >= 0; i--)
                {
                    steps.Insert(0, bodyStepModels[i]);
                }
            }
        }
        else if (SyntaxHelper.IsMethodCall(invocation, "Fork"))
        {
            // Extract step models from fork path lambdas
            if (TryParseForkForStepModels(invocation, semanticModel, currentLoopPrefix, out var forkPathStepModels, cancellationToken))
            {
                // Insert fork path steps at the beginning (deduplication happens in caller)
                for (var i = forkPathStepModels.Count - 1; i >= 0; i--)
                {
                    steps.Insert(0, forkPathStepModels[i]);
                }
            }
        }
        else if (SyntaxHelper.IsMethodCall(invocation, "ValidateState"))
        {
            // Extract validation info - it will be applied to the next step
            (pendingValidationPredicate, pendingValidationErrorMessage) = ValidationParser.Extract(invocation);
        }
        else if (TryGetStepModel(invocation, semanticModel, currentLoopPrefix, pendingValidationPredicate, pendingValidationErrorMessage, out var stepModel))
        {
            // Regular step - insert at beginning since we're walking backwards
            steps.Insert(0, stepModel);

            // Clear pending validation after applying it
            pendingValidationPredicate = null;
            pendingValidationErrorMessage = null;
        }

        // Walk to the receiver (previous call in the chain)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax previousInvocation)
            {
                WalkInvocationChainForStepModelsInternal(
                    previousInvocation,
                    steps,
                    semanticModel,
                    currentLoopPrefix,
                    ref pendingValidationPredicate,
                    ref pendingValidationErrorMessage,
                    cancellationToken);
            }
        }
    }

    private static bool TryParseRepeatUntilForStepModels(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? parentLoopPrefix,
        out string effectivePrefix,
        out List<StepModel> bodyStepModels,
        CancellationToken cancellationToken)
    {
        bodyStepModels = new List<StepModel>();

        // Use shared utility for loop parsing
        if (!InvocationChainWalker.TryParseRepeatUntil(invocation, parentLoopPrefix, out effectivePrefix, out var bodyLambda)
            || bodyLambda is null)
        {
            return false;
        }

        // Process the lambda body for step models
        ParseLoopBodyForStepModels(bodyLambda, semanticModel, effectivePrefix, bodyStepModels, cancellationToken);

        return true;
    }

    /// <summary>
    /// Tries to parse step models from a Fork invocation's path lambdas.
    /// </summary>
    /// <param name="invocation">The Fork invocation expression.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="currentLoopPrefix">The current loop prefix, if inside a loop.</param>
    /// <param name="forkPathStepModels">The extracted step models from all fork paths.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if fork paths were successfully parsed; otherwise, false.</returns>
    private static bool TryParseForkForStepModels(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? currentLoopPrefix,
        out List<StepModel> forkPathStepModels,
        CancellationToken cancellationToken)
    {
        forkPathStepModels = new List<StepModel>();

        // Parse fork path step models using existing helper
        ParseForkPathStepModels(invocation, semanticModel, currentLoopPrefix, forkPathStepModels, cancellationToken);

        return forkPathStepModels.Count > 0;
    }

    private static void ParseLoopBodyForStepModels(
        LambdaExpressionSyntax bodyLambda,
        SemanticModel semanticModel,
        string currentPrefix,
        List<StepModel> stepModels,
        CancellationToken cancellationToken)
    {
        // Use shared utility to collect invocations (excludes nested lambdas)
        var directInvocations = InvocationChainWalker.CollectInvocationsInLambda(bodyLambda);

        string? pendingValidationPredicate = null;
        string? pendingValidationErrorMessage = null;

        foreach (var inv in directInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "ValidateState"))
            {
                (pendingValidationPredicate, pendingValidationErrorMessage) = ValidationParser.Extract(inv);
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Then"))
            {
                if (TryGetStepModel(inv, semanticModel, currentPrefix, pendingValidationPredicate, pendingValidationErrorMessage, out var stepModel))
                {
                    stepModels.Add(stepModel);
                    pendingValidationPredicate = null;
                    pendingValidationErrorMessage = null;
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "RepeatUntil"))
            {
                if (TryParseRepeatUntilForStepModels(inv, semanticModel, currentPrefix, out _, out var nestedStepModels, cancellationToken))
                {
                    stepModels.AddRange(nestedStepModels);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Fork"))
            {
                // Process fork path step models
                ParseForkPathStepModels(inv, semanticModel, currentPrefix, stepModels, cancellationToken);
            }
            else if (SyntaxHelper.IsMethodCall(inv, "Join"))
            {
                // Add join step model
                if (TryGetJoinStepModel(inv, semanticModel, currentPrefix, out var joinStepModel))
                {
                    stepModels.Add(joinStepModel);
                }
            }

            // NOTE: Branch() constructs are NOT parsed here.
            // Branch steps are handled separately by BranchExtractor and
            // emitted by SagaStepHandlersEmitter in a dedicated branch loop.
        }
    }

    /// <summary>
    /// Parses fork path step models from a Fork invocation.
    /// </summary>
    private static void ParseForkPathStepModels(
        InvocationExpressionSyntax forkInvocation,
        SemanticModel semanticModel,
        string currentPrefix,
        List<StepModel> stepModels,
        CancellationToken cancellationToken)
    {
        var arguments = forkInvocation.ArgumentList.Arguments;

        foreach (var arg in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pathLambda = arg.Expression switch
            {
                SimpleLambdaExpressionSyntax simple => (LambdaExpressionSyntax)simple,
                ParenthesizedLambdaExpressionSyntax parens => parens,
                _ => null
            };

            if (pathLambda is null)
            {
                continue;
            }

            var pathInvocations = pathLambda
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => SyntaxHelper.IsMethodCall(inv, "Then"))
                .Reverse()
                .ToList();

            foreach (var inv in pathInvocations)
            {
                if (TryGetStepModel(inv, semanticModel, currentPrefix, null, null, out var stepModel))
                {
                    stepModels.Add(stepModel);
                }
            }
        }
    }

    /// <summary>
    /// Tries to get a step model from a Join invocation expression.
    /// </summary>
    private static bool TryGetJoinStepModel(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? loopName,
        out StepModel stepModel)
    {
        stepModel = default!;

        if (!TryGetGenericTypeArgument(invocation, "Join", out var typeArgument))
        {
            return false;
        }

        if (!ResolveTypeNameAndFullName(typeArgument, semanticModel, out var stepName, out var stepTypeName))
        {
            return false;
        }

        stepModel = StepModel.Create(stepName, stepTypeName, instanceName: null, loopName: loopName);
        return true;
    }

    private static bool TryGetStepModel(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? loopName,
        string? validationPredicate,
        string? validationErrorMessage,
        out StepModel stepModel)
    {
        stepModel = default!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        var methodName = SyntaxHelper.GetMethodName(memberAccess);
        if (!DslMethodNames.Contains(methodName))
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

        if (!ResolveTypeNameAndFullName(typeArgument, semanticModel, out var stepName, out var stepTypeName))
        {
            return false;
        }

        stepModel = StepModel.Create(
            stepName,
            stepTypeName,
            instanceName: null,
            loopName: loopName,
            validationPredicate: validationPredicate,
            validationErrorMessage: validationErrorMessage);
        return true;
    }

    /// <summary>
    /// Tries to get the generic type argument from an invocation of the specified method name.
    /// </summary>
    private static bool TryGetGenericTypeArgument(
        InvocationExpressionSyntax invocation,
        string expectedMethodName,
        out TypeSyntax typeArgument)
    {
        typeArgument = null!;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        if (!SyntaxHelper.GetMethodName(memberAccess).Equals(expectedMethodName, StringComparison.Ordinal))
        {
            return false;
        }

        var arg = genericName.TypeArgumentList.Arguments.FirstOrDefault();
        if (arg is null)
        {
            return false;
        }

        typeArgument = arg;
        return true;
    }

    /// <summary>
    /// Resolves a type syntax to both its short name and fully qualified type name using the semantic model.
    /// </summary>
    private static bool ResolveTypeNameAndFullName(
        TypeSyntax typeArgument,
        SemanticModel semanticModel,
        out string stepName,
        out string stepTypeName)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            stepName = namedType.Name;
            stepTypeName = namedType.ToDisplayString(NamespacedTypeFormat);
        }
        else
        {
            stepName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
            stepTypeName = typeArgument.ToString();
        }

        return !string.IsNullOrEmpty(stepName);
    }
}
