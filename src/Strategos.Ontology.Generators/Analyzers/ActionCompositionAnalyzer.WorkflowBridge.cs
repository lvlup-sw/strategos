using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using Strategos.Ontology.ActionLogic;

namespace Strategos.Ontology.Generators.Analyzers;

/// <summary>
/// Shared, source-link-safe projections of the action parser used by workflow
/// binding proof. Keeping these entry points on the composition analyzer means
/// typed predicates have one Roslyn grammar in both analyzer assemblies.
/// </summary>
internal static partial class ActionCompositionAnalyzer
{
    /// <summary>Parses a typed or expression-tree action predicate for workflow proof.</summary>
    internal static bool TryParseWorkflowPredicate(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out WorkflowPredicateSyntax predicate)
    {
        var resolving = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (TryParsePredicate(expression, semanticModel, cancellationToken, resolving, out var typed))
        {
            predicate = Project(typed);
            return true;
        }

        var candidate = Unwrap(expression);
        if (candidate is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(
                identifier,
                semanticModel,
                cancellationToken,
                resolving,
                out var initializer))
        {
            candidate = Unwrap(initializer);
        }

        if (candidate is not LambdaExpressionSyntax lambda
            || lambda.Body is not ExpressionSyntax body)
        {
            predicate = WorkflowPredicateSyntax.Dynamic;
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
            : semanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) as IParameterSymbol;
        if (parameter is null)
        {
            predicate = WorkflowPredicateSyntax.Invalid("an action-predicate lambda must have one subject parameter");
            return true;
        }

        if (!IsSupportedExpressionPredicate(
                body,
                parameter,
                semanticModel,
                cancellationToken,
                topLevel: true,
                out var failureReason))
        {
            predicate = WorkflowPredicateSyntax.Invalid(
                failureReason
                ?? "use the closed property-to-literal AND/OR/NOT grammar or explicit ActionPredicate.Custom");
            return true;
        }

        if (!TryTranslateWorkflowExpression(
                body,
                parameter,
                semanticModel,
                cancellationToken,
                out var translated,
                out failureReason))
        {
            predicate = WorkflowPredicateSyntax.Invalid(
                failureReason ?? "the action-predicate expression could not be translated");
            return true;
        }

        predicate = Project(translated);
        return true;
    }

    /// <summary>Parses a direct, statically immutable ActionDescriptor construction.</summary>
    internal static bool TryParseWorkflowActionDescriptor(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out WorkflowActionContractSyntax action,
        out string? unresolvedReason)
    {
        var bindingFailure = TryParseDescriptorWorkflowBinding(
            expression,
            semanticModel,
            cancellationToken,
            out var hasWorkflowBinding,
            out var boundWorkflow);
        var compensationFailure = TryParseDescriptorCompensatingAction(
            expression,
            semanticModel,
            cancellationToken,
            out var compensatingActionName);
        if (!TryParseAction(
                expression,
                semanticModel,
                cancellationToken,
                new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                out var parsed,
                out unresolvedReason))
        {
            if (hasWorkflowBinding
                && TryParseWorkflowActionIdentity(
                    expression,
                    semanticModel,
                    cancellationToken,
                    out var identity,
                    out var identityFailure))
            {
                action = new WorkflowActionContractSyntax(
                    identity.DomainName,
                    identity.ObjectTypeName,
                    identity.ActionName,
                    hasWorkflowBinding: true,
                    boundWorkflow,
                    WorkflowPredicateSyntax.All(Array.Empty<WorkflowPredicateSyntax>()),
                    WorkflowPredicateSyntax.All(Array.Empty<WorkflowPredicateSyntax>()),
                    ImmutableArray<string>.Empty,
                    requiredAuthority: null,
                    compensatingActionName,
                    unresolvedReason
                        ?? identityFailure
                        ?? bindingFailure
                        ?? compensationFailure
                        ?? "the workflow-bound action contract is not statically closed",
                    identity.Location);
                return true;
            }

            action = null!;
            return false;
        }

        action = new WorkflowActionContractSyntax(
            parsed.DomainName,
            parsed.ObjectTypeName,
            parsed.Name,
            hasWorkflowBinding,
            boundWorkflow,
            Project(parsed.Requirement),
            Project(parsed.Guarantee),
            parsed.Frame.OrderBy(resource => resource, StringComparer.Ordinal).ToImmutableArray(),
            parsed.RequiredAuthority,
            compensatingActionName,
            parsed.InvalidReason ?? bindingFailure ?? compensationFailure,
            parsed.Location);
        return true;
    }

    /// <summary>Parses one statically closed fluent <c>Action(...)</c> declaration.</summary>
    internal static bool TryParseWorkflowFluentAction(
        InvocationExpressionSyntax actionInvocation,
        string domainName,
        string objectTypeName,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out WorkflowActionContractSyntax action)
    {
        var actionNameExpression = actionInvocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        var actionName = "<dynamic-action-name>";
        var actionNameStatus = StaticParseKind.Dynamic;
        if (actionNameExpression is not null)
        {
            actionNameStatus = TryParseRequiredString(
                actionNameExpression,
                semanticModel,
                cancellationToken,
                out actionName);
        }

        string? invalidReason = actionNameStatus switch
        {
            StaticParseKind.Dynamic => "the workflow-bound action name is dynamic",
            StaticParseKind.Invalid => "an action name cannot be empty",
            _ => null,
        };
        if (actionNameStatus != StaticParseKind.Success)
        {
            actionName = actionNameStatus == StaticParseKind.Dynamic
                ? "<dynamic-action-name>"
                : "<invalid-action-name>";
        }

        var hasWorkflowBinding = false;
        string? boundWorkflow = null;
        string? requiredAuthority = null;
        string? compensatingActionName = null;
        var requirements = new List<WorkflowPredicateSyntax>();
        var guarantees = new List<WorkflowPredicateSyntax>();
        var frame = new HashSet<string>(StringComparer.Ordinal);

        foreach (var invocation in EnumerateWorkflowFluentChain(actionInvocation))
        {
            var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (invocation == actionInvocation)
            {
                continue;
            }

            if (!IsWorkflowActionBuilderMethod(method))
            {
                invalidReason ??= method is null
                    ? "a fluent action call cannot be resolved statically"
                    : $"fluent action call '{method.ToDisplayString()}' is outside the closed action-builder grammar";
                continue;
            }

            var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
            switch (method.Name)
            {
                case "BoundToWorkflow":
                    hasWorkflowBinding = true;
                    if (!TryParseWorkflowBindingArgument(
                        invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression,
                        semanticModel,
                        cancellationToken,
                        out boundWorkflow,
                        out var bindingReason))
                    {
                        invalidReason ??= bindingReason ?? "the bound workflow identity is dynamic";
                    }

                    break;
                case "BoundToTool":
                    hasWorkflowBinding = false;
                    boundWorkflow = null;
                    break;
                case "Requires":
                case "Ensures":
                    var predicateExpression = FindArgumentExpression(invocation, operation, "predicate");
                    if (predicateExpression is null
                        || !TryParseWorkflowPredicate(
                            predicateExpression,
                            semanticModel,
                            cancellationToken,
                            out var predicate))
                    {
                        predicate = WorkflowPredicateSyntax.Dynamic;
                    }

                    (method.Name == "Requires" ? requirements : guarantees).Add(predicate);
                    break;
                case "RequiresSoft":
                    var softPredicateExpression = FindArgumentExpression(invocation, operation, "predicate");
                    if (softPredicateExpression is null
                        || !TryParseWorkflowPredicate(
                            softPredicateExpression,
                            semanticModel,
                            cancellationToken,
                            out var softPredicate))
                    {
                        softPredicate = WorkflowPredicateSyntax.Dynamic;
                    }

                    if (softPredicate.InvalidReason is not null
                        && softPredicate.OpaqueKeys.IsEmpty)
                    {
                        invalidReason ??= softPredicate.InvalidReason;
                    }

                    break;
                case "RequiresLink":
                case "EnsuresLink":
                    if (TryParseFluentRequiredString(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out var linkName,
                        out var linkReason))
                    {
                        var link = WorkflowPredicateSyntax.BooleanAtom("link|" + linkName, "link|" + linkName);
                        (method.Name == "RequiresLink" ? requirements : guarantees).Add(link);
                    }
                    else
                    {
                        invalidReason ??= linkReason;
                    }

                    break;
                case "RequiresLinkSoft":
                    if (!TryParseFluentRequiredString(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out _,
                        out var softLinkReason))
                    {
                        invalidReason ??= softLinkReason;
                    }

                    break;
                case "RequiresRelation":
                case "EnsuresRelation":
                    if (TryParseFluentRelation(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out var relation,
                        out var relationReason))
                    {
                        (method.Name == "RequiresRelation" ? requirements : guarantees).Add(relation);
                    }
                    else
                    {
                        invalidReason ??= relationReason;
                    }

                    break;
                case "Touches":
                    var resourceExpression = FindArgumentExpression(invocation, operation, "resource");
                    string? resourceReason = null;
                    if (resourceExpression is null
                        || TryParseActionResource(
                            resourceExpression,
                            semanticModel,
                            cancellationToken,
                            new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                            out var resourceKind,
                            out var resourceName,
                            out resourceReason) != StaticParseKind.Success)
                    {
                        invalidReason ??= resourceReason ?? "an action frame resource is dynamic";
                    }
                    else
                    {
                        frame.Add(ResourceKey(resourceKind, resourceName));
                    }

                    break;
                case "Modifies":
                    var selectorExpression = FindArgumentExpression(invocation, operation, "propertySelector");
                    if (!TryParseFluentPropertySelector(
                        selectorExpression,
                        semanticModel,
                        cancellationToken,
                        out var propertyName))
                    {
                        invalidReason ??= "a modified property selector is dynamic or indirect";
                    }
                    else
                    {
                        frame.Add("property|" + propertyName);
                    }

                    break;
                case "CreatesLinked":
                    if (TryParseFluentRequiredString(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out var createdLink,
                        out var createdLinkReason))
                    {
                        var resource = "link|" + createdLink;
                        frame.Add(resource);
                        guarantees.Add(WorkflowPredicateSyntax.BooleanAtom(resource, resource));
                    }
                    else
                    {
                        invalidReason ??= createdLinkReason;
                    }

                    break;
                case "EmitsEvent":
                    var eventType = method.TypeArguments.FirstOrDefault();
                    if (eventType is null || string.IsNullOrWhiteSpace(eventType.MetadataName))
                    {
                        invalidReason ??= "an emitted event type could not be resolved";
                    }
                    else
                    {
                        frame.Add("event|" + eventType.MetadataName);
                    }

                    break;
                case "RequiresAuthority":
                    if (!TryParseFluentRequiredString(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out requiredAuthority,
                        out var authorityReason))
                    {
                        invalidReason ??= authorityReason;
                    }

                    break;
                case "CompensatedBy":
                    if (!TryParseFluentRequiredString(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out compensatingActionName,
                        out var compensationReason))
                    {
                        invalidReason ??= compensationReason;
                    }

                    break;
                case "Description":
                case "Accepts":
                case "Returns":
                case "ReadOnly":
                case "Idempotent":
                case "ValidFromState":
                    break;
                default:
                    invalidReason ??= $"action-builder member '{method.Name}' has no closed workflow-proof interpretation";
                    break;
            }
        }

        var requirement = WorkflowPredicateSyntax.All(requirements);
        var guarantee = WorkflowPredicateSyntax.All(guarantees);
        action = new WorkflowActionContractSyntax(
            domainName,
            objectTypeName,
            actionName,
            hasWorkflowBinding,
            boundWorkflow,
            requirement,
            guarantee,
            frame.OrderBy(resource => resource, StringComparer.Ordinal).ToImmutableArray(),
            requiredAuthority,
            compensatingActionName,
            invalidReason ?? requirement.InvalidReason ?? guarantee.InvalidReason,
            actionInvocation.GetLocation());
        return true;
    }

    /// <summary>
    /// Parses a standalone workflow-binding call. Such a call is unsupported
    /// unless it belongs to the direct fluent chain rooted at <c>Action(...)</c>;
    /// the catalog uses this method to inventory and reject otherwise-hidden
    /// bindings instead of silently omitting them.
    /// </summary>
    internal static bool TryParseStandaloneWorkflowBinding(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string? workflowName,
        out string? failureReason)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (!IsWorkflowActionBuilderMethod(method)
            || !string.Equals(method!.Name, "BoundToWorkflow", StringComparison.Ordinal))
        {
            workflowName = null;
            failureReason = null;
            return false;
        }

        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        var argument = FindArgumentExpression(invocation, operation, "workflowName")
            ?? FindArgumentExpression(invocation, operation, "workflow");
        if (!TryParseWorkflowBindingArgument(
            argument,
            semanticModel,
            cancellationToken,
            out workflowName,
            out failureReason))
        {
            failureReason ??= "the workflow binding is dynamic";
        }

        return true;
    }

    internal static IEnumerable<InvocationExpressionSyntax> EnumerateWorkflowFluentChain(
        InvocationExpressionSyntax root)
    {
        yield return root;
        ExpressionSyntax current = root;
        while (true)
        {
            var receiver = SkipTransparentFluentWrappers(current);
            if (receiver.Parent is not MemberAccessExpressionSyntax member
                || member.Expression != receiver
                || member.Parent is not InvocationExpressionSyntax invocation)
            {
                yield break;
            }

            yield return invocation;
            current = invocation;
        }
    }

    private static ExpressionSyntax SkipTransparentFluentWrappers(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            switch (current.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesized
                    when parenthesized.Expression == current:
                    current = parenthesized;
                    continue;
                case CastExpressionSyntax cast when cast.Expression == current:
                    current = cast;
                    continue;
                case PostfixUnaryExpressionSyntax suppress
                    when suppress.IsKind(SyntaxKind.SuppressNullableWarningExpression)
                        && suppress.Operand == current:
                    current = suppress;
                    continue;
                case CheckedExpressionSyntax checkedExpression
                    when checkedExpression.Expression == current:
                    current = checkedExpression;
                    continue;
                default:
                    return current;
            }
        }
    }

    private static bool IsWorkflowActionBuilderMethod(IMethodSymbol? method)
    {
        if (method?.ContainingType is not INamedTypeSymbol containingType
            || !string.Equals(containingType.Name, "IActionBuilder", StringComparison.Ordinal)
            || !string.Equals(
                containingType.ContainingNamespace.ToDisplayString(),
                "Strategos.Ontology.Builder",
                StringComparison.Ordinal))
        {
            return false;
        }

        return containingType.Arity is 0 or 1;
    }

    private static string? TryParseDescriptorWorkflowBinding(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out bool hasWorkflowBinding,
        out string? workflowName)
    {
        hasWorkflowBinding = false;
        workflowName = null;
        if (Unwrap(expression) is not BaseObjectCreationExpressionSyntax creation
            || creation.Initializer is null)
        {
            return null;
        }

        var bindingType = "Unbound";
        var bindingTypeIsWorkflow = false;
        var bindingTypeExpression = FindInitializerValue(creation.Initializer, "BindingType");
        if (bindingTypeExpression is not null)
        {
            var bindingTypeStatus = TryResolveEnumMemberName(
                bindingTypeExpression,
                semanticModel,
                cancellationToken,
                DescriptorNamespace + ".ActionBindingType",
                out bindingType);
            if (bindingTypeStatus == StaticParseKind.Dynamic)
            {
                hasWorkflowBinding = true;
                return "the action binding type is dynamic";
            }

            if (bindingTypeStatus == StaticParseKind.Invalid)
            {
                hasWorkflowBinding = true;
                return "the action binding type is invalid";
            }

            bindingTypeIsWorkflow = string.Equals(
                bindingType,
                "Workflow",
                StringComparison.Ordinal);
            hasWorkflowBinding = bindingTypeIsWorkflow;
        }

        var workflowExpression = FindInitializerValue(creation.Initializer, "BoundWorkflow");
        if (workflowExpression is null)
        {
            return hasWorkflowBinding
                ? "a workflow-bound action must name a workflow"
                : null;
        }

        // BoundWorkflow is semantically a binding declaration even when the discriminator is
        // inconsistent. Retain it in the inventory so the generator fails closed, but never
        // certify a workflow contract the runtime will not dispatch as a workflow.
        hasWorkflowBinding = true;
        if (!bindingTypeIsWorkflow)
        {
            return $"BoundWorkflow is present while BindingType is '{bindingType}'";
        }

        if (Unwrap(workflowExpression).IsKind(SyntaxKind.NullLiteralExpression))
        {
            return "a workflow-bound action must name a workflow";
        }

        return TryParseWorkflowBindingArgument(
            workflowExpression,
            semanticModel,
            cancellationToken,
            out workflowName,
            out var reason)
            ? null
            : reason;
    }

    private static string? TryParseDescriptorCompensatingAction(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string? compensatingActionName)
    {
        compensatingActionName = null;
        if (Unwrap(expression) is not BaseObjectCreationExpressionSyntax creation
            || creation.Initializer is null)
        {
            return null;
        }

        var compensationExpression = FindInitializerValue(
            creation.Initializer,
            "CompensatingActionName");
        if (compensationExpression is null
            || Unwrap(compensationExpression).IsKind(SyntaxKind.NullLiteralExpression))
        {
            return null;
        }

        var status = TryParseRequiredString(
            compensationExpression,
            semanticModel,
            cancellationToken,
            out var parsed);
        if (status == StaticParseKind.Success)
        {
            compensatingActionName = parsed;
            return null;
        }

        return status == StaticParseKind.Invalid
            ? "a compensating action name cannot be empty"
            : "the compensating action name is dynamic";
    }

    private static bool TryParseWorkflowActionIdentity(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out WorkflowActionIdentitySyntax identity,
        out string? failureReason)
    {
        if (Unwrap(expression) is not BaseObjectCreationExpressionSyntax creation
            || creation.ArgumentList is null)
        {
            identity = null!;
            failureReason = null;
            return false;
        }

        var operation = semanticModel.GetOperation(creation, cancellationToken) as IObjectCreationOperation;
        var subjectExpression = FindArgumentExpression(
            creation.ArgumentList,
            operation,
            "subject",
            position: 0);
        var nameExpression = FindArgumentExpression(
            creation.ArgumentList,
            operation,
            "name",
            position: 1);
        var resolving = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (subjectExpression is null
            || !TryParseSubject(
                subjectExpression,
                semanticModel,
                cancellationToken,
                resolving,
                out var subject))
        {
            identity = new WorkflowActionIdentitySyntax(
                "<dynamic-domain>",
                "<dynamic-object>",
                "<dynamic-action-name>",
                creation.GetLocation());
            failureReason = "the workflow-bound action subject is dynamic";
            return true;
        }

        var actionName = "<dynamic-action-name>";
        var nameStatus = StaticParseKind.Dynamic;
        if (nameExpression is not null)
        {
            nameStatus = TryParseRequiredString(
                nameExpression,
                semanticModel,
                cancellationToken,
                out actionName);
        }

        if (nameStatus != StaticParseKind.Success)
        {
            actionName = nameStatus == StaticParseKind.Invalid
                ? "<invalid-action-name>"
                : "<dynamic-action-name>";
        }

        identity = new WorkflowActionIdentitySyntax(
            subject.DomainName,
            subject.ObjectTypeName,
            actionName,
            creation.GetLocation());
        var nameFailure = nameStatus switch
        {
            StaticParseKind.Dynamic => "the workflow-bound action name is dynamic",
            StaticParseKind.Invalid => "an action name cannot be empty",
            _ => null,
        };
        failureReason = subject.InvalidReason ?? nameFailure;
        return true;
    }

    private static bool TryParseWorkflowBindingArgument(
        ExpressionSyntax? expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string? workflowName,
        out string? failureReason)
    {
        workflowName = null;
        failureReason = null;
        if (expression is null)
        {
            failureReason = "a workflow binding must name a workflow";
            return false;
        }

        expression = Unwrap(expression);
        if (expression is IdentifierNameSyntax identifier
            && TryResolveImmutableLocal(
                identifier,
                semanticModel,
                cancellationToken,
                new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                out var initializer))
        {
            return TryParseWorkflowBindingArgument(
                initializer,
                semanticModel,
                cancellationToken,
                out workflowName,
                out failureReason);
        }

        var stringStatus = TryParseRequiredString(
            expression,
            semanticModel,
            cancellationToken,
            out var directName);
        if (stringStatus == StaticParseKind.Success)
        {
            workflowName = directName;
            return true;
        }

        if (expression is BaseObjectCreationExpressionSyntax creation
            && IsType(
                semanticModel.GetTypeInfo(creation, cancellationToken).Type,
                DescriptorNamespace + ".WorkflowBindingReference")
            && creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is ExpressionSyntax nameExpression)
        {
            var nameStatus = TryParseRequiredString(
                nameExpression,
                semanticModel,
                cancellationToken,
                out var constructedName);
            if (nameStatus == StaticParseKind.Success)
            {
                workflowName = constructedName;
                return true;
            }

            failureReason = nameStatus == StaticParseKind.Invalid
                ? "a workflow binding name cannot be empty"
                : "the workflow binding name is dynamic";
            return false;
        }

        failureReason = stringStatus == StaticParseKind.Invalid
            ? "a workflow binding name cannot be empty"
            : "the workflow binding name is dynamic";
        return false;
    }

    private static bool TryParseFluentRequiredString(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string value,
        out string? failureReason)
    {
        value = null!;
        var expression = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        var status = expression is null
            ? StaticParseKind.Invalid
            : TryParseRequiredString(expression, semanticModel, cancellationToken, out value);
        if (status == StaticParseKind.Success)
        {
            failureReason = null;
            return true;
        }

        failureReason = status == StaticParseKind.Invalid
            ? $"{invocation.Expression} requires a non-empty string"
            : $"{invocation.Expression} uses a dynamic string";
        return false;
    }

    private static bool TryParseFluentRelation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out WorkflowPredicateSyntax predicate,
        out string? failureReason)
    {
        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        var relationArgument = operation?.Arguments.FirstOrDefault(argument =>
            argument.Parameter?.Name == "relationName");
        if (relationArgument?.Syntax is not ArgumentSyntax relationArgumentSyntax
            || TryParseRequiredString(
                relationArgumentSyntax.Expression,
                semanticModel,
                cancellationToken,
                out var relationName) != StaticParseKind.Success)
        {
            predicate = null!;
            failureReason = "a relation predicate requires a static non-empty relation name";
            return false;
        }

        var pathNames = new List<string>();
        foreach (var operationArgument in operation!.Arguments.Where(argument =>
            argument.Parameter?.Name == "linkPath"))
        {
            if (operationArgument.Syntax is not ArgumentSyntax argument)
            {
                predicate = null!;
                failureReason = "a relation path must contain only static non-empty link names";
                return false;
            }

            var resolving = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            if (TryResolveItems(
                argument.Expression,
                semanticModel,
                cancellationToken,
                resolving,
                out var items))
            {
                foreach (var item in items)
                {
                    if (TryParseRequiredString(item, semanticModel, cancellationToken, out var itemName)
                        != StaticParseKind.Success)
                    {
                        predicate = null!;
                        failureReason = "a relation path must contain only static non-empty link names";
                        return false;
                    }

                    pathNames.Add(itemName);
                }
            }
            else if (TryParseRequiredString(
                argument.Expression,
                semanticModel,
                cancellationToken,
                out var pathName) == StaticParseKind.Success)
            {
                pathNames.Add(pathName);
            }
            else
            {
                predicate = null!;
                failureReason = "a relation path must contain only static non-empty link names";
                return false;
            }
        }

        var token = "relation|relation:" + Segment(relationName) + ":"
            + string.Concat(pathNames.Select(Segment));
        predicate = WorkflowPredicateSyntax.BooleanAtom(
            token,
            "link|" + (pathNames.Count == 0 ? relationName : pathNames[0]));
        failureReason = null;
        return true;
    }

    private static bool TryParseFluentPropertySelector(
        ExpressionSyntax? expression,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out string propertyName)
    {
        expression = expression is null ? null : Unwrap(expression);
        if (expression is not LambdaExpressionSyntax lambda
            || lambda.Body is not ExpressionSyntax body)
        {
            propertyName = null!;
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
            : semanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) as IParameterSymbol;
        if (parameter is null
            || !TryGetDirectProperty(
                body,
                parameter,
                semanticModel,
                cancellationToken,
                out var property,
                out var conversionIsInvalid)
            || conversionIsInvalid)
        {
            propertyName = null!;
            return false;
        }

        propertyName = property.Name;
        return true;
    }

    private static WorkflowPredicateSyntax Project(StaticPredicate predicate) => new(
        predicate.Formula,
        predicate.AtomReads.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray(),
            StringComparer.Ordinal),
        predicate.OpaqueKeys
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToImmutableArray(),
        predicate.InvalidReason);

    private static bool TryTranslateWorkflowExpression(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out StaticPredicate predicate,
        out string? failureReason)
    {
        expression = UnwrapParentheses(expression);

        if (expression is LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
            {
                predicate = StaticPredicate.True;
                failureReason = null;
                return true;
            }

            if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                predicate = StaticPredicate.False;
                failureReason = null;
                return true;
            }
        }

        if (expression is PrefixUnaryExpressionSyntax not
            && not.IsKind(SyntaxKind.LogicalNotExpression)
            && TryTranslateWorkflowExpression(
                not.Operand,
                parameter,
                semanticModel,
                cancellationToken,
                out var negated,
                out failureReason))
        {
            predicate = negated.Not();
            return true;
        }

        if (expression is BinaryExpressionSyntax logical
            && logical.Kind() is SyntaxKind.LogicalAndExpression
                or SyntaxKind.BitwiseAndExpression
                or SyntaxKind.LogicalOrExpression
                or SyntaxKind.BitwiseOrExpression
            && TryTranslateWorkflowExpression(
                logical.Left,
                parameter,
                semanticModel,
                cancellationToken,
                out var leftPredicate,
                out failureReason)
            && TryTranslateWorkflowExpression(
                logical.Right,
                parameter,
                semanticModel,
                cancellationToken,
                out var rightPredicate,
                out failureReason))
        {
            predicate = logical.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.BitwiseAndExpression
                ? StaticPredicate.All(new[] { leftPredicate, rightPredicate })
                : StaticPredicate.Any(new[] { leftPredicate, rightPredicate });
            return true;
        }

        if (TryGetDirectProperty(
                expression,
                parameter,
                semanticModel,
                cancellationToken,
                out var booleanProperty,
                out var directConversionIsInvalid)
            && !directConversionIsInvalid
            && TryCreateWorkflowResource(booleanProperty, out var booleanResource, out failureReason)
            && booleanResource.ScalarKind == LogicScalarKind.Boolean)
        {
            predicate = StaticPredicate.Comparison(
                booleanResource,
                LogicComparisonOperator.Equal,
                new LogicLiteral(LogicLiteralKind.Boolean, "true"));
            return true;
        }

        if (expression is BinaryExpressionSyntax comparison
            && comparison.Kind() is SyntaxKind.EqualsExpression
                or SyntaxKind.NotEqualsExpression
                or SyntaxKind.LessThanExpression
                or SyntaxKind.LessThanOrEqualExpression
                or SyntaxKind.GreaterThanExpression
                or SyntaxKind.GreaterThanOrEqualExpression)
        {
            var leftIsProperty = TryGetDirectProperty(
                comparison.Left,
                parameter,
                semanticModel,
                cancellationToken,
                out var leftProperty,
                out _);
            var rightIsProperty = TryGetDirectProperty(
                comparison.Right,
                parameter,
                semanticModel,
                cancellationToken,
                out var rightProperty,
                out _);
            if (leftIsProperty == rightIsProperty)
            {
                predicate = null!;
                failureReason = "exactly one comparison operand must be a direct subject property";
                return false;
            }

            var property = leftIsProperty ? leftProperty : rightProperty;
            if (!TryCreateWorkflowResource(property, out var resource, out failureReason))
            {
                predicate = null!;
                return false;
            }

            var literalExpression = leftIsProperty ? comparison.Right : comparison.Left;
            if (!TryCreateWorkflowLiteral(
                    literalExpression,
                    property.Type,
                    resource.ScalarKind,
                    semanticModel,
                    cancellationToken,
                    out var value,
                    out failureReason))
            {
                predicate = null!;
                return false;
            }

            var comparisonOperator = WorkflowComparison(comparison.Kind());
            if (!leftIsProperty)
            {
                comparisonOperator = ReverseWorkflowComparison(comparisonOperator);
            }

            predicate = StaticPredicate.Comparison(resource, comparisonOperator, value);
            failureReason = null;
            return true;
        }

        predicate = null!;
        failureReason = "unsupported action-predicate expression";
        return false;
    }

    private static bool TryCreateWorkflowResource(
        IPropertySymbol property,
        out LogicResource resource,
        out string? failureReason)
    {
        if (!TryGetExpressionScalarKind(property.Type, out var scalarKind, out var nullable))
        {
            resource = null!;
            failureReason = $"property type '{property.Type.ToDisplayString()}' is outside the predicate scalar grammar";
            return false;
        }

        var scalarTypeName = scalarKind == LogicScalarKind.Enum
            ? UnwrapNullable(property.Type, out _).Name
            : null;
        resource = new LogicResource(
            "property|" + property.Name,
            scalarKind,
            nullable,
            scalarTypeName);
        failureReason = null;
        return true;
    }

    private static bool TryCreateWorkflowLiteral(
        ExpressionSyntax expression,
        ITypeSymbol propertyType,
        LogicScalarKind scalarKind,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken,
        out LogicLiteral literal,
        out string? failureReason)
    {
        var constant = semanticModel.GetConstantValue(expression, cancellationToken);
        if (constant.HasValue && constant.Value is null)
        {
            literal = LogicLiteral.Null;
            failureReason = null;
            return true;
        }

        switch (scalarKind)
        {
            case LogicScalarKind.Boolean when constant is { HasValue: true, Value: bool boolean }:
                literal = new LogicLiteral(
                    LogicLiteralKind.Boolean,
                    boolean ? "true" : "false");
                failureReason = null;
                return true;

            case LogicScalarKind.Integer:
                if (TryParseBigIntegerExpression(
                        expression,
                        semanticModel,
                        cancellationToken,
                        new HashSet<ISymbol>(SymbolEqualityComparer.Default),
                        out var integer,
                        out var integerWasStatic)
                    && integerWasStatic)
                {
                    literal = new LogicLiteral(LogicLiteralKind.Integer, integer);
                    failureReason = null;
                    return true;
                }

                break;

            case LogicScalarKind.Decimal when constant.HasValue:
                var decimalText = constant.Value switch
                {
                    decimal number => number.ToString("G29", CultureInfo.InvariantCulture),
                    _ when constant.Value is not null && TryInvariantInteger(constant.Value, out var integerText) =>
                        integerText,
                    _ => null,
                };
                if (decimalText is not null
                    && FiniteDomainSolver.TryNormalizeExactDecimal(decimalText, out var canonicalDecimal))
                {
                    literal = new LogicLiteral(LogicLiteralKind.Decimal, canonicalDecimal);
                    failureReason = null;
                    return true;
                }

                break;

            case LogicScalarKind.String when constant is { HasValue: true, Value: string text }:
                literal = new LogicLiteral(LogicLiteralKind.String, text);
                failureReason = null;
                return true;

            case LogicScalarKind.Enum:
                var enumType = UnwrapNullable(propertyType, out _);
                var enumField = expression.DescendantNodesAndSelf()
                    .Select(node => semanticModel.GetSymbolInfo(node, cancellationToken).Symbol)
                    .OfType<IFieldSymbol>()
                    .FirstOrDefault(field => field.ContainingType.TypeKind == TypeKind.Enum);
                if (enumField is not null)
                {
                    literal = new LogicLiteral(
                        LogicLiteralKind.Enum,
                        enumField.Name,
                        enumType.Name);
                    failureReason = null;
                    return true;
                }

                break;
        }

        literal = null!;
        failureReason = $"the literal is not representable in the property's {scalarKind} domain";
        return false;
    }

    private static LogicComparisonOperator WorkflowComparison(SyntaxKind kind) => kind switch
    {
        SyntaxKind.EqualsExpression => LogicComparisonOperator.Equal,
        SyntaxKind.NotEqualsExpression => LogicComparisonOperator.NotEqual,
        SyntaxKind.LessThanExpression => LogicComparisonOperator.LessThan,
        SyntaxKind.LessThanOrEqualExpression => LogicComparisonOperator.LessThanOrEqual,
        SyntaxKind.GreaterThanExpression => LogicComparisonOperator.GreaterThan,
        SyntaxKind.GreaterThanOrEqualExpression => LogicComparisonOperator.GreaterThanOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static LogicComparisonOperator ReverseWorkflowComparison(
        LogicComparisonOperator comparison) => comparison switch
        {
            LogicComparisonOperator.Equal => LogicComparisonOperator.Equal,
            LogicComparisonOperator.NotEqual => LogicComparisonOperator.NotEqual,
            LogicComparisonOperator.LessThan => LogicComparisonOperator.GreaterThan,
            LogicComparisonOperator.LessThanOrEqual => LogicComparisonOperator.GreaterThanOrEqual,
            LogicComparisonOperator.GreaterThan => LogicComparisonOperator.LessThan,
            LogicComparisonOperator.GreaterThanOrEqual => LogicComparisonOperator.LessThanOrEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison)),
        };

    /// <summary>A dependency-free projection of one parsed predicate.</summary>
    internal sealed class WorkflowPredicateSyntax
    {
        internal WorkflowPredicateSyntax(
            LogicFormula formula,
            ImmutableDictionary<string, ImmutableArray<string>> atomReads,
            ImmutableArray<string> opaqueKeys,
            string? invalidReason)
        {
            Formula = formula;
            AtomReads = atomReads;
            OpaqueKeys = opaqueKeys;
            InvalidReason = invalidReason;
        }

        internal static WorkflowPredicateSyntax Dynamic { get; } = new(
            LogicFormula.True,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty,
            ImmutableArray<string>.Empty,
            "the predicate is dynamic or outside the typed predicate grammar");

        internal LogicFormula Formula { get; }

        internal ImmutableDictionary<string, ImmutableArray<string>> AtomReads { get; }

        internal ImmutableArray<string> OpaqueKeys { get; }

        internal string? InvalidReason { get; }

        internal static WorkflowPredicateSyntax Invalid(string reason) => new(
            LogicFormula.True,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty,
            ImmutableArray<string>.Empty,
            reason);

        internal static WorkflowPredicateSyntax BooleanAtom(string key, params string[] reads) => new(
            LogicFormula.BooleanAtom(new LogicResource(key, LogicScalarKind.Boolean, false)),
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add(
                key,
                reads.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray()),
            ImmutableArray<string>.Empty,
            null);

        internal static WorkflowPredicateSyntax All(IEnumerable<WorkflowPredicateSyntax> predicates)
        {
            var array = predicates.ToArray();
            var reads = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var predicate in array)
            {
                foreach (var pair in predicate.AtomReads)
                {
                    if (!reads.TryGetValue(pair.Key, out var values))
                    {
                        values = new HashSet<string>(StringComparer.Ordinal);
                        reads.Add(pair.Key, values);
                    }

                    values.UnionWith(pair.Value);
                }
            }

            return new WorkflowPredicateSyntax(
                LogicFormula.All(array.Select(item => item.Formula)),
                reads.ToImmutableDictionary(
                    pair => pair.Key,
                    pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToImmutableArray(),
                    StringComparer.Ordinal),
                array.SelectMany(item => item.OpaqueKeys)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToImmutableArray(),
                array.Select(item => item.InvalidReason).FirstOrDefault(reason => reason is not null));
        }
    }

    /// <summary>A dependency-free projection of one direct action descriptor.</summary>
    internal sealed class WorkflowActionContractSyntax
    {
        internal WorkflowActionContractSyntax(
            string domainName,
            string objectTypeName,
            string actionName,
            bool hasWorkflowBinding,
            string? boundWorkflowName,
            WorkflowPredicateSyntax requirement,
            WorkflowPredicateSyntax guarantee,
            ImmutableArray<string> frame,
            string? requiredAuthority,
            string? compensatingActionName,
            string? invalidReason,
            Location location)
        {
            DomainName = domainName;
            ObjectTypeName = objectTypeName;
            ActionName = actionName;
            HasWorkflowBinding = hasWorkflowBinding;
            BoundWorkflowName = boundWorkflowName;
            Requirement = requirement;
            Guarantee = guarantee;
            Frame = frame;
            RequiredAuthority = requiredAuthority;
            CompensatingActionName = compensatingActionName;
            InvalidReason = invalidReason;
            Location = location;
        }

        internal string DomainName { get; }

        internal string ObjectTypeName { get; }

        internal string ActionName { get; }

        internal bool HasWorkflowBinding { get; }

        internal string? BoundWorkflowName { get; }

        internal WorkflowPredicateSyntax Requirement { get; }

        internal WorkflowPredicateSyntax Guarantee { get; }

        internal ImmutableArray<string> Frame { get; }

        internal string? RequiredAuthority { get; }

        internal string? CompensatingActionName { get; }

        internal string? InvalidReason { get; }

        internal Location Location { get; }
    }

    private sealed class WorkflowActionIdentitySyntax
    {
        internal WorkflowActionIdentitySyntax(
            string domainName,
            string objectTypeName,
            string actionName,
            Location location)
        {
            DomainName = domainName;
            ObjectTypeName = objectTypeName;
            ActionName = actionName;
            Location = location;
        }

        internal string DomainName { get; }

        internal string ObjectTypeName { get; }

        internal string ActionName { get; }

        internal Location Location { get; }
    }
}
