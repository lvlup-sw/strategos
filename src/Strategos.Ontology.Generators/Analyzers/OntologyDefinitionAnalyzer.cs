using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OntologyDefinitionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            OntologyDiagnostics.MissingKey,
            OntologyDiagnostics.InvalidPropertyExpression,
            OntologyDiagnostics.LinkTargetNotRegistered,
            OntologyDiagnostics.ActionNotBound,
            OntologyDiagnostics.InterfaceMappingBadProperty,
            OntologyDiagnostics.DuplicateObjectType,
            OntologyDiagnostics.CrossDomainLinkUnverifiable,
            OntologyDiagnostics.EdgeTypeMissingProperty,
            OntologyDiagnostics.EmitsEventUndeclared,
            OntologyDiagnostics.ModifiesUndeclaredProperty,
            OntologyDiagnostics.CreatesLinkedUndeclared,
            OntologyDiagnostics.RequiresLinkUndeclared,
            OntologyDiagnostics.PostconditionOverlapsEvent,
            OntologyDiagnostics.LifecyclePropertyUndeclared,
            OntologyDiagnostics.LifecycleInitialStateCount,
            OntologyDiagnostics.LifecycleNoTerminalState,
            OntologyDiagnostics.LifecycleTransitionBadState,
            OntologyDiagnostics.LifecycleTransitionBadAction,
            OntologyDiagnostics.LifecycleTransitionBadEvent,
            OntologyDiagnostics.LifecycleUnreachableState,
            OntologyDiagnostics.LifecycleDeadEndState,
            OntologyDiagnostics.DerivedFromUndeclaredProperty,
            OntologyDiagnostics.DerivedFromNonComputed,
            OntologyDiagnostics.DerivationCycle,
            OntologyDiagnostics.DerivedFromExternalUnresolvable,
            OntologyDiagnostics.ComputedNoDerivedFrom,
            OntologyDiagnostics.InterfaceActionUnmapped,
            OntologyDiagnostics.ActionViaBadReference,
            OntologyDiagnostics.InterfaceActionIncompatible,
            OntologyDiagnostics.InterfaceActionNoImplementors,
            OntologyDiagnostics.CrossDomainLinkNoExtensionPoint,
            OntologyDiagnostics.ExtensionPointInterfaceUnsatisfied,
            OntologyDiagnostics.ExtensionPointEdgeMissing,
            OntologyDiagnostics.ExtensionPointNoLinks,
            OntologyDiagnostics.ExtensionPointMaxLinksExceeded,
            OntologyDiagnostics.ReadOnlyConflictsWithMutation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.Text != "Define")
        {
            return;
        }

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
        if (methodSymbol == null || !methodSymbol.IsOverride)
        {
            return;
        }

        // Check if containing type derives from DomainOntology
        var containingType = methodSymbol.ContainingType;
        if (!IsDomainOntologySubclass(containingType))
        {
            return;
        }

        // Collect all builder invocations in the method body
        var body = method.Body ?? (SyntaxNode?)method.ExpressionBody;
        if (body == null)
        {
            return;
        }

        var domainInfo = new DomainAnalysisInfo();
        CollectDomainInfo(body, context.SemanticModel, domainInfo);
        ReportDiagnostics(context, domainInfo);
    }

    private static bool IsDomainOntologySubclass(INamedTypeSymbol? type)
    {
        var current = type?.BaseType;
        while (current != null)
        {
            if (current.Name == "DomainOntology" &&
                current.ContainingNamespace?.ToDisplayString() == "Strategos.Ontology")
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void CollectDomainInfo(SyntaxNode body, SemanticModel model, DomainAnalysisInfo info)
    {
        var invocations = body.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            var methodName = calledMethod.Name;
            var receiverType = calledMethod.ContainingType?.Name ?? "";

            // builder.Object<T>(...)
            if (methodName == "Object" && calledMethod.TypeArguments.Length == 1 &&
                IsOntologyBuilderType(receiverType))
            {
                var typeName = calledMethod.TypeArguments[0].Name;
                var objectInfo = new ObjectTypeInfo(typeName, invocation.GetLocation());

                if (info.ObjectTypes.ContainsKey(typeName))
                {
                    info.DuplicateObjectTypes.Add((typeName, invocation.GetLocation()));
                }
                else
                {
                    info.ObjectTypes[typeName] = objectInfo;
                }

                // Parse the configure lambda
                var args = invocation.ArgumentList.Arguments;
                if (args.Count > 0 && args[0].Expression is LambdaExpressionSyntax lambda)
                {
                    CollectObjectTypeInfo(lambda, model, objectInfo);
                }
            }

            // builder.CrossDomainLink(...)
            if (methodName == "CrossDomainLink" && IsOntologyBuilderType(receiverType))
            {
                var linkName = ExtractStringArg(invocation, 0);
                if (linkName != null)
                {
                    var cdLink = new CrossDomainLinkInfo(linkName, invocation.GetLocation());
                    info.CrossDomainLinks.Add(cdLink);

                    // Walk forward through the fluent chain to collect From<T>, WithEdge, etc.
                    CollectCrossDomainLinkChain(invocation, model, cdLink);
                }
            }

            // builder.Interface<T>(...)
            if (methodName == "Interface" && calledMethod.TypeArguments.Length == 1 &&
                IsOntologyBuilderType(receiverType))
            {
                var interfaceName = ExtractStringArg(invocation, 0) ?? calledMethod.TypeArguments[0].Name;
                var ifaceInfo = new InterfaceInfo(interfaceName, invocation.GetLocation());
                info.Interfaces[interfaceName] = ifaceInfo;

                // Parse the configure lambda for interface actions
                if (invocation.ArgumentList.Arguments.Count > 1)
                {
                    var lastArg = invocation.ArgumentList.Arguments.Last();
                    if (lastArg.Expression is LambdaExpressionSyntax ifaceLambda)
                    {
                        CollectInterfaceInfo(ifaceLambda, model, ifaceInfo);
                    }
                }
            }
        }
    }

    private static void CollectObjectTypeInfo(
        LambdaExpressionSyntax lambda, SemanticModel model, ObjectTypeInfo info)
    {
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            var methodName = calledMethod.Name;

            switch (methodName)
            {
                case "Key":
                    info.HasKey = true;
                    break;

                case "Property" when IsObjectTypeBuilderMethod(calledMethod):
                    CollectPropertyDeclaration(invocation, info);
                    break;

                case "HasOne" or "HasMany" or "ManyToMany":
                    CollectLinkDeclaration(invocation, model, calledMethod, methodName, info);
                    break;

                case "Action" when IsObjectTypeBuilderMethod(calledMethod):
                    CollectActionDeclaration(invocation, info);
                    break;

                case "Event":
                    CollectEventDeclaration(invocation, model, calledMethod, info);
                    break;

                case "Accepts" when calledMethod.TypeArguments.Length > 0:
                    CollectAcceptsInfo(invocation, model, calledMethod, info);
                    break;

                case "BoundToWorkflow" or "BoundToTool":
                    CollectBoundActionInfo(invocation, model, info);
                    break;

                case "ReadOnly":
                    CollectReadOnlyActionInfo(invocation, model, info);
                    break;

                case "Modifies":
                    CollectModifiesInfo(invocation, model, info);
                    break;

                case "EmitsEvent":
                    CollectEmitsEventInfo(invocation, model, calledMethod, info);
                    break;

                case "CreatesLinked":
                    CollectCreatesLinkedInfo(invocation, model, info);
                    break;

                case "RequiresLink":
                    CollectRequiresLinkInfo(invocation, model, info);
                    break;

                case "Computed":
                    CollectComputedInfo(invocation, model, info);
                    break;

                case "DerivedFrom" when !calledMethod.Name.Contains("External"):
                    CollectDerivedFromInfo(invocation, model, info);
                    break;

                case "DerivedFromExternal":
                    CollectDerivedFromExternalInfo(invocation, model, info);
                    break;

                case "Lifecycle":
                    CollectLifecycleDeclaration(invocation, model, info);
                    break;

                case "AcceptsExternalLinks":
                    CollectExtensionPointDeclaration(invocation, model, info);
                    break;

                case "Implements":
                    CollectImplementsDeclaration(invocation, model, calledMethod, info);
                    break;
            }
        }
    }

    private static void CollectPropertyDeclaration(
        InvocationExpressionSyntax invocation, ObjectTypeInfo info)
    {
        var propName = ExtractPropertyNameFromExpression(invocation);
        if (propName != null)
        {
            info.DeclaredProperties.Add(propName);
        }
        else if (invocation.ArgumentList.Arguments.Count > 0)
        {
            info.InvalidPropertyExpressions.Add(invocation.GetLocation());
        }
    }

    private static void CollectLinkDeclaration(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol calledMethod,
        string methodName,
        ObjectTypeInfo info)
    {
        var linkName = ExtractStringArg(invocation, 0);
        if (linkName != null)
        {
            info.DeclaredLinks.Add(linkName);
        }

        if (calledMethod.TypeArguments.Length > 0)
        {
            var linkTargetType = calledMethod.TypeArguments[0].Name;
            info.LinkTargets.Add((linkName ?? "", linkTargetType, invocation.GetLocation()));
        }

        if (methodName == "ManyToMany" && invocation.ArgumentList.Arguments.Count >= 2)
        {
            CollectManyToManyEdgeInfo(invocation, model, linkName, info);
        }
    }

    private static void CollectManyToManyEdgeInfo(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        string? linkName,
        ObjectTypeInfo info)
    {
        var edgeArg = invocation.ArgumentList.Arguments[1].Expression;
        var hasEdgeProperty = false;
        if (edgeArg is LambdaExpressionSyntax edgeLambda)
        {
            var edgeInvocations = edgeLambda.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var edgeInv in edgeInvocations)
            {
                var edgeSymbolInfo = model.GetSymbolInfo(edgeInv);
                if (edgeSymbolInfo.Symbol is IMethodSymbol edgeMethod && edgeMethod.Name == "Property")
                {
                    hasEdgeProperty = true;
                    break;
                }
            }
        }

        if (!hasEdgeProperty)
        {
            info.EdgesWithoutProperties.Add((linkName ?? "", invocation.GetLocation()));
        }
    }

    private static void CollectActionDeclaration(
        InvocationExpressionSyntax invocation, ObjectTypeInfo info)
    {
        var actionName = ExtractStringArg(invocation, 0);
        if (actionName != null)
        {
            info.DeclaredActions.Add(actionName);
            info.ActionLocations[actionName] = invocation.GetLocation();
        }
    }

    private static void CollectEventDeclaration(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol calledMethod,
        ObjectTypeInfo info)
    {
        if (calledMethod.TypeArguments.Length == 0)
        {
            return;
        }

        var eventTypeName = calledMethod.TypeArguments[0].Name;
        info.DeclaredEvents.Add(eventTypeName);

        if (invocation.ArgumentList.Arguments.Count > 0 &&
            invocation.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax eventLambda)
        {
            CollectEventInfo(eventLambda, model, info, eventTypeName);
        }
    }

    private static void CollectAcceptsInfo(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol calledMethod,
        ObjectTypeInfo info)
    {
        var acceptsActionName = FindActionNameInChain(invocation, model);
        if (acceptsActionName != null)
        {
            info.ActionAcceptsTypes[acceptsActionName] = calledMethod.TypeArguments[0].Name;
        }
    }

    private static void CollectBoundActionInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var boundActionName = FindActionNameInChain(invocation, model);
        if (boundActionName != null)
        {
            info.BoundActions.Add(boundActionName);
        }
    }

    private static void CollectReadOnlyActionInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var actionName = FindActionNameInChain(invocation, model);
        if (actionName != null)
        {
            info.ReadOnlyActions.Add(actionName);
        }
    }

    private static void CollectModifiesInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var modifiesActionName = FindActionNameInChain(invocation, model);
        if (modifiesActionName != null)
        {
            // Always record the mutating call presence so AONT036 can flag a
            // ReadOnly+mutate conflict even when the property argument is
            // non-literal and ExtractPropertyNameFromExpression returns null.
            info.ActionMutationCalls.Add((modifiesActionName, "Modifies", invocation.GetLocation()));
        }

        var modifiesProp = ExtractPropertyNameFromExpression(invocation);
        if (modifiesActionName != null && modifiesProp != null)
        {
            info.ActionModifiesProperties.Add((modifiesActionName, modifiesProp, invocation.GetLocation()));
        }
    }

    private static void CollectEmitsEventInfo(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol calledMethod,
        ObjectTypeInfo info)
    {
        var emitsActionName = FindActionNameInChain(invocation, model);
        if (emitsActionName != null)
        {
            info.ActionMutationCalls.Add((emitsActionName, "EmitsEvent", invocation.GetLocation()));
        }

        if (emitsActionName != null && calledMethod.TypeArguments.Length > 0)
        {
            var eventTypeName = calledMethod.TypeArguments[0].Name;
            info.ActionEmitsEvents.Add((emitsActionName, eventTypeName, invocation.GetLocation()));
        }
    }

    private static void CollectCreatesLinkedInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var createsActionName = FindActionNameInChain(invocation, model);
        if (createsActionName != null)
        {
            info.ActionMutationCalls.Add((createsActionName, "CreatesLinked", invocation.GetLocation()));
        }

        var createsLinkName = ExtractStringArg(invocation, 0);
        if (createsActionName != null && createsLinkName != null)
        {
            info.ActionCreatesLinked.Add((createsActionName, createsLinkName, invocation.GetLocation()));
        }
    }

    private static void CollectRequiresLinkInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var reqActionName = FindActionNameInChain(invocation, model);
        var reqLinkName = ExtractStringArg(invocation, 0);
        if (reqActionName != null && reqLinkName != null)
        {
            info.ActionRequiresLinks.Add((reqActionName, reqLinkName, invocation.GetLocation()));
        }
    }

    private static void CollectComputedInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var computedPropName = FindPropertyNameInChain(invocation, model);
        if (computedPropName != null)
        {
            info.ComputedProperties.Add(computedPropName);
        }
    }

    private static void CollectDerivedFromInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var derivedPropName = FindPropertyNameInChain(invocation, model);
        if (derivedPropName == null)
        {
            return;
        }

        info.PropertiesWithDerivedFrom.Add(derivedPropName);

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var sourceProp = ExtractPropertyNameFromLambdaArg(arg.Expression);
            if (sourceProp != null)
            {
                info.DerivedFromReferences.Add((derivedPropName, sourceProp, invocation.GetLocation()));
            }
        }
    }

    private static void CollectDerivedFromExternalInfo(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var extDerivedPropName = FindPropertyNameInChain(invocation, model);
        if (extDerivedPropName == null)
        {
            return;
        }

        info.PropertiesWithDerivedFrom.Add(extDerivedPropName);
        var extDomain = ExtractStringArg(invocation, 0);
        var extType = ExtractStringArg(invocation, 1);
        var extProp = ExtractStringArg(invocation, 2);
        if (extDomain != null && extType != null && extProp != null)
        {
            info.DerivedFromExternalReferences.Add(
                (extDerivedPropName, extDomain, extType, extProp, invocation.GetLocation()));
        }
    }

    private static void CollectLifecycleDeclaration(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var lifecyclePropName = ExtractPropertyNameFromExpression(invocation);
        if (lifecyclePropName != null)
        {
            info.LifecyclePropertyName = lifecyclePropName;
            info.LifecycleLocation = invocation.GetLocation();
        }

        if (invocation.ArgumentList.Arguments.Count > 1)
        {
            var lifecycleLambdaArg = invocation.ArgumentList.Arguments.Last();
            CollectLifecycleInfo(lifecycleLambdaArg.Expression, model, info);
        }
    }

    private static void CollectExtensionPointDeclaration(
        InvocationExpressionSyntax invocation, SemanticModel model, ObjectTypeInfo info)
    {
        var epName = ExtractStringArg(invocation, 0);
        if (epName == null)
        {
            return;
        }

        var epInfo = new ExtensionPointInfo(epName, invocation.GetLocation());
        info.ExtensionPoints.Add(epInfo);

        if (invocation.ArgumentList.Arguments.Count > 1 &&
            invocation.ArgumentList.Arguments[1].Expression is LambdaExpressionSyntax epLambdaSyntax)
        {
            CollectExtensionPointInfo(epLambdaSyntax, model, epInfo);
        }
    }

    private static void CollectImplementsDeclaration(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol calledMethod,
        ObjectTypeInfo info)
    {
        if (calledMethod.TypeArguments.Length == 0)
        {
            return;
        }

        var interfaceTypeName = calledMethod.TypeArguments[0].Name;
        info.ImplementedInterfaces.Add(interfaceTypeName);

        if (invocation.ArgumentList.Arguments.Count > 0 &&
            invocation.ArgumentList.Arguments.Last().Expression is LambdaExpressionSyntax implLambdaSyntax)
        {
            CollectImplementsMappingInfo(implLambdaSyntax, model, info, interfaceTypeName);
        }
    }

    private static void CollectCrossDomainLinkChain(
        InvocationExpressionSyntax crossDomainLinkInvocation, SemanticModel model, CrossDomainLinkInfo linkInfo)
    {
        // Walk up the syntax tree to find the statement containing this fluent chain
        // Then look at all invocations that are part of the chain
        var statement = crossDomainLinkInvocation.FirstAncestorOrSelf<StatementSyntax>();
        if (statement == null)
        {
            return;
        }

        var allInvocations = statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in allInvocations)
        {
            var symInfo = model.GetSymbolInfo(inv);
            if (symInfo.Symbol is not IMethodSymbol method)
            {
                continue;
            }

            var containingType = method.ContainingType?.Name ?? "";
            if (!containingType.Contains("CrossDomainLink"))
            {
                continue;
            }

            switch (method.Name)
            {
                case "From" when method.TypeArguments.Length > 0:
                    linkInfo.SourceType = method.TypeArguments[0].Name;
                    break;

                case "WithEdge":
                    if (inv.ArgumentList.Arguments.Count > 0 &&
                        inv.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax edgeLambda)
                    {
                        var edgeInvocations = edgeLambda.DescendantNodes().OfType<InvocationExpressionSyntax>();
                        foreach (var edgeInv in edgeInvocations)
                        {
                            var edgeSym = model.GetSymbolInfo(edgeInv);
                            if (edgeSym.Symbol is IMethodSymbol edgeMethod && edgeMethod.Name == "Property")
                            {
                                var epName = ExtractStringArg(edgeInv, 0);
                                if (epName != null)
                                {
                                    linkInfo.EdgeProperties.Add(epName);
                                }
                            }
                        }
                    }

                    break;
            }
        }
    }

    private static void CollectExtensionPointInfo(
        LambdaExpressionSyntax lambda, SemanticModel model, ExtensionPointInfo info)
    {
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            switch (calledMethod.Name)
            {
                case "FromInterface":
                    if (calledMethod.TypeArguments.Length > 0)
                    {
                        info.RequiredInterface = calledMethod.TypeArguments[0].Name;
                    }

                    break;

                case "RequiresEdgeProperty":
                    var edgePropName = ExtractStringArg(invocation, 0);
                    if (edgePropName != null)
                    {
                        info.RequiredEdgeProperties.Add(edgePropName);
                    }

                    break;

                case "MaxLinks":
                    if (invocation.ArgumentList.Arguments.Count > 0 &&
                        invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax maxLiteral &&
                        maxLiteral.IsKind(SyntaxKind.NumericLiteralExpression) &&
                        maxLiteral.Token.Value is int maxVal)
                    {
                        info.MaxLinks = maxVal;
                    }

                    break;
            }
        }
    }

    private static void CollectEventInfo(
        LambdaExpressionSyntax lambda, SemanticModel model, ObjectTypeInfo info, string eventTypeName)
    {
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            if (calledMethod.Name == "UpdatesProperty" && invocation.ArgumentList.Arguments.Count >= 1)
            {
                var propName = ExtractPropertyNameFromLambdaArg(invocation.ArgumentList.Arguments[0].Expression);
                if (propName != null)
                {
                    info.EventUpdatesProperties.Add((eventTypeName, propName, invocation.GetLocation()));
                }
            }
        }
    }

    private static void CollectLifecycleInfo(
        ExpressionSyntax expression, SemanticModel model, ObjectTypeInfo info)
    {
        // The lifecycle configure lambda may be wrapped in a cast: (Action<ILifecycleBuilder<T>>)(lifecycle => {...})
        SyntaxNode? lambdaBody = null;

        if (expression is CastExpressionSyntax cast && cast.Expression is ParenthesizedExpressionSyntax paren)
        {
            lambdaBody = paren.Expression;
        }
        else if (expression is LambdaExpressionSyntax lambda)
        {
            lambdaBody = lambda;
        }
        else if (expression is ParenthesizedExpressionSyntax parenDirect)
        {
            lambdaBody = parenDirect.Expression;
        }

        if (lambdaBody == null)
        {
            return;
        }

        var invocations = lambdaBody.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            switch (calledMethod.Name)
            {
                case "State":
                    // Extract enum value from argument
                    var stateArg = invocation.ArgumentList.Arguments.FirstOrDefault();
                    if (stateArg != null)
                    {
                        var stateName = ExtractEnumValueName(stateArg.Expression);
                        if (stateName != null)
                        {
                            info.LifecycleStates.Add(stateName);
                        }
                    }

                    break;

                case "Initial":
                    info.LifecycleInitialCount++;
                    var initialState = FindStateNameInChain(invocation);
                    if (initialState != null)
                    {
                        info.LifecycleInitialStates.Add(initialState);
                    }

                    break;

                case "Terminal":
                    info.LifecycleTerminalCount++;
                    var terminalState = FindStateNameInChain(invocation);
                    if (terminalState != null)
                    {
                        info.LifecycleTerminalStates.Add(terminalState);
                    }

                    break;

                case "Transition":
                    if (invocation.ArgumentList.Arguments.Count >= 2)
                    {
                        var from = ExtractEnumValueName(invocation.ArgumentList.Arguments[0].Expression);
                        var to = ExtractEnumValueName(invocation.ArgumentList.Arguments[1].Expression);
                        if (from != null && to != null)
                        {
                            info.LifecycleTransitions.Add((from, to, invocation.GetLocation()));
                        }
                    }

                    break;

                case "TriggeredByAction":
                    var triggerActionName = ExtractStringArg(invocation, 0);
                    if (triggerActionName != null)
                    {
                        info.LifecycleTransitionActions.Add((triggerActionName, invocation.GetLocation()));
                    }

                    break;

                case "TriggeredByEvent":
                    if (calledMethod.TypeArguments.Length > 0)
                    {
                        var triggerEventType = calledMethod.TypeArguments[0].Name;
                        info.LifecycleTransitionEvents.Add((triggerEventType, invocation.GetLocation()));
                    }

                    break;
            }
        }
    }

    private static void CollectInterfaceInfo(
        LambdaExpressionSyntax lambda, SemanticModel model, InterfaceInfo info)
    {
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            if (calledMethod.Name == "Action")
            {
                var actionName = ExtractStringArg(invocation, 0);
                if (actionName != null)
                {
                    info.DeclaredActions.Add(actionName);
                }
            }

            if (calledMethod.Name == "Accepts" && calledMethod.TypeArguments.Length > 0)
            {
                var acceptsType = calledMethod.TypeArguments[0].Name;
                var ifaceActionName = FindActionNameInChainSyntactic(invocation);
                if (ifaceActionName != null)
                {
                    info.ActionAcceptsTypes[ifaceActionName] = acceptsType;
                }
            }
        }
    }

    private static void CollectImplementsMappingInfo(
        LambdaExpressionSyntax lambda, SemanticModel model, ObjectTypeInfo info, string interfaceTypeName)
    {
        var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            if (calledMethod.Name == "Via" &&
                invocation.ArgumentList.Arguments.Count >= 2)
            {
                // Via(p => p.SourceProp, i => i.TargetProp)
                var sourceProp = ExtractPropertyNameFromLambdaArg(invocation.ArgumentList.Arguments[0].Expression);
                if (sourceProp != null)
                {
                    info.InterfaceViaMappings.Add((interfaceTypeName, sourceProp, invocation.GetLocation()));
                }
            }

            if (calledMethod.Name == "ActionVia" &&
                invocation.ArgumentList.Arguments.Count >= 2)
            {
                var ifaceAction = ExtractStringArg(invocation, 0);
                var concreteAction = ExtractStringArg(invocation, 1);
                if (ifaceAction != null && concreteAction != null)
                {
                    info.InterfaceActionMappings.Add((interfaceTypeName, ifaceAction, concreteAction, invocation.GetLocation()));
                }
            }

            if (calledMethod.Name == "ActionDefault" &&
                invocation.ArgumentList.Arguments.Count >= 1)
            {
                var ifaceAction = ExtractStringArg(invocation, 0);
                if (ifaceAction != null)
                {
                    // ActionDefault implicitly maps the action
                    info.InterfaceActionMappings.Add((interfaceTypeName, ifaceAction, ifaceAction, invocation.GetLocation()));
                }
            }
        }
    }

    private static void ReportDiagnostics(SyntaxNodeAnalysisContext context, DomainAnalysisInfo info)
    {
        ReportDuplicateObjectTypes(context, info);

        foreach (var kvp in info.ObjectTypes)
        {
            var ot = kvp.Value;
            ReportObjectTypeBasicDiagnostics(context, ot);
            ReportLinkDiagnostics(context, info, ot);
            ReportActionDiagnostics(context, ot);
            ReportPostconditionOverlapDiagnostics(context, ot);
            ReportLifecycleDiagnostics(context, ot);
            ReportDerivedPropertyDiagnostics(context, ot);
            ReportInterfaceActionDiagnostics(context, info, ot);
        }

        ReportInterfaceNoImplementorsDiagnostics(context, info);
        ReportCrossDomainLinkDiagnostics(context, info);
        ReportExtensionPointDiagnostics(context, info);
    }

    private static void ReportDuplicateObjectTypes(
        SyntaxNodeAnalysisContext context, DomainAnalysisInfo info)
    {
        // AONT006: Duplicate object types
        foreach (var (name, location) in info.DuplicateObjectTypes)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.DuplicateObjectType, location, name, "domain"));
        }
    }

    private static void ReportObjectTypeBasicDiagnostics(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        // AONT001: Missing Key()
        if (!ot.HasKey)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.MissingKey, ot.Location, ot.Name));
        }

        // AONT002: Invalid property expression
        foreach (var location in ot.InvalidPropertyExpressions)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.InvalidPropertyExpression, location, ot.Name));
        }

        // AONT005: Interface mapping references non-existent property
        foreach (var (interfaceType, propName, location) in ot.InterfaceViaMappings)
        {
            if (!ot.DeclaredProperties.Contains(propName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InterfaceMappingBadProperty, location, ot.Name, propName));
            }
        }

        // AONT008: Edge type missing properties
        foreach (var (linkName, location) in ot.EdgesWithoutProperties)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.EdgeTypeMissingProperty, location, linkName, ot.Name));
        }
    }

    private static void ReportLinkDiagnostics(
        SyntaxNodeAnalysisContext context, DomainAnalysisInfo info, ObjectTypeInfo ot)
    {
        // AONT003: Link target not registered
        foreach (var (linkName, targetType, location) in ot.LinkTargets)
        {
            if (!info.ObjectTypes.ContainsKey(targetType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LinkTargetNotRegistered, location, linkName, ot.Name, targetType));
            }
        }
    }

    private static void ReportActionDiagnostics(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        // AONT004: Action not bound
        foreach (var actionName in ot.DeclaredActions)
        {
            if (!ot.BoundActions.Contains(actionName) && ot.ActionLocations.TryGetValue(actionName, out var loc))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.ActionNotBound, loc, actionName, ot.Name));
            }
        }

        // AONT009: EmitsEvent undeclared
        foreach (var (actionName, eventType, location) in ot.ActionEmitsEvents)
        {
            if (!ot.DeclaredEvents.Contains(eventType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.EmitsEventUndeclared, location, actionName, ot.Name, eventType));
            }
        }

        // AONT010: Modifies undeclared property
        foreach (var (actionName, propName, location) in ot.ActionModifiesProperties)
        {
            if (!ot.DeclaredProperties.Contains(propName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.ModifiesUndeclaredProperty, location, actionName, ot.Name, propName));
            }
        }

        // AONT011: CreatesLinked undeclared
        foreach (var (actionName, linkName, location) in ot.ActionCreatesLinked)
        {
            if (!ot.DeclaredLinks.Contains(linkName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.CreatesLinkedUndeclared, location, actionName, ot.Name, linkName));
            }
        }

        // AONT012: RequiresLink undeclared
        foreach (var (actionName, linkName, location) in ot.ActionRequiresLinks)
        {
            if (!ot.DeclaredLinks.Contains(linkName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.RequiresLinkUndeclared, location, actionName, ot.Name, linkName));
            }
        }

        // AONT036: ReadOnly action declares mutating chain call. Keyed on
        // mutating-call presence (not on parsed payload), so non-literal
        // Modifies(...) / CreatesLinked(...) shapes still trigger the
        // diagnostic when chained with ReadOnly().
        foreach (var (actionName, mutator, location) in ot.ActionMutationCalls)
        {
            if (ot.ReadOnlyActions.Contains(actionName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.ReadOnlyConflictsWithMutation, location, actionName, ot.Name, mutator));
            }
        }
    }

    private static void ReportPostconditionOverlapDiagnostics(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        // AONT013: Postcondition overlaps event
        var eventUpdatedProps = new HashSet<string>(ot.EventUpdatesProperties.Select(e => e.PropertyName));
        foreach (var (actionName, propName, location) in ot.ActionModifiesProperties)
        {
            if (!eventUpdatedProps.Contains(propName))
            {
                continue;
            }

            var actionEvents = new HashSet<string>(
                ot.ActionEmitsEvents.Where(e => e.ActionName == actionName).Select(e => e.EventType));
            var overlapping = ot.EventUpdatesProperties
                .Any(e => e.PropertyName == propName && actionEvents.Contains(e.EventType));
            if (overlapping)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.PostconditionOverlapsEvent, location, actionName, ot.Name, propName));
            }
        }
    }

    private static void ReportLifecycleDiagnostics(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        // AONT014: Lifecycle property undeclared
        if (ot.LifecyclePropertyName != null && !ot.DeclaredProperties.Contains(ot.LifecyclePropertyName))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.LifecyclePropertyUndeclared, ot.LifecycleLocation!, ot.Name, ot.LifecyclePropertyName));
        }

        // AONT015: Lifecycle initial state count
        if (ot.LifecyclePropertyName != null && ot.LifecycleInitialCount != 1)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.LifecycleInitialStateCount, ot.LifecycleLocation!, ot.Name, ot.LifecycleInitialCount.ToString()));
        }

        // AONT016: Lifecycle no terminal
        if (ot.LifecyclePropertyName != null && ot.LifecycleTerminalCount == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.LifecycleNoTerminalState, ot.LifecycleLocation!, ot.Name));
        }

        // AONT017: Transition references undeclared state
        foreach (var (from, to, location) in ot.LifecycleTransitions)
        {
            if (!ot.LifecycleStates.Contains(from))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LifecycleTransitionBadState, location, ot.Name, from));
            }

            if (!ot.LifecycleStates.Contains(to))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LifecycleTransitionBadState, location, ot.Name, to));
            }
        }

        // AONT018: TriggeredByAction references undeclared action
        foreach (var (actionName, location) in ot.LifecycleTransitionActions)
        {
            if (!ot.DeclaredActions.Contains(actionName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LifecycleTransitionBadAction, location, ot.Name, actionName));
            }
        }

        // AONT019: TriggeredByEvent references undeclared event
        foreach (var (eventType, location) in ot.LifecycleTransitionEvents)
        {
            if (!ot.DeclaredEvents.Contains(eventType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LifecycleTransitionBadEvent, location, ot.Name, eventType));
            }
        }

        ReportLifecycleReachabilityDiagnostics(context, ot);
    }

    private static void ReportLifecycleReachabilityDiagnostics(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        if (ot.LifecyclePropertyName == null)
        {
            return;
        }

        // AONT020: Unreachable state (not Initial and not target of any transition)
        var reachableStates = new HashSet<string>(ot.LifecycleInitialStates);
        foreach (var (_, to, _) in ot.LifecycleTransitions)
        {
            reachableStates.Add(to);
        }

        foreach (var state in ot.LifecycleStates)
        {
            if (!reachableStates.Contains(state))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LifecycleUnreachableState, ot.LifecycleLocation!, state, ot.Name));
            }
        }

        // AONT021: Dead-end non-terminal state (no outgoing transitions and not Terminal)
        var statesWithOutgoing = new HashSet<string>(ot.LifecycleTransitions.Select(t => t.FromState));
        foreach (var state in ot.LifecycleStates)
        {
            if (!statesWithOutgoing.Contains(state) && !ot.LifecycleTerminalStates.Contains(state))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.LifecycleDeadEndState, ot.LifecycleLocation!, state, ot.Name));
            }
        }
    }

    private static void ReportDerivedPropertyDiagnostics(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        // AONT022: DerivedFrom references undeclared property
        foreach (var (propName, sourceProp, location) in ot.DerivedFromReferences)
        {
            if (!ot.DeclaredProperties.Contains(sourceProp))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.DerivedFromUndeclaredProperty, location, propName, ot.Name, sourceProp));
            }
        }

        // AONT023: DerivedFrom on non-Computed property
        foreach (var propName in ot.PropertiesWithDerivedFrom)
        {
            if (!ot.ComputedProperties.Contains(propName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.DerivedFromNonComputed, ot.Location, propName, ot.Name));
            }
        }

        // AONT024: Derivation cycle detection
        DetectDerivationCycles(context, ot);

        // AONT025: DerivedFromExternal unresolvable
        foreach (var (propName, domain, objectType, property, location) in ot.DerivedFromExternalReferences)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.DerivedFromExternalUnresolvable, location,
                propName, ot.Name, domain, objectType, property));
        }

        // AONT026: Computed but no DerivedFrom
        foreach (var propName in ot.ComputedProperties)
        {
            if (!ot.PropertiesWithDerivedFrom.Contains(propName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.ComputedNoDerivedFrom, ot.Location, propName, ot.Name));
            }
        }
    }

    private static void ReportInterfaceActionDiagnostics(
        SyntaxNodeAnalysisContext context, DomainAnalysisInfo info, ObjectTypeInfo ot)
    {
        // AONT027/028/029: Interface action mappings
        foreach (var interfaceTypeName in ot.ImplementedInterfaces)
        {
            ReportUnmappedInterfaceActions(context, info, ot, interfaceTypeName);
            ReportBadActionViaReferences(context, ot, interfaceTypeName);
            ReportIncompatibleAcceptsTypes(context, info, ot, interfaceTypeName);
        }
    }

    private static void ReportUnmappedInterfaceActions(
        SyntaxNodeAnalysisContext context,
        DomainAnalysisInfo info,
        ObjectTypeInfo ot,
        string interfaceTypeName)
    {
        // AONT027: Interface action unmapped
        if (!info.Interfaces.TryGetValue(interfaceTypeName, out var ifaceInfo) &&
            !TryFindInterfaceByTypeName(info.Interfaces, interfaceTypeName, out ifaceInfo))
        {
            return;
        }

        var mappedActions = new HashSet<string>(
            ot.InterfaceActionMappings
                .Where(m => m.InterfaceType == interfaceTypeName)
                .Select(m => m.InterfaceAction));

        foreach (var ifaceAction in ifaceInfo.DeclaredActions)
        {
            if (!mappedActions.Contains(ifaceAction))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InterfaceActionUnmapped, ot.Location,
                    ot.Name, ifaceInfo.Name, ifaceAction));
            }
        }
    }

    private static void ReportBadActionViaReferences(
        SyntaxNodeAnalysisContext context, ObjectTypeInfo ot, string interfaceTypeName)
    {
        // AONT028: ActionVia references undeclared action
        foreach (var (ifType, ifAction, concreteAction, location) in ot.InterfaceActionMappings)
        {
            if (ifType == interfaceTypeName && !ot.DeclaredActions.Contains(concreteAction) &&
                concreteAction != ifAction)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.ActionViaBadReference, location,
                    ot.Name, ifAction, concreteAction));
            }
        }
    }

    private static void ReportIncompatibleAcceptsTypes(
        SyntaxNodeAnalysisContext context,
        DomainAnalysisInfo info,
        ObjectTypeInfo ot,
        string interfaceTypeName)
    {
        // AONT029: Interface action Accepts<T> incompatible with concrete action
        if (!info.Interfaces.TryGetValue(interfaceTypeName, out var ifaceInfo) &&
            !TryFindInterfaceByTypeName(info.Interfaces, interfaceTypeName, out ifaceInfo))
        {
            return;
        }

        foreach (var (ifType, ifAction, concreteAction, location) in ot.InterfaceActionMappings)
        {
            if (ifType != interfaceTypeName)
            {
                continue;
            }

            if (ifaceInfo.ActionAcceptsTypes.TryGetValue(ifAction, out var ifaceAcceptsType) &&
                ot.ActionAcceptsTypes.TryGetValue(concreteAction, out var concreteAcceptsType) &&
                ifaceAcceptsType != concreteAcceptsType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.InterfaceActionIncompatible, location,
                    ifAction, ifaceAcceptsType, concreteAction, concreteAcceptsType));
            }
        }
    }

    private static void ReportInterfaceNoImplementorsDiagnostics(
        SyntaxNodeAnalysisContext context, DomainAnalysisInfo info)
    {
        // AONT030: Interface declares actions but no implementors
        foreach (var kvp in info.Interfaces)
        {
            var ifaceInfo = kvp.Value;
            if (ifaceInfo.DeclaredActions.Count == 0)
            {
                continue;
            }

            var hasImplementor = info.ObjectTypes.Values
                .Any(ot => ot.ImplementedInterfaces.Contains(ifaceInfo.Name));
            if (!hasImplementor)
            {
                foreach (var actionName in ifaceInfo.DeclaredActions)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.InterfaceActionNoImplementors, ifaceInfo.Location,
                        ifaceInfo.Name, actionName));
                }
            }
        }
    }

    private static void ReportCrossDomainLinkDiagnostics(
        SyntaxNodeAnalysisContext context, DomainAnalysisInfo info)
    {
        // AONT007: Cross-domain link unverifiable
        foreach (var link in info.CrossDomainLinks)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.CrossDomainLinkUnverifiable, link.Location,
                link.Name, "external", "external domain"));
        }

        // Build lookup: which object types have extension points
        var typesWithExtensionPoints = new HashSet<string>();
        foreach (var kvpOt in info.ObjectTypes)
        {
            if (kvpOt.Value.ExtensionPoints.Count > 0)
            {
                typesWithExtensionPoints.Add(kvpOt.Key);
            }
        }

        // AONT031: Cross-domain link targets type with no extension point
        foreach (var link in info.CrossDomainLinks)
        {
            if (link.SourceType != null && !typesWithExtensionPoints.Contains(link.SourceType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.CrossDomainLinkNoExtensionPoint, link.Location,
                    link.Name, link.SourceType ?? "unknown"));
            }
        }
    }

    private static void ReportExtensionPointDiagnostics(
        SyntaxNodeAnalysisContext context, DomainAnalysisInfo info)
    {
        // AONT032/033/034/035: Extension point validations
        foreach (var kvpOt in info.ObjectTypes)
        {
            var ot = kvpOt.Value;
            foreach (var ep in ot.ExtensionPoints)
            {
                ReportSingleExtensionPointDiagnostics(context, info, ot, ep);
            }
        }
    }

    private static void ReportSingleExtensionPointDiagnostics(
        SyntaxNodeAnalysisContext context,
        DomainAnalysisInfo info,
        ObjectTypeInfo ot,
        ExtensionPointInfo ep)
    {
        // AONT032: Extension point interface constraint unsatisfied
        if (ep.RequiredInterface != null && !ot.ImplementedInterfaces.Contains(ep.RequiredInterface))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.ExtensionPointInterfaceUnsatisfied, ep.Location,
                ep.Name, ot.Name, ep.RequiredInterface));
        }

        var matchingLinks = info.CrossDomainLinks
            .Where(l => l.SourceType == ot.Name)
            .ToList();

        // AONT033: Extension point requires edge property missing from link
        if (ep.RequiredEdgeProperties.Count > 0)
        {
            ReportMissingEdgeProperties(context, ot, ep, matchingLinks);
        }

        // AONT034: Extension point declared but no links match
        if (matchingLinks.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.ExtensionPointNoLinks, ep.Location,
                ep.Name, ot.Name));
        }

        // AONT035: Max links exceeded
        if (ep.MaxLinks.HasValue && matchingLinks.Count > ep.MaxLinks.Value)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.ExtensionPointMaxLinksExceeded, ep.Location,
                ep.Name, ot.Name, ep.MaxLinks.Value.ToString(), matchingLinks.Count.ToString()));
        }
    }

    private static void ReportMissingEdgeProperties(
        SyntaxNodeAnalysisContext context,
        ObjectTypeInfo ot,
        ExtensionPointInfo ep,
        List<CrossDomainLinkInfo> matchingLinks)
    {
        foreach (var link in matchingLinks)
        {
            foreach (var reqProp in ep.RequiredEdgeProperties)
            {
                if (!link.EdgeProperties.Contains(reqProp))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.ExtensionPointEdgeMissing, ep.Location,
                        ep.Name, ot.Name, reqProp));
                }
            }
        }

        // Also report if there are required edge properties but no links to validate
        if (matchingLinks.Count == 0)
        {
            foreach (var reqProp in ep.RequiredEdgeProperties)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.ExtensionPointEdgeMissing, ep.Location,
                    ep.Name, ot.Name, reqProp));
            }
        }
    }

    private static void DetectDerivationCycles(SyntaxNodeAnalysisContext context, ObjectTypeInfo ot)
    {
        // Build adjacency list: property -> properties it derives from
        var graph = new Dictionary<string, List<string>>();
        foreach (var (propName, sourceProp, _) in ot.DerivedFromReferences)
        {
            if (!graph.ContainsKey(propName))
            {
                graph[propName] = new List<string>();
            }

            graph[propName].Add(sourceProp);
        }

        // DFS cycle detection
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        foreach (var node in graph.Keys)
        {
            if (visited.Contains(node))
            {
                continue;
            }

            var path = new List<string>();
            if (HasCycle(node, graph, visited, inStack, path))
            {
                // Find the cycle portion of the path
                var cycleStart = path.Last();
                var cycleStartIndex = path.IndexOf(cycleStart);
                var cyclePath = string.Join(" -> ", path.Skip(cycleStartIndex));

                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.DerivationCycle, ot.Location, ot.Name, cyclePath));
                return; // Report only first cycle found
            }
        }
    }

    private static bool HasCycle(
        string node,
        Dictionary<string, List<string>> graph,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path)
    {
        visited.Add(node);
        inStack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (inStack.Contains(neighbor))
                {
                    path.Add(neighbor);
                    return true;
                }

                if (!visited.Contains(neighbor) && HasCycle(neighbor, graph, visited, inStack, path))
                {
                    return true;
                }
            }
        }

        inStack.Remove(node);
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static bool TryFindInterfaceByTypeName(
        Dictionary<string, InterfaceInfo> interfaces, string typeName, out InterfaceInfo result)
    {
        // The interface might be registered with a user-given name but the object type
        // uses typeof(T).Name. Try both lookups.
        foreach (var kvp in interfaces)
        {
            if (kvp.Key == typeName || kvp.Value.Name == typeName)
            {
                result = kvp.Value;
                return true;
            }
        }

        result = null!;
        return false;
    }

    private static string? ExtractStringArg(InvocationExpressionSyntax invocation, int index)
    {
        if (invocation.ArgumentList.Arguments.Count <= index)
        {
            return null;
        }

        var arg = invocation.ArgumentList.Arguments[index];
        if (arg.Expression is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    private static string? ExtractPropertyNameFromExpression(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
        return ExtractPropertyNameFromLambdaArg(firstArg);
    }

    private static string? ExtractPropertyNameFromLambdaArg(ExpressionSyntax expression)
    {
        ExpressionSyntax? body = expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body as ExpressionSyntax,
            ParenthesizedLambdaExpressionSyntax parens => parens.Body as ExpressionSyntax,
            _ => null,
        };

        if (body is null)
        {
            return null;
        }

        // p => p.PropertyName or (p) => p.PropertyName
        if (body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }

        // p => (object)p.PropertyName or (p) => (object)p.PropertyName (boxing)
        if (body is CastExpressionSyntax castBody &&
            castBody.Expression is MemberAccessExpressionSyntax castMember)
        {
            return castMember.Name.Identifier.Text;
        }

        return null;
    }

    private static string? ExtractEnumValueName(ExpressionSyntax expression)
    {
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }

        return null;
    }

    private static string? FindActionNameInChain(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        // Walk the fluent chain backwards to find the Action("name") call
        var current = invocation.Expression;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax parentInvocation)
            {
                var parentSymbol = model.GetSymbolInfo(parentInvocation);
                if (parentSymbol.Symbol is IMethodSymbol parentMethod && parentMethod.Name == "Action")
                {
                    return ExtractStringArg(parentInvocation, 0);
                }

                current = parentInvocation.Expression;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    private static string? FindPropertyNameInChain(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        // Walk the fluent chain backwards to find the Property(p => p.Name) call
        var current = invocation.Expression;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax parentInvocation)
            {
                var parentSymbol = model.GetSymbolInfo(parentInvocation);
                if (parentSymbol.Symbol is IMethodSymbol parentMethod && parentMethod.Name == "Property")
                {
                    return ExtractPropertyNameFromExpression(parentInvocation);
                }

                current = parentInvocation.Expression;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    private static string? FindActionNameInChainSyntactic(InvocationExpressionSyntax invocation)
    {
        // Walk the fluent chain backwards to find the Action("name") call, syntactically
        var current = invocation.Expression;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax parentInvocation)
            {
                if (parentInvocation.Expression is MemberAccessExpressionSyntax parentMember &&
                    parentMember.Name.Identifier.Text == "Action")
                {
                    return ExtractStringArg(parentInvocation, 0);
                }

                current = parentInvocation.Expression;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    private static string? FindStateNameInChain(InvocationExpressionSyntax invocation)
    {
        // Walk the fluent chain backwards to find the State(Enum.Value) call
        // Pattern: lc.State(Status.Draft).Initial()
        var current = invocation.Expression;
        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax parentInvocation)
            {
                // Check if the parent is a State() call by looking at the method name syntactically
                if (parentInvocation.Expression is MemberAccessExpressionSyntax parentMemberAccess &&
                    parentMemberAccess.Name.Identifier.Text == "State")
                {
                    var stateArg = parentInvocation.ArgumentList.Arguments.FirstOrDefault();
                    if (stateArg != null)
                    {
                        return ExtractEnumValueName(stateArg.Expression);
                    }
                }

                current = parentInvocation.Expression;
            }
            else
            {
                break;
            }
        }

        return null;
    }

    private static bool IsOntologyBuilderType(string typeName)
    {
        return typeName == "IOntologyBuilder" || typeName == "OntologyBuilder";
    }

    private static bool IsObjectTypeBuilderMethod(IMethodSymbol method)
    {
        var containingTypeName = method.ContainingType?.Name ?? "";
        return containingTypeName.StartsWith("IObjectTypeBuilder", StringComparison.Ordinal) ||
               containingTypeName.StartsWith("ObjectTypeBuilder", StringComparison.Ordinal);
    }

    // --- Analysis data structures ---

    private sealed class DomainAnalysisInfo
    {
        public Dictionary<string, ObjectTypeInfo> ObjectTypes { get; } = new Dictionary<string, ObjectTypeInfo>();
        public List<(string Name, Location Location)> DuplicateObjectTypes { get; } = new List<(string, Location)>();
        public List<CrossDomainLinkInfo> CrossDomainLinks { get; } = new List<CrossDomainLinkInfo>();
        public Dictionary<string, InterfaceInfo> Interfaces { get; } = new Dictionary<string, InterfaceInfo>();
    }

    private sealed class ObjectTypeInfo
    {
        public ObjectTypeInfo(string name, Location location)
        {
            Name = name;
            Location = location;
        }

        public string Name { get; }
        public Location Location { get; }
        public bool HasKey { get; set; }
        public HashSet<string> DeclaredProperties { get; } = new HashSet<string>();
        public HashSet<string> DeclaredLinks { get; } = new HashSet<string>();
        public HashSet<string> DeclaredActions { get; } = new HashSet<string>();
        public HashSet<string> DeclaredEvents { get; } = new HashSet<string>();
        public HashSet<string> BoundActions { get; } = new HashSet<string>();
        public HashSet<string> ReadOnlyActions { get; } = new HashSet<string>();
        public HashSet<string> ComputedProperties { get; } = new HashSet<string>();
        public HashSet<string> PropertiesWithDerivedFrom { get; } = new HashSet<string>();
        public HashSet<string> ImplementedInterfaces { get; } = new HashSet<string>();

        public List<Location> InvalidPropertyExpressions { get; } = new List<Location>();

        public List<(string LinkName, string TargetType, Location Location)> LinkTargets { get; } =
            new List<(string, string, Location)>();

        public List<(string LinkName, Location Location)> EdgesWithoutProperties { get; } =
            new List<(string, Location)>();

        public Dictionary<string, Location> ActionLocations { get; } = new Dictionary<string, Location>();

        public Dictionary<string, string> ActionAcceptsTypes { get; } = new Dictionary<string, string>();

        public List<(string ActionName, string EventType, Location Location)> ActionEmitsEvents { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string PropertyName, Location Location)> ActionModifiesProperties { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string LinkName, Location Location)> ActionCreatesLinked { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string Mutator, Location Location)> ActionMutationCalls { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string LinkName, Location Location)> ActionRequiresLinks { get; } =
            new List<(string, string, Location)>();

        public List<(string EventType, string PropertyName, Location Location)> EventUpdatesProperties { get; } =
            new List<(string, string, Location)>();

        public List<(string PropertyName, string SourceProperty, Location Location)> DerivedFromReferences { get; } =
            new List<(string, string, Location)>();

        public List<(string PropertyName, string Domain, string ObjectType, string Property, Location Location)> DerivedFromExternalReferences { get; } =
            new List<(string, string, string, string, Location)>();

        // Lifecycle
        public string? LifecyclePropertyName { get; set; }
        public Location? LifecycleLocation { get; set; }
        public HashSet<string> LifecycleStates { get; } = new HashSet<string>();
        public int LifecycleInitialCount { get; set; }
        public int LifecycleTerminalCount { get; set; }
        public HashSet<string> LifecycleInitialStates { get; } = new HashSet<string>();
        public HashSet<string> LifecycleTerminalStates { get; } = new HashSet<string>();

        public List<(string FromState, string ToState, Location Location)> LifecycleTransitions { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, Location Location)> LifecycleTransitionActions { get; } =
            new List<(string, Location)>();

        public List<(string EventType, Location Location)> LifecycleTransitionEvents { get; } =
            new List<(string, Location)>();

        // Interface action mappings
        public List<(string InterfaceType, string InterfaceAction, string ConcreteAction, Location Location)> InterfaceActionMappings { get; } =
            new List<(string, string, string, Location)>();

        // Interface Via() property mappings
        public List<(string InterfaceType, string PropertyName, Location Location)> InterfaceViaMappings { get; } =
            new List<(string, string, Location)>();

        // Extension points
        public List<ExtensionPointInfo> ExtensionPoints { get; } = new List<ExtensionPointInfo>();
    }

    private sealed class CrossDomainLinkInfo
    {
        public CrossDomainLinkInfo(string name, Location location)
        {
            Name = name;
            Location = location;
        }

        public string Name { get; }
        public Location Location { get; }
        public string? SourceType { get; set; }
        public HashSet<string> EdgeProperties { get; } = new HashSet<string>();
    }

    private sealed class ExtensionPointInfo
    {
        public ExtensionPointInfo(string name, Location location)
        {
            Name = name;
            Location = location;
        }

        public string Name { get; }
        public Location Location { get; }
        public string? RequiredInterface { get; set; }
        public HashSet<string> RequiredEdgeProperties { get; } = new HashSet<string>();
        public int? MaxLinks { get; set; }
    }

    private sealed class InterfaceInfo
    {
        public InterfaceInfo(string name, Location location)
        {
            Name = name;
            Location = location;
        }

        public string Name { get; }
        public Location Location { get; }
        public HashSet<string> DeclaredActions { get; } = new HashSet<string>();
        public Dictionary<string, string> ActionAcceptsTypes { get; } = new Dictionary<string, string>();
    }
}
