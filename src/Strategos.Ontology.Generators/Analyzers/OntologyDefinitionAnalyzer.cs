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
            OntologyDiagnostics.LinkTargetNotRegistered,
            OntologyDiagnostics.ActionNotBound,
            OntologyDiagnostics.DuplicateObjectType,
            OntologyDiagnostics.CrossDomainLinkUnverifiable,
            OntologyDiagnostics.EmitsEventUndeclared,
            OntologyDiagnostics.ModifiesUndeclaredProperty,
            OntologyDiagnostics.CreatesLinkedUndeclared,
            OntologyDiagnostics.RequiresLinkUndeclared,
            OntologyDiagnostics.LifecyclePropertyUndeclared,
            OntologyDiagnostics.LifecycleInitialStateCount,
            OntologyDiagnostics.LifecycleNoTerminalState,
            OntologyDiagnostics.LifecycleTransitionBadState,
            OntologyDiagnostics.LifecycleTransitionBadAction,
            OntologyDiagnostics.DerivedFromUndeclaredProperty,
            OntologyDiagnostics.DerivedFromNonComputed,
            OntologyDiagnostics.ComputedNoDerivedFrom,
            OntologyDiagnostics.InterfaceActionUnmapped,
            OntologyDiagnostics.ActionViaBadReference);

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
                    info.CrossDomainLinks.Add(new CrossDomainLinkInfo(linkName, invocation.GetLocation()));
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
                    var propName = ExtractPropertyNameFromExpression(invocation);
                    if (propName != null)
                    {
                        info.DeclaredProperties.Add(propName);
                    }

                    break;

                case "HasOne" or "HasMany" or "ManyToMany":
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

                    break;

                case "Action" when IsObjectTypeBuilderMethod(calledMethod):
                    var actionName = ExtractStringArg(invocation, 0);
                    if (actionName != null)
                    {
                        info.DeclaredActions.Add(actionName);
                        info.ActionLocations[actionName] = invocation.GetLocation();
                    }

                    break;

                case "Event":
                    if (calledMethod.TypeArguments.Length > 0)
                    {
                        info.DeclaredEvents.Add(calledMethod.TypeArguments[0].Name);
                    }

                    break;

                case "BoundToWorkflow":
                    // Walk up the fluent chain to find which action this is on
                    var boundActionName = FindActionNameInChain(invocation, model);
                    if (boundActionName != null)
                    {
                        info.BoundActions.Add(boundActionName);
                    }

                    break;

                case "Modifies":
                    var modifiesActionName = FindActionNameInChain(invocation, model);
                    var modifiesProp = ExtractPropertyNameFromExpression(invocation);
                    if (modifiesActionName != null && modifiesProp != null)
                    {
                        info.ActionModifiesProperties.Add((modifiesActionName, modifiesProp, invocation.GetLocation()));
                    }

                    break;

                case "EmitsEvent":
                    var emitsActionName = FindActionNameInChain(invocation, model);
                    if (emitsActionName != null && calledMethod.TypeArguments.Length > 0)
                    {
                        var eventTypeName = calledMethod.TypeArguments[0].Name;
                        info.ActionEmitsEvents.Add((emitsActionName, eventTypeName, invocation.GetLocation()));
                    }

                    break;

                case "CreatesLinked":
                    var createsActionName = FindActionNameInChain(invocation, model);
                    var createsLinkName = ExtractStringArg(invocation, 0);
                    if (createsActionName != null && createsLinkName != null)
                    {
                        info.ActionCreatesLinked.Add((createsActionName, createsLinkName, invocation.GetLocation()));
                    }

                    break;

                case "RequiresLink":
                    var reqActionName = FindActionNameInChain(invocation, model);
                    var reqLinkName = ExtractStringArg(invocation, 0);
                    if (reqActionName != null && reqLinkName != null)
                    {
                        info.ActionRequiresLinks.Add((reqActionName, reqLinkName, invocation.GetLocation()));
                    }

                    break;

                case "Computed":
                    var computedPropName = FindPropertyNameInChain(invocation, model);
                    if (computedPropName != null)
                    {
                        info.ComputedProperties.Add(computedPropName);
                    }

                    break;

                case "DerivedFrom" when !calledMethod.Name.Contains("External"):
                    var derivedPropName = FindPropertyNameInChain(invocation, model);
                    if (derivedPropName != null)
                    {
                        info.PropertiesWithDerivedFrom.Add(derivedPropName);

                        // Extract the referenced property names from expressions
                        foreach (var arg in invocation.ArgumentList.Arguments)
                        {
                            var sourceProp = ExtractPropertyNameFromLambdaArg(arg.Expression);
                            if (sourceProp != null)
                            {
                                info.DerivedFromReferences.Add((derivedPropName, sourceProp, invocation.GetLocation()));
                            }
                        }
                    }

                    break;

                case "Lifecycle":
                    var lifecyclePropName = ExtractPropertyNameFromExpression(invocation);
                    if (lifecyclePropName != null)
                    {
                        info.LifecyclePropertyName = lifecyclePropName;
                        info.LifecycleLocation = invocation.GetLocation();
                    }

                    // Parse lifecycle lambda
                    if (invocation.ArgumentList.Arguments.Count > 1)
                    {
                        var lifecycleLambdaArg = invocation.ArgumentList.Arguments.Last();
                        CollectLifecycleInfo(lifecycleLambdaArg.Expression, model, info);
                    }

                    break;

                case "Implements":
                    if (calledMethod.TypeArguments.Length > 0)
                    {
                        var interfaceTypeName = calledMethod.TypeArguments[0].Name;
                        info.ImplementedInterfaces.Add(interfaceTypeName);

                        // Parse implements lambda for action mappings
                        if (invocation.ArgumentList.Arguments.Count > 0)
                        {
                            var implLambda = invocation.ArgumentList.Arguments.Last().Expression;
                            if (implLambda is LambdaExpressionSyntax implLambdaSyntax)
                            {
                                CollectImplementsMappingInfo(implLambdaSyntax, model, info, interfaceTypeName);
                            }
                        }
                    }

                    break;
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
                    break;

                case "Terminal":
                    info.LifecycleTerminalCount++;
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
        // AONT006: Duplicate object types
        foreach (var (name, location) in info.DuplicateObjectTypes)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.DuplicateObjectType, location, name, "domain"));
        }

        foreach (var kvp in info.ObjectTypes)
        {
            var ot = kvp.Value;

            // AONT001: Missing Key()
            if (!ot.HasKey)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    OntologyDiagnostics.MissingKey, ot.Location, ot.Name));
            }

            // AONT003: Link target not registered
            foreach (var (linkName, targetType, location) in ot.LinkTargets)
            {
                if (!info.ObjectTypes.ContainsKey(targetType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.LinkTargetNotRegistered, location, linkName, ot.Name, targetType));
                }
            }

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

            // AONT026: Computed but no DerivedFrom
            foreach (var propName in ot.ComputedProperties)
            {
                if (!ot.PropertiesWithDerivedFrom.Contains(propName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        OntologyDiagnostics.ComputedNoDerivedFrom, ot.Location, propName, ot.Name));
                }
            }

            // AONT027/028: Interface action mappings
            foreach (var interfaceTypeName in ot.ImplementedInterfaces)
            {
                if (info.Interfaces.TryGetValue(interfaceTypeName, out var ifaceInfo) ||
                    TryFindInterfaceByTypeName(info.Interfaces, interfaceTypeName, out ifaceInfo))
                {
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

                // AONT028: ActionVia references undeclared action
                foreach (var (ifType, ifAction, concreteAction, location) in ot.InterfaceActionMappings)
                {
                    // ActionDefault creates the action, so skip validation for those
                    if (ifType == interfaceTypeName && !ot.DeclaredActions.Contains(concreteAction) &&
                        concreteAction != ifAction)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            OntologyDiagnostics.ActionViaBadReference, location,
                            ot.Name, ifAction, concreteAction));
                    }
                }
            }
        }

        // AONT007: Cross-domain link unverifiable
        foreach (var link in info.CrossDomainLinks)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                OntologyDiagnostics.CrossDomainLinkUnverifiable, link.Location,
                link.Name, "external", "external domain"));
        }
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
        if (expression is not SimpleLambdaExpressionSyntax lambda)
        {
            return null;
        }

        // p => p.PropertyName
        if (lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.Text;
        }

        // p => (object)p.PropertyName (boxing)
        if (lambda.Body is CastExpressionSyntax castBody &&
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
        public HashSet<string> ComputedProperties { get; } = new HashSet<string>();
        public HashSet<string> PropertiesWithDerivedFrom { get; } = new HashSet<string>();
        public HashSet<string> ImplementedInterfaces { get; } = new HashSet<string>();

        public List<(string LinkName, string TargetType, Location Location)> LinkTargets { get; } =
            new List<(string, string, Location)>();

        public Dictionary<string, Location> ActionLocations { get; } = new Dictionary<string, Location>();

        public List<(string ActionName, string EventType, Location Location)> ActionEmitsEvents { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string PropertyName, Location Location)> ActionModifiesProperties { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string LinkName, Location Location)> ActionCreatesLinked { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, string LinkName, Location Location)> ActionRequiresLinks { get; } =
            new List<(string, string, Location)>();

        public List<(string PropertyName, string SourceProperty, Location Location)> DerivedFromReferences { get; } =
            new List<(string, string, Location)>();

        // Lifecycle
        public string? LifecyclePropertyName { get; set; }
        public Location? LifecycleLocation { get; set; }
        public HashSet<string> LifecycleStates { get; } = new HashSet<string>();
        public int LifecycleInitialCount { get; set; }
        public int LifecycleTerminalCount { get; set; }

        public List<(string FromState, string ToState, Location Location)> LifecycleTransitions { get; } =
            new List<(string, string, Location)>();

        public List<(string ActionName, Location Location)> LifecycleTransitionActions { get; } =
            new List<(string, Location)>();

        // Interface action mappings
        public List<(string InterfaceType, string InterfaceAction, string ConcreteAction, Location Location)> InterfaceActionMappings { get; } =
            new List<(string, string, string, Location)>();
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
    }
}
