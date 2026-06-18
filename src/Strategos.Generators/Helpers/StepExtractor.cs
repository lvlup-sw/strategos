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
    /// Tries to build a configured <see cref="StepModel"/> for a fork-path <c>Then&lt;TStep&gt;()</c>
    /// invocation, mirroring the top-level/loop emitters' step model.
    /// </summary>
    /// <param name="invocation">The <c>Then</c> invocation expression for the fork-path step.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="loopPrefix">The current loop prefix, if the fork is inside a loop.</param>
    /// <param name="stepModel">The resulting configured step model, if successful.</param>
    /// <returns>True if the step model was built; otherwise, false.</returns>
    /// <remarks>
    /// Threads any per-step <c>ValidateState</c> configuration declared via the
    /// <c>Then&lt;TStep&gt;(step =&gt; step.ValidateState(...))</c> configure-lambda overload into the
    /// <see cref="StepModel"/>, scoped to this invocation's own arguments, reusing the same
    /// resolution as <see cref="ParseForkPathStepModels"/>. The instance name is intentionally
    /// dropped: fork-path phase/command/event names key off the step <b>type</b> name (this matches
    /// the pre-existing fork extraction behaviour, so emitted output is unchanged), while the new
    /// configured-step shape preserves per-step configuration such as <c>ValidateState</c>.
    /// </remarks>
    internal static bool TryBuildConfiguredForkPathStepModel(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string? loopPrefix,
        out StepModel stepModel)
    {
        var (validationPredicate, validationErrorMessage) = ExtractConfiguredValidation(invocation);
        if (!TryGetStepModel(invocation, semanticModel, loopPrefix, validationPredicate, validationErrorMessage, out stepModel))
        {
            return false;
        }

        // Fork-path steps phase/command/event on their type name, never the instance name.
        if (stepModel.InstanceName is not null)
        {
            stepModel = stepModel with { InstanceName = null };
        }

        return true;
    }

    /// <summary>
    /// Tries to build a fully configured <see cref="StepModel"/> for a <c>Then&lt;TStep&gt;()</c>
    /// invocation, routing through the shared <c>TryGetStepModel</c> path so the step carries any
    /// per-step resilience (<c>WithRetry</c>/<c>WithTimeout</c>/<c>Compensate</c>/confidence) and
    /// <c>ValidateState</c> guard declared via the configure-lambda overload.
    /// </summary>
    /// <param name="invocation">The <c>Then</c> invocation expression for the step.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="stepModel">The resulting configured step model, if successful.</param>
    /// <returns>True if the step model was built; otherwise, false.</returns>
    /// <remarks>
    /// Unlike <see cref="TryBuildConfiguredForkPathStepModel"/>, the instance name is preserved.
    /// Used by <see cref="FailureHandlerExtractor"/> so failure-handler steps thread their
    /// configure-lambda resilience into the <see cref="StepModel"/> IR (DR-7), bringing the
    /// failure-handler parse path to parity with the top-level/loop/fork parse paths.
    /// </remarks>
    internal static bool TryBuildConfiguredStepModel(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out StepModel stepModel)
    {
        var (validationPredicate, validationErrorMessage) = ExtractConfiguredValidation(invocation);
        return TryGetStepModel(invocation, semanticModel, loopName: null, validationPredicate, validationErrorMessage, out stepModel);
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
        else if (SyntaxHelper.IsMethodCall(invocation, "Branch"))
        {
            // Extract step models from branch case path lambdas. Branch case steps are
            // routed through the shared TryGetStepModel path so any per-step resilience
            // declared via the Then<TStep>(step => step.WithRetry(...)) configure-lambda
            // overload (DR-7) is captured into the StepModel IR, exactly as fork-path steps
            // are. Branch handler EMISSION is still owned by BranchExtractor/the dedicated
            // branch loop in SagaStepHandlersEmitter (keyed by step name), so surfacing the
            // configured StepModel here only enriches the deduplicated step IR; it does not
            // change which steps are emitted.
            var branchPathStepModels = new List<StepModel>();
            ParseBranchPathStepModels(invocation, semanticModel, currentLoopPrefix, branchPathStepModels, cancellationToken);

            // Insert branch path steps at the beginning (deduplication happens in caller)
            for (var i = branchPathStepModels.Count - 1; i >= 0; i--)
            {
                steps.Insert(0, branchPathStepModels[i]);
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
                // Thread per-step ValidateState configuration declared via the
                // Then<TStep>(step => step.ValidateState(...)) configure-lambda overload
                // into the StepModel, so the fork-path step's validation guard lowers into
                // the saga exactly as a top-level/loop step's does. The validation lives in
                // this Then call's own configure lambda, so scope the lookup to its arguments.
                var (validationPredicate, validationErrorMessage) = ExtractConfiguredValidation(inv);

                if (TryGetStepModel(inv, semanticModel, currentPrefix, validationPredicate, validationErrorMessage, out var stepModel))
                {
                    stepModels.Add(stepModel);
                }
            }
        }
    }

    /// <summary>
    /// Parses branch case path step models from a <c>Branch</c> invocation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mirrors <see cref="ParseForkPathStepModels"/> for branch cases: it descends into each
    /// <c>When</c>/<c>Otherwise</c> case's path lambda and routes every <c>Then</c> through the
    /// shared <see cref="TryGetStepModel"/> path, which threads per-step resilience
    /// (<c>WithRetry</c>/<c>WithTimeout</c>/<c>Compensate</c>/confidence) declared via the
    /// <c>Then&lt;TStep&gt;(step =&gt; step...)</c> configure-lambda overload (DR-7) into the
    /// <see cref="StepModel"/> IR, plus any <c>ValidateState</c> guard.
    /// </para>
    /// <para>
    /// This enriches only the deduplicated step IR consumed for worker-handler/command generation.
    /// Branch routing/handler EMISSION is owned by <c>BranchExtractor</c> and the dedicated branch
    /// loop in the saga emitter (keyed by step name), which are intentionally left unchanged.
    /// </para>
    /// </remarks>
    private static void ParseBranchPathStepModels(
        InvocationExpressionSyntax branchInvocation,
        SemanticModel semanticModel,
        string? currentPrefix,
        List<StepModel> stepModels,
        CancellationToken cancellationToken)
    {
        var arguments = branchInvocation.ArgumentList.Arguments;

        // Skip first argument (discriminator); remaining are BranchCase.When()/Otherwise().
        for (var i = 1; i < arguments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryExtractBranchCasePathLambda(arguments[i], out var pathLambda))
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
                // Scope the per-step validation lookup to this Then call's own configure lambda,
                // identically to the fork-path parse, so branch-path steps lower their validation
                // and resilience config the same way.
                var (validationPredicate, validationErrorMessage) = ExtractConfiguredValidation(inv);

                if (TryGetStepModel(inv, semanticModel, currentPrefix, validationPredicate, validationErrorMessage, out var stepModel))
                {
                    stepModels.Add(stepModel);
                }
            }
        }
    }

    /// <summary>
    /// Extracts validation info from a step's configure lambda, e.g.
    /// <c>Then&lt;TStep&gt;(step =&gt; step.ValidateState(s =&gt; ..., "message"))</c>.
    /// </summary>
    /// <param name="thenInvocation">The <c>Then</c> invocation whose configure lambda is inspected.</param>
    /// <returns>
    /// The predicate/message pair from the first <c>ValidateState</c> call found inside the
    /// configure lambda, or <c>(null, null)</c> if the step is not configured with validation.
    /// </returns>
    private static (string? Predicate, string? ErrorMessage) ExtractConfiguredValidation(
        InvocationExpressionSyntax thenInvocation)
    {
        var arguments = thenInvocation.ArgumentList?.Arguments;
        if (arguments is null)
        {
            return (null, null);
        }

        foreach (var arg in arguments.Value)
        {
            // The configure lambda is the Action<IStepConfiguration<TState>> argument.
            if (arg.Expression is not LambdaExpressionSyntax)
            {
                continue;
            }

            var validateCall = arg.Expression
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault(call => SyntaxHelper.IsMethodCall(call, "ValidateState"));

            if (validateCall is not null)
            {
                return ValidationParser.Extract(validateCall);
            }
        }

        return (null, null);
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

        var instanceName = ExtractInstanceName(invocation);
        stepModel = StepModel.Create(stepName, stepTypeName, instanceName: instanceName, loopName: loopName);
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

        var instanceName = ExtractInstanceName(invocation);
        var resilience = ExtractConfiguredResilience(invocation, semanticModel);
        stepModel = StepModel.Create(
            stepName,
            stepTypeName,
            instanceName: instanceName,
            loopName: loopName,
            validationPredicate: validationPredicate,
            validationErrorMessage: validationErrorMessage,
            retry: resilience.Retry,
            timeout: resilience.Timeout,
            compensation: resilience.Compensation,
            confidence: resilience.Confidence);
        return true;
    }

    /// <summary>
    /// Extracts per-step resilience configuration from a step's configure lambda, e.g.
    /// <c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(3, TimeSpan.FromSeconds(5)).WithTimeout(...)
    /// .Compensate&lt;TRollback&gt;().RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;THandler&gt;()))</c>.
    /// </summary>
    /// <param name="thenInvocation">The <c>Then</c>/<c>StartWith</c> invocation whose configure lambda is inspected.</param>
    /// <param name="semanticModel">The semantic model, used to resolve the compensation step's fully qualified name.</param>
    /// <returns>
    /// The retry/timeout/compensation/confidence models from the first matching call of each kind
    /// found inside the configure lambda; any concern the step does not declare is left null.
    /// </returns>
    /// <remarks>
    /// Mirrors <see cref="ExtractConfiguredValidation"/>: the resilience calls live in this
    /// invocation's own <c>Action&lt;IStepConfiguration&lt;TState&gt;&gt;</c> configure lambda, so the
    /// lookup is scoped to its arguments. Routing through <see cref="TryGetStepModel"/> threads the
    /// same extraction uniformly into top-level, loop, and fork-path steps. Per INV-8 the
    /// compensation step is carried as its fully qualified type name (a string descriptor), never a
    /// CLR <see cref="System.Type"/>.
    /// </remarks>
    private static (RetryModel? Retry, TimeoutModel? Timeout, CompensationModel? Compensation, ConfidenceModel? Confidence)
        ExtractConfiguredResilience(
            InvocationExpressionSyntax thenInvocation,
            SemanticModel semanticModel)
    {
        var arguments = thenInvocation.ArgumentList?.Arguments;
        if (arguments is null)
        {
            return (null, null, null, null);
        }

        RetryModel? retry = null;
        TimeoutModel? timeout = null;
        CompensationModel? compensation = null;
        ConfidenceModel? confidence = null;

        foreach (var arg in arguments.Value)
        {
            // The configure lambda is the Action<IStepConfiguration<TState>> argument.
            if (arg.Expression is not LambdaExpressionSyntax)
            {
                continue;
            }

            var configInvocations = arg.Expression
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var configCall in configInvocations)
            {
                if (retry is null && SyntaxHelper.IsMethodCall(configCall, "WithRetry"))
                {
                    retry = ResilienceParser.ExtractRetry(configCall);
                }
                else if (timeout is null && SyntaxHelper.IsMethodCall(configCall, "WithTimeout"))
                {
                    timeout = ResilienceParser.ExtractTimeout(configCall);
                }
                else if (compensation is null && SyntaxHelper.IsMethodCall(configCall, "Compensate"))
                {
                    compensation = ExtractCompensation(configCall, semanticModel);
                }
                else if (SyntaxHelper.IsMethodCall(configCall, "RequireConfidence")
                    || SyntaxHelper.IsMethodCall(configCall, "OnLowConfidence"))
                {
                    confidence = MergeConfidence(confidence, configCall, semanticModel);
                }
            }
        }

        return (retry, timeout, compensation, confidence);
    }

    /// <summary>
    /// Resolves a <c>Compensate&lt;TCompensation&gt;()</c> call into a <see cref="CompensationModel"/>,
    /// carrying the compensation step's fully qualified type name (INV-8: a string descriptor,
    /// never a CLR <see cref="System.Type"/>). Mirrors the <c>Join&lt;T&gt;</c> symbol resolution.
    /// </summary>
    private static CompensationModel? ExtractCompensation(
        InvocationExpressionSyntax compensateInvocation,
        SemanticModel semanticModel)
    {
        if (!TryGetGenericTypeArgument(compensateInvocation, "Compensate", out var typeArgument))
        {
            return null;
        }

        if (!ResolveTypeNameAndFullName(typeArgument, semanticModel, out _, out var compensationTypeName))
        {
            return null;
        }

        // DR-8 / INV-5 (CompensateNotAStep): record whether the compensation type resolves to a
        // type implementing IWorkflowStep<TState>. The DSL's generic constraint also
        // rejects non-steps at the C# call site, but capturing the verdict lets the
        // generator surface a clearer, suppressible diagnostic. Unresolved symbols are
        // treated as valid (true) so the analyzer does not double-report on a type that
        // simply could not be bound (the C# compiler reports that independently).
        var symbol = semanticModel.GetSymbolInfo(typeArgument).Symbol as INamedTypeSymbol;
        var isRegisteredStep = symbol is null || ImplementsWorkflowStep(symbol);

        return new CompensationModel(compensationTypeName, IsRegisteredStep: isRegisteredStep);
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> implements
    /// <c>Strategos.Abstractions.IWorkflowStep&lt;TState&gt;</c> (any state type argument).
    /// </summary>
    private static bool ImplementsWorkflowStep(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(IsWorkflowStepInterface);

    /// <summary>
    /// Determines whether <paramref name="iface"/> is the generic
    /// <c>Strategos.Abstractions.IWorkflowStep&lt;&gt;</c> interface (matched on its open
    /// definition's metadata name + containing namespace, robust to display formatting).
    /// </summary>
    private static bool IsWorkflowStepInterface(INamedTypeSymbol iface)
    {
        if (!iface.IsGenericType)
        {
            return false;
        }

        var original = iface.OriginalDefinition;
        return string.Equals(original.MetadataName, "IWorkflowStep`1", StringComparison.Ordinal)
            && string.Equals(
                original.ContainingNamespace?.ToDisplayString(),
                "Strategos.Abstractions",
                StringComparison.Ordinal);
    }

    /// <summary>
    /// Folds a <c>RequireConfidence(double)</c> or <c>OnLowConfidence(alt =&gt; alt.Then&lt;THandler&gt;())</c>
    /// call into the accumulating <see cref="ConfidenceModel"/>.
    /// </summary>
    /// <remarks>
    /// The two calls are independent fluent members on <see cref="Strategos.Builders.IStepConfiguration{TState}"/>,
    /// so either may appear without the other; this merges whichever is seen into the same model.
    /// The low-confidence handler is identified by the first <c>Then&lt;THandler&gt;()</c> step declared
    /// inside the handler lambda — captured both as a simple name descriptor (consistent with INV-8) and
    /// as a fully-resolved <see cref="StepModel"/> so DR-5 can lower the handler step into the saga and
    /// route to it via a Wolverine cascade.
    /// </remarks>
    private static ConfidenceModel MergeConfidence(
        ConfidenceModel? existing,
        InvocationExpressionSyntax confidenceCall,
        SemanticModel semanticModel)
    {
        var threshold = existing?.Threshold ?? 0.0;
        var handlerId = existing?.OnLowConfidenceHandlerId;
        var handlerStep = existing?.OnLowConfidenceHandlerStep;

        if (SyntaxHelper.IsMethodCall(confidenceCall, "RequireConfidence"))
        {
            var arguments = confidenceCall.ArgumentList.Arguments;
            if (arguments.Count > 0 && TryGetDoubleLiteral(arguments[0].Expression, out var parsed))
            {
                threshold = parsed;
            }
        }
        else if (SyntaxHelper.IsMethodCall(confidenceCall, "OnLowConfidence"))
        {
            var extracted = ExtractLowConfidenceHandlerStep(confidenceCall, semanticModel);
            if (extracted is not null)
            {
                handlerStep = extracted;
                handlerId = extracted.StepName;
            }
        }

        return new ConfidenceModel(threshold, handlerId, handlerStep);
    }

    /// <summary>
    /// Extracts the low-confidence handler step — the first <c>Then&lt;THandler&gt;()</c> step
    /// declared inside an <c>OnLowConfidence(alt =&gt; ...)</c> handler lambda — as a fully-resolved
    /// <see cref="StepModel"/> carrying both its simple name and fully qualified type name. The fully
    /// qualified name is required so the handler step lowers into a worker handler with correct DI
    /// usings (DR-5).
    /// </summary>
    private static StepModel? ExtractLowConfidenceHandlerStep(
        InvocationExpressionSyntax onLowConfidenceCall,
        SemanticModel semanticModel)
    {
        var arguments = onLowConfidenceCall.ArgumentList.Arguments;
        if (arguments.Count == 0 || arguments[0].Expression is not LambdaExpressionSyntax handlerLambda)
        {
            return null;
        }

        var firstThen = handlerLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => SyntaxHelper.IsMethodCall(inv, "Then"));

        if (firstThen is null || !TryGetGenericTypeArgument(firstThen, "Then", out var typeArgument))
        {
            return null;
        }

        if (!ResolveTypeNameAndFullName(typeArgument, semanticModel, out var stepName, out var stepTypeName))
        {
            return null;
        }

        return StepModel.Create(stepName, stepTypeName);
    }

    private static bool TryGetDoubleLiteral(ExpressionSyntax expression, out double value)
    {
        value = 0;

        // Unary minus over a numeric literal (e.g. RequireConfidence(-0.1)) — recognized so a
        // negative (out-of-range) threshold reaches the IR and trips the out-of-range diagnostic.
        if (expression is PrefixUnaryExpressionSyntax unary
            && unary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryMinusExpression)
            && TryGetDoubleLiteral(unary.Operand, out var operand))
        {
            value = -operand;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression))
        {
            switch (literal.Token.Value)
            {
                case double d:
                    value = d;
                    return true;
                case int i:
                    value = i;
                    return true;
                case float f:
                    value = f;
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the instance name from a string literal argument (e.g., Then&lt;T&gt;("alias")).
    /// </summary>
    private static string? ExtractInstanceName(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList?.Arguments;
        if (arguments is not null && arguments.Value.Count > 0)
        {
            var firstArg = arguments.Value[0];
            if (firstArg.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
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
