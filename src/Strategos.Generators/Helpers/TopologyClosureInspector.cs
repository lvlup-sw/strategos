// -----------------------------------------------------------------------
// <copyright file="TopologyClosureInspector.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis.Operations;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Detects legal fluent topology that the syntax extractors cannot represent without loss.
/// </summary>
/// <remarks>
/// This inspector does not change generator lowering. It records a closed-world proof signal so
/// workflow/action binding cannot certify a smaller graph than the one the fluent builder executes
/// at runtime. Calls are identified by their Strategos symbols, not by method-name coincidence.
/// </remarks>
internal static class TopologyClosureInspector
{
    private const string BuildersNamespace = "Strategos.Builders";
    private const string DefinitionsNamespace = "Strategos.Definitions";
    private static readonly HashSet<string> ModelAffectingMethodNames = new(StringComparer.Ordinal)
    {
        "AwaitApproval",
        "Branch",
        "Compensate",
        "Complete",
        "Create",
        "EscalateTo",
        "Finally",
        "Fork",
        "Join",
        "OnFailure",
        "OnLowConfidence",
        "OnRejection",
        "OnTimeout",
        "RejoinMainFlow",
        "RepeatUntil",
        "StartWith",
        "Then",
        "While",
    };

    /// <summary>
    /// Extracts stable topology-closure failures from the authored workflow expression.
    /// </summary>
    /// <param name="context">The shared fluent parse context.</param>
    /// <returns>Failure reasons in deterministic source order.</returns>
    public static ImmutableArray<string> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var failures = ImmutableArray.CreateBuilder<string>();
        if (context.DefinitionClosureFailure is not null)
        {
            failures.Add(context.DefinitionClosureFailure);
        }

        if (context.FinallyInvocation is null)
        {
            return failures.ToImmutable();
        }

        InspectInvocationOwnership(context, failures);
        InspectWorkflowChain(context, failures);
        var invocations = context.FinallyInvocation
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .OrderBy(invocation => invocation.SpanStart)
            .ThenBy(invocation => invocation.Span.End);
        var workflowOnFailureCount = 0;

        foreach (var invocation in invocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!TryGetDslOperation(context, invocation, out var operation))
            {
                continue;
            }

            var containingType = operation.TargetMethod.ContainingType.OriginalDefinition.Name;
            switch (operation.TargetMethod.Name)
            {
                case "Branch" when containingType is "IWorkflowBuilder" or "ILoopBuilder":
                    InspectBranch(context, invocation, operation, containingType, failures);
                    break;

                case "RepeatUntil" or "While" when containingType is "IWorkflowBuilder" or "ILoopBuilder":
                    InspectLoop(context, invocation, operation, failures);
                    break;

                case "Fork" when containingType is "IWorkflowBuilder" or "ILoopBuilder":
                    InspectFork(context, invocation, operation, failures);
                    break;

                case "OnFailure" when containingType is "IWorkflowBuilder" or "IForkPathBuilder":
                    InspectFailureHandler(context, operation, containingType, failures);
                    if (string.Equals(containingType, "IWorkflowBuilder", StringComparison.Ordinal))
                    {
                        workflowOnFailureCount++;
                    }

                    break;

                case "OnLowConfidence" when containingType == "IStepConfiguration":
                    InspectLowConfidenceHandler(context, invocation, operation, failures);
                    break;

                case "AwaitApproval" when containingType is "IWorkflowBuilder" or "IBranchBuilder":
                    InspectApproval(context, invocation, operation, failures);
                    break;

                case "OnRejection" when containingType == "IApprovalBuilder":
                    InspectApprovalHandler(context, operation, "OnRejection", "IApprovalRejectionBuilder", failures);
                    break;

                case "OnTimeout" when containingType == "IApprovalBuilder":
                    InspectApprovalHandler(context, operation, "OnTimeout", "IApprovalEscalationBuilder", failures);
                    break;

                case "Then" when containingType == "IWorkflowBuilder"
                    && IsDelegateStep(operation.TargetMethod):
                    failures.Add(
                        "workflow delegate Then(string, StepDelegate<TState>) occurrence has no statically provable action reference");
                    break;

                case "EscalateTo" when containingType == "IApprovalEscalationBuilder":
                    failures.Add(
                        "nested EscalateTo approval is not represented by the current closed workflow proof");
                    break;
            }
        }

        if (workflowOnFailureCount > 1)
        {
            failures.Add(
                "workflow contains duplicate OnFailure callbacks; runtime rejects the second declaration");
        }

        return failures
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static void InspectInvocationOwnership(
        FluentDslParseContext context,
        ImmutableArray<string>.Builder failures)
    {
        var mainChain = new HashSet<InvocationExpressionSyntax>();
        ExpressionSyntax current = context.FinallyInvocation!;
        while (current is InvocationExpressionSyntax invocation)
        {
            mainChain.Add(invocation);
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                break;
            }

            current = StripTransparent(memberAccess.Expression);
        }

        foreach (var invocation in context.AllInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!TryGetDslOperation(context, invocation, out var operation))
            {
                var methodName = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => string.Empty,
                };
                if (ModelAffectingMethodNames.Contains(methodName))
                {
                    failures.Add(
                        $"workflow Definition contains model-affecting call '{methodName}' that does not resolve to the Strategos DSL");
                }

                continue;
            }

            if (mainChain.Contains(invocation))
            {
                continue;
            }

            var containingType = operation.TargetMethod.ContainingType.OriginalDefinition.Name;
            if ((string.Equals(operation.TargetMethod.Name, "Create", StringComparison.Ordinal)
                    && string.Equals(containingType, "Workflow", StringComparison.Ordinal))
                || (string.Equals(operation.TargetMethod.Name, "Finally", StringComparison.Ordinal)
                    && string.Equals(containingType, "IWorkflowBuilder", StringComparison.Ordinal)))
            {
                failures.Add(
                    "workflow Definition contains a nested Workflow<TState> construction outside the selected fluent chain");
                continue;
            }

            if (ModelAffectingMethodNames.Contains(operation.TargetMethod.Name)
                && !IsOwnedCallbackInvocation(context, invocation, mainChain, new HashSet<InvocationExpressionSyntax>()))
            {
                failures.Add(
                    $"workflow Definition contains model-affecting Strategos call '{operation.TargetMethod.Name}' that is not rooted in an owned inline builder callback");
            }
        }
    }

    private static bool IsOwnedCallbackInvocation(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        HashSet<InvocationExpressionSyntax> mainChain,
        HashSet<InvocationExpressionSyntax> visiting)
    {
        if (mainChain.Contains(invocation))
        {
            return true;
        }

        if (!visiting.Add(invocation)
            || !TryGetRootParameter(context, invocation, out var parameter, out var declaringLambda)
            || !IsBuilderCallbackParameter(parameter)
            || !TryGetDirectCallbackHost(declaringLambda, out var callbackHost))
        {
            return false;
        }

        if (TryGetDslOperation(context, callbackHost, out _))
        {
            return IsOwnedCallbackInvocation(context, callbackHost, mainChain, visiting);
        }

        // Branch path lambdas are wrapped by BranchCase.When/Otherwise before becoming direct
        // arguments of the owned Branch call. No other factory wrapper is part of the closed
        // topology grammar.
        if (!IsBranchCaseFactory(context, callbackHost)
            || !TryGetDirectExpressionHost(callbackHost, out var branchHost)
            || !TryGetDslOperation(context, branchHost, out var branchOperation)
            || !string.Equals(branchOperation.TargetMethod.Name, "Branch", StringComparison.Ordinal))
        {
            return false;
        }

        return IsOwnedCallbackInvocation(context, branchHost, mainChain, visiting);
    }

    private static bool TryGetRootParameter(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        out IParameterSymbol parameter,
        out LambdaExpressionSyntax declaringLambda)
    {
        parameter = null!;
        declaringLambda = null!;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        ExpressionSyntax receiver = StripTransparent(memberAccess.Expression);
        while (receiver is InvocationExpressionSyntax chained
            && chained.Expression is MemberAccessExpressionSyntax chainedAccess)
        {
            receiver = StripTransparent(chainedAccess.Expression);
        }

        if (receiver is not IdentifierNameSyntax identifier
            || context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol
                is not IParameterSymbol rootParameter)
        {
            return false;
        }

        // The nearest lambda must declare the root. A builder captured from an outer callback and
        // used inside an unrelated nested lambda is data-dependent execution, not owned topology.
        var nearestLambda = identifier.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (nearestLambda is null
            || !GetLambdaParameters(nearestLambda).Any(parameterSyntax =>
                context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken)
                    is IParameterSymbol declared
                && SymbolEqualityComparer.Default.Equals(declared, rootParameter)))
        {
            return false;
        }

        parameter = rootParameter;
        declaringLambda = nearestLambda;
        return true;
    }

    private static IEnumerable<ParameterSyntax> GetLambdaParameters(LambdaExpressionSyntax lambda) =>
        lambda switch
        {
            SimpleLambdaExpressionSyntax simple => [simple.Parameter],
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters,
            _ => [],
        };

    private static bool IsBuilderCallbackParameter(IParameterSymbol parameter) =>
        parameter.Type is INamedTypeSymbol type
        && string.Equals(type.ContainingNamespace.ToDisplayString(), BuildersNamespace, StringComparison.Ordinal)
        && string.Equals(type.ContainingAssembly.Name, "Strategos", StringComparison.Ordinal)
        && type.OriginalDefinition.Name is
            "IApprovalBuilder"
            or "IApprovalEscalationBuilder"
            or "IApprovalRejectionBuilder"
            or "IBranchBuilder"
            or "IFailureBuilder"
            or "IForkPathBuilder"
            or "ILoopBuilder"
            or "IStepConfiguration";

    private static bool TryGetDirectCallbackHost(
        LambdaExpressionSyntax lambda,
        out InvocationExpressionSyntax host) =>
        TryGetDirectExpressionHost(lambda, out host);

    private static bool TryGetDirectExpressionHost(
        ExpressionSyntax expression,
        out InvocationExpressionSyntax host)
    {
        ExpressionSyntax current = expression;
        while (current.Parent is ParenthesizedExpressionSyntax parenthesized
            && parenthesized.Expression == current)
        {
            current = parenthesized;
        }

        if (current.Parent is ArgumentSyntax
            {
                Parent: ArgumentListSyntax
                {
                    Parent: InvocationExpressionSyntax invocation,
                },
            })
        {
            host = invocation;
            return true;
        }

        host = null!;
        return false;
    }

    private static bool IsBranchCaseFactory(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation)
    {
        return context.SemanticModel.GetOperation(invocation, context.CancellationToken)
                is IInvocationOperation operation
            && operation.TargetMethod.Name is "When" or "Otherwise"
            && string.Equals(
                operation.TargetMethod.ContainingType.OriginalDefinition.Name,
                "BranchCase",
                StringComparison.Ordinal)
            && string.Equals(
                operation.TargetMethod.ContainingNamespace.ToDisplayString(),
                DefinitionsNamespace,
                StringComparison.Ordinal)
            && string.Equals(operation.TargetMethod.ContainingAssembly.Name, "Strategos", StringComparison.Ordinal);
    }

    private static void InspectWorkflowChain(
        FluentDslParseContext context,
        ImmutableArray<string>.Builder failures)
    {
        ExpressionSyntax current = context.FinallyInvocation!;
        var originatesAtCreate = false;
        while (current is InvocationExpressionSyntax invocation)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!TryGetDslOperation(context, invocation, out var operation))
            {
                var methodName = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => "<dynamic>",
                };
                failures.Add(
                    $"workflow fluent chain call '{methodName}' is outside the statically closed workflow grammar");
            }
            else if (string.Equals(operation.TargetMethod.Name, "Create", StringComparison.Ordinal)
                && string.Equals(
                    operation.TargetMethod.ContainingType.OriginalDefinition.Name,
                    "Workflow",
                    StringComparison.Ordinal))
            {
                originatesAtCreate = true;
                InspectWorkflowIdentity(context, operation, failures);
            }

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                break;
            }

            current = StripTransparent(memberAccess.Expression);
        }

        if (!originatesAtCreate)
        {
            failures.Add(
                "workflow fluent chain does not originate at a statically visible Workflow<TState>.Create call");
        }
    }

    private static void InspectWorkflowIdentity(
        FluentDslParseContext context,
        IInvocationOperation operation,
        ImmutableArray<string>.Builder failures)
    {
        var expectedName = context.WorkflowName;
        if (expectedName is null)
        {
            return;
        }

        var nameArgument = operation.Arguments.FirstOrDefault(argument => string.Equals(
            argument.Parameter?.Name,
            "name",
            StringComparison.Ordinal));
        if (nameArgument is null
            || !nameArgument.Value.ConstantValue.HasValue
            || nameArgument.Value.ConstantValue.Value is not string authoredName)
        {
            failures.Add(
                "Workflow<TState>.Create identity is not a compile-time constant string");
            return;
        }

        if (!string.Equals(authoredName, expectedName, StringComparison.Ordinal))
        {
            failures.Add(
                $"Workflow<TState>.Create identity '{authoredName}' does not match [Workflow] identity '{expectedName}'");
        }
    }

    private static bool IsDelegateStep(IMethodSymbol method)
    {
        if (method.IsGenericMethod || method.Parameters.Length != 2)
        {
            return false;
        }

        var delegateType = method.Parameters[1].Type;
        return method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && delegateType.TypeKind == TypeKind.Delegate
            && string.Equals(delegateType.OriginalDefinition.Name, "StepDelegate", StringComparison.Ordinal)
            && string.Equals(
                delegateType.ContainingNamespace.ToDisplayString(),
                "Strategos.Steps",
                StringComparison.Ordinal)
            && string.Equals(delegateType.ContainingAssembly.Name, "Strategos", StringComparison.Ordinal);
    }

    private static void InspectBranch(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        string containingType,
        ImmutableArray<string>.Builder failures)
    {
        if (!HasCanonicalSourceArgumentOrder(invocation, operation.TargetMethod))
        {
            failures.Add(
                "Branch arguments are not in the canonical source order required by current static lowering");
        }

        var discriminator = GetArguments(operation, "discriminator").FirstOrDefault();
        var supportsInvokedMember = string.Equals(containingType, "IWorkflowBuilder", StringComparison.Ordinal);
        if (discriminator is null
            || !IsClosedBranchDiscriminator(context, discriminator, supportsInvokedMember))
        {
            failures.Add("Branch discriminator is outside the statically closed branch grammar");
        }

        var cases = GetArguments(operation, "cases");
        var unresolvedCount = cases.Count(expression => !IsClosedBranchCase(context, expression));
        if (cases.Length == 0 || unresolvedCount > 0)
        {
            var count = cases.Length == 0 ? 1 : unresolvedCount;
            failures.Add(
                $"Branch contains {count} of {cases.Length} case declarations outside the statically closed branch grammar");
        }
    }

    private static void InspectLoop(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        ImmutableArray<string>.Builder failures)
    {
        var construct = operation.TargetMethod.Name;
        if (!HasCanonicalSourceArgumentOrder(invocation, operation.TargetMethod))
        {
            failures.Add(
                $"{construct} arguments are not in the canonical source order required by current static lowering");
        }

        var loopIdentity = GetArguments(operation, "loopName").FirstOrDefault();
        if (loopIdentity is null
            || loopIdentity is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            failures.Add($"{construct} loop identity is not a string literal");
        }

        if (!HasStaticallyKnownPositiveIntegerArgument(operation, "maxIterations"))
        {
            failures.Add($"{construct} maxIterations is not a statically known positive integer");
        }

        var body = GetArguments(operation, "body").FirstOrDefault();
        if (body is not LambdaExpressionSyntax lambda)
        {
            failures.Add($"{construct} body callback is not an inline lambda");
        }
        else if (!HasClosedBuilderUses(context, lambda, "ILoopBuilder"))
        {
            failures.Add($"{construct} body callback lets its builder escape the statically closed loop grammar");
        }
        else if (!ContainsStaticallyClosedLoopBodyStep(context, lambda))
        {
            failures.Add($"{construct} body callback does not contain a statically closed loop step");
        }
    }

    private static void InspectFork(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        ImmutableArray<string>.Builder failures)
    {
        if (!HasCanonicalSourceArgumentOrder(invocation, operation.TargetMethod))
        {
            failures.Add(
                "Fork arguments are not in the canonical source order required by current static lowering");
        }

        var paths = GetArguments(operation, "paths");
        if (paths.Length < 2)
        {
            failures.Add(
                $"Fork contains {paths.Length} statically visible path callbacks; at least two are required");
        }

        var unresolvedCount = paths.Count(expression =>
            expression is not LambdaExpressionSyntax lambda
            || !HasClosedBuilderUses(context, lambda, "IForkPathBuilder")
            || !ContainsOwnBuilderInvocation(context, lambda, "IForkPathBuilder", "Then"));
        if (unresolvedCount > 0)
        {
            failures.Add(
                $"Fork contains {unresolvedCount} of {paths.Length} path callbacks outside the statically closed fork grammar");
        }
    }

    private static void InspectFailureHandler(
        FluentDslParseContext context,
        IInvocationOperation operation,
        string containingType,
        ImmutableArray<string>.Builder failures)
    {
        var scope = string.Equals(containingType, "IWorkflowBuilder", StringComparison.Ordinal)
            ? "workflow"
            : "fork-path";
        var handler = GetArguments(operation, "handler").FirstOrDefault();
        if (handler is not LambdaExpressionSyntax lambda)
        {
            failures.Add($"{scope} OnFailure callback is not an inline lambda");
            return;
        }

        if (!HasClosedBuilderUses(context, lambda, "IFailureBuilder"))
        {
            failures.Add($"{scope} OnFailure callback lets its builder escape the statically closed handler grammar");
            return;
        }

        if (!ContainsOwnBuilderInvocation(context, lambda, "IFailureBuilder", "Then"))
        {
            failures.Add($"{scope} OnFailure callback does not contain a statically closed handler chain");
            return;
        }

        if (string.Equals(scope, "workflow", StringComparison.Ordinal)
            && !ContainsOwnBuilderInvocation(context, lambda, "IFailureBuilder", "Complete"))
        {
            failures.Add(
                "workflow OnFailure is nonterminal; current runtime lowering cannot rejoin the main flow");
        }
    }

    private static void InspectLowConfidenceHandler(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        ImmutableArray<string>.Builder failures)
    {
        if (invocation.Ancestors().OfType<InvocationExpressionSyntax>().Any(ancestor =>
            TryGetDslOperation(context, ancestor, out var ancestorOperation)
            && string.Equals(
                ancestorOperation.TargetMethod.Name,
                "OnLowConfidence",
                StringComparison.Ordinal)))
        {
            failures.Add(
                "nested OnLowConfidence handlers are not supported by the current runtime lowering");
            return;
        }

        var handler = GetArguments(operation, "handler").FirstOrDefault();
        if (handler is not LambdaExpressionSyntax lambda)
        {
            failures.Add("OnLowConfidence callback is not an inline lambda");
            return;
        }

        if (!HasClosedBuilderUses(context, lambda, "IBranchBuilder"))
        {
            failures.Add("OnLowConfidence callback lets its builder escape the statically closed handler grammar");
            return;
        }

        if (!ContainsOwnBuilderInvocation(context, lambda, "IBranchBuilder", "Then"))
        {
            failures.Add("OnLowConfidence callback does not contain a statically closed handler chain");
        }
    }

    private static void InspectApproval(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        ImmutableArray<string>.Builder failures)
    {
        if (!HasCanonicalSourceArgumentOrder(invocation, operation.TargetMethod))
        {
            failures.Add(
                "AwaitApproval arguments are not in the canonical source order required by current static lowering");
        }

        var configure = GetArguments(operation, "configure").FirstOrDefault();
        if (configure is not LambdaExpressionSyntax lambda)
        {
            failures.Add("AwaitApproval configuration is not an inline lambda");
        }
        else if (!HasClosedBuilderUses(context, lambda, "IApprovalBuilder"))
        {
            failures.Add("AwaitApproval configuration lets its builder escape the statically closed approval grammar");
        }

        if (configure is LambdaExpressionSyntax configuration)
        {
            InspectDuplicateApprovalHandlers(context, configuration, failures);
        }
    }

    private static void InspectDuplicateApprovalHandlers(
        FluentDslParseContext context,
        LambdaExpressionSyntax configuration,
        ImmutableArray<string>.Builder failures)
    {
        var rejectionCount = 0;
        var timeoutCount = 0;
        foreach (var invocation in InvocationChainWalker.CollectInvocationsInLambda(configuration))
        {
            if (!TryGetDslOperation(context, invocation, out var operation)
                || !string.Equals(
                    operation.TargetMethod.ContainingType.OriginalDefinition.Name,
                    "IApprovalBuilder",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(operation.TargetMethod.Name, "OnRejection", StringComparison.Ordinal))
            {
                rejectionCount++;
            }
            else if (string.Equals(operation.TargetMethod.Name, "OnTimeout", StringComparison.Ordinal))
            {
                timeoutCount++;
            }
        }

        if (rejectionCount > 1)
        {
            failures.Add(
                "AwaitApproval configuration contains duplicate OnRejection callbacks; runtime last-wins semantics are outside current static lowering");
        }

        if (timeoutCount > 1)
        {
            failures.Add(
                "AwaitApproval configuration contains duplicate OnTimeout callbacks; runtime last-wins semantics are outside current static lowering");
        }
    }

    private static void InspectApprovalHandler(
        FluentDslParseContext context,
        IInvocationOperation operation,
        string construct,
        string builderType,
        ImmutableArray<string>.Builder failures)
    {
        var configure = GetArguments(operation, "configure").FirstOrDefault();
        if (configure is not LambdaExpressionSyntax lambda)
        {
            failures.Add($"{construct} callback is not an inline lambda");
            return;
        }

        if (!HasClosedBuilderUses(context, lambda, builderType))
        {
            failures.Add($"{construct} callback lets its builder escape the statically closed handler grammar");
            return;
        }

        var hasStep = ContainsOwnBuilderInvocation(context, lambda, builderType, "Then");
        var hasNestedApproval = string.Equals(construct, "OnTimeout", StringComparison.Ordinal)
            && ContainsOwnBuilderInvocation(context, lambda, builderType, "EscalateTo");
        if (!hasStep && !hasNestedApproval)
        {
            failures.Add($"{construct} callback does not contain a statically closed handler chain");
        }
    }

    private static bool IsClosedBranchDiscriminator(
        FluentDslParseContext context,
        ExpressionSyntax expression,
        bool supportsInvokedMember)
    {
        if (expression is LambdaExpressionSyntax lambda)
        {
            return lambda.Body is MemberAccessExpressionSyntax
                || (supportsInvokedMember
                    && lambda.Body is InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax,
                    });
        }

        return expression is IdentifierNameSyntax identifier
            && context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is IMethodSymbol;
    }

    private static bool IsClosedBranchCase(
        FluentDslParseContext context,
        ExpressionSyntax expression)
    {
        if (expression is not InvocationExpressionSyntax invocation
            || context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation
            || operation.TargetMethod.Name is not ("When" or "Otherwise")
            || !string.Equals(
                operation.TargetMethod.ContainingNamespace.ToDisplayString(),
                DefinitionsNamespace,
                StringComparison.Ordinal)
            || !string.Equals(
                operation.TargetMethod.ContainingType.OriginalDefinition.Name,
                "BranchCase",
                StringComparison.Ordinal)
            || !HasCanonicalSourceArgumentOrder(invocation, operation.TargetMethod))
        {
            return false;
        }

        var pathBuilder = GetArguments(operation, "pathBuilder").FirstOrDefault();
        return pathBuilder is LambdaExpressionSyntax lambda
            && HasClosedBuilderUses(context, lambda, "IBranchBuilder")
            && ContainsOwnBuilderInvocation(context, lambda, "IBranchBuilder", "Then");
    }

    private static bool HasClosedBuilderUses(
        FluentDslParseContext context,
        LambdaExpressionSyntax lambda,
        string builderType)
    {
        if (lambda.Body is BlockSyntax block
            && block.Statements.Any(statement => statement is not ExpressionStatementSyntax))
        {
            return false;
        }

        var parameterSyntax = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized
                when parenthesized.ParameterList.Parameters.Count == 1 =>
                parenthesized.ParameterList.Parameters[0],
            _ => null,
        };
        var parameter = parameterSyntax is null
            ? null
            : context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken)
                as IParameterSymbol;
        if (parameter is null)
        {
            return false;
        }

        foreach (var identifier in lambda.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol
                    is not IParameterSymbol referenced
                || !SymbolEqualityComparer.Default.Equals(referenced, parameter))
            {
                continue;
            }

            if (identifier.Ancestors()
                    .TakeWhile(ancestor => ancestor != lambda)
                    .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax
                        or LocalFunctionStatementSyntax)
                || !IsClosedBuilderUse(context, identifier, lambda, builderType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClosedBuilderUse(
        FluentDslParseContext context,
        IdentifierNameSyntax identifier,
        LambdaExpressionSyntax lambda,
        string builderType)
    {
        ExpressionSyntax current = identifier;
        while (true)
        {
            current = SyntaxHelper.IncludeTransparentParents(current);

            if (current.Parent is MemberAccessExpressionSyntax member
                && member.Expression == current
                && member.Parent is InvocationExpressionSyntax invocation)
            {
                if (!TryGetDslOperation(context, invocation, out var operation)
                    || !IsAllowedBuilderType(
                        builderType,
                        operation.TargetMethod.ContainingType.OriginalDefinition.Name))
                {
                    return false;
                }

                current = invocation;
                continue;
            }

            return current.Parent is ExpressionStatementSyntax
                || ReferenceEquals(current.Parent, lambda);
        }
    }

    private static bool IsAllowedBuilderType(string expectedType, string actualType) =>
        string.Equals(expectedType, actualType, StringComparison.Ordinal)
        || (string.Equals(expectedType, "ILoopBuilder", StringComparison.Ordinal)
            && string.Equals(actualType, "ILoopForkJoinBuilder", StringComparison.Ordinal));

    private static ExpressionSyntax StripTransparent(ExpressionSyntax expression) =>
        SyntaxHelper.StripTransparent(expression);

    private static bool ContainsOwnBuilderInvocation(
        FluentDslParseContext context,
        LambdaExpressionSyntax lambda,
        string containingType,
        string methodName)
    {
        foreach (var invocation in InvocationChainWalker.CollectInvocationsInLambda(lambda))
        {
            if (TryGetDslOperation(context, invocation, out var operation)
                && string.Equals(operation.TargetMethod.Name, methodName, StringComparison.Ordinal)
                && string.Equals(
                    operation.TargetMethod.ContainingType.OriginalDefinition.Name,
                    containingType,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsStaticallyClosedLoopBodyStep(
        FluentDslParseContext context,
        LambdaExpressionSyntax lambda)
    {
        foreach (var invocation in InvocationChainWalker.CollectInvocationsInLambda(lambda))
        {
            if (!TryGetDslOperation(context, invocation, out var operation))
            {
                continue;
            }

            var containingType = operation.TargetMethod.ContainingType.OriginalDefinition.Name;
            if ((string.Equals(containingType, "ILoopBuilder", StringComparison.Ordinal)
                    && operation.TargetMethod.Name is "Then" or "RepeatUntil" or "Fork")
                || (string.Equals(containingType, "ILoopForkJoinBuilder", StringComparison.Ordinal)
                    && string.Equals(operation.TargetMethod.Name, "Join", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStaticallyKnownPositiveIntegerArgument(
        IInvocationOperation operation,
        string parameterName)
    {
        var argument = operation.Arguments.FirstOrDefault(candidate => string.Equals(
            candidate.Parameter?.Name,
            parameterName,
            StringComparison.Ordinal));
        if (argument is not null)
        {
            return argument.Value.ConstantValue.HasValue
                && argument.Value.ConstantValue.Value is int value
                && value > 0;
        }

        var parameter = operation.TargetMethod.Parameters.FirstOrDefault(candidate => string.Equals(
            candidate.Name,
            parameterName,
            StringComparison.Ordinal));
        return parameter?.HasExplicitDefaultValue == true
            && parameter.ExplicitDefaultValue is int defaultValue
            && defaultValue > 0;
    }

    private static ImmutableArray<ExpressionSyntax> GetArguments(
        IInvocationOperation operation,
        string parameterName)
    {
        var result = ImmutableArray.CreateBuilder<ExpressionSyntax>();
        foreach (var argument in operation.Arguments.Where(argument =>
            string.Equals(argument.Parameter?.Name, parameterName, StringComparison.Ordinal)))
        {
            if (argument.IsImplicit
                && argument.ArgumentKind == ArgumentKind.ParamArray
                && argument.Value is IArrayCreationOperation
                {
                    Initializer: { } initializer,
                })
            {
                foreach (var element in initializer.ElementValues)
                {
                    if (element.Syntax is ExpressionSyntax elementExpression)
                    {
                        result.Add(elementExpression);
                    }
                }

                continue;
            }

            if (argument.IsImplicit)
            {
                continue;
            }

            var expression = argument.Syntax is ArgumentSyntax syntax
                ? syntax.Expression
                : argument.Value.Syntax as ExpressionSyntax;
            if (expression is not null)
            {
                result.Add(expression);
            }
        }

        return result.ToImmutable();
    }

    private static bool HasCanonicalSourceArgumentOrder(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method)
    {
        var parameters = method.Parameters;
        for (var index = 0; index < invocation.ArgumentList.Arguments.Count; index++)
        {
            var argument = invocation.ArgumentList.Arguments[index];
            var expectedParameter = index < parameters.Length
                ? parameters[index]
                : parameters.LastOrDefault(candidate => candidate.IsParams);
            IParameterSymbol? actualParameter;
            if (argument.NameColon is { } nameColon)
            {
                var parameterName = nameColon.Name.Identifier.ValueText;
                actualParameter = parameters.FirstOrDefault(candidate => string.Equals(
                    candidate.Name,
                    parameterName,
                    StringComparison.Ordinal));
            }
            else
            {
                actualParameter = expectedParameter;
            }

            if (expectedParameter is null
                || actualParameter is null
                || actualParameter.Ordinal != expectedParameter.Ordinal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetDslOperation(
        FluentDslParseContext context,
        InvocationExpressionSyntax invocation,
        out IInvocationOperation operation)
    {
        operation = null!;
        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation candidate
            || !string.Equals(
                candidate.TargetMethod.ContainingNamespace.ToDisplayString(),
                BuildersNamespace,
                StringComparison.Ordinal)
            || !string.Equals(
                candidate.TargetMethod.ContainingAssembly.Name,
                "Strategos",
                StringComparison.Ordinal))
        {
            return false;
        }

        operation = candidate;
        return true;
    }
}
