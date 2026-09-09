// -----------------------------------------------------------------------
// <copyright file="OntologyActionCatalog.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

using Strategos.Ontology.ActionLogic;
using Strategos.Ontology.Generators.Analyzers;

namespace Strategos.Analyzers.Proof;

/// <summary>
/// Builds the compilation-wide, ordinal ontology-action catalog consumed by
/// ontology inverse validation and workflow binding refinement proof.
/// </summary>
internal sealed class OntologyActionCatalog
{
    private const string DomainOntologyTypeName = "Strategos.Ontology.DomainOntology";
    private const string ActionDescriptorTypeName = "Strategos.Ontology.Descriptors.ActionDescriptor";
    private const string ObjectTypeDescriptorTypeName =
        "Strategos.Ontology.Descriptors.ObjectTypeDescriptor";
    private const string ActionBindingTypeName = "Strategos.Ontology.Descriptors.ActionBindingType";
    private const string WorkflowBindingReferenceTypeName =
        "Strategos.Ontology.Descriptors.WorkflowBindingReference";
    private const string OntologyBuilderTypeName = "Strategos.Ontology.Builder.IOntologyBuilder";
    private const string ObjectBuilderTypeName = "Strategos.Ontology.Builder.IObjectTypeBuilder<T>";

    private OntologyActionCatalog(
        ImmutableArray<OntologyActionContract> actions,
        ImmutableDictionary<string, ImmutableArray<OntologyAuthorityLattice>> authorityLattices)
    {
        Actions = actions;
        AuthorityLattices = authorityLattices;
    }

    internal ImmutableArray<OntologyActionContract> Actions { get; }

    internal ImmutableDictionary<string, ImmutableArray<OntologyAuthorityLattice>> AuthorityLattices { get; }

    internal static OntologyActionCatalog Build(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var actions = new List<OntologyActionContract>();
        var lattices = new Dictionary<string, List<OntologyAuthorityLattice>>(StringComparer.Ordinal);
        var parsedDirectActions = new HashSet<SyntaxNode>();
        var inventoriedBindings = new HashSet<InvocationExpressionSyntax>();

        foreach (var tree in compilation.SyntaxTrees.OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = tree.GetRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var type = semanticModel.GetDeclaredSymbol(declaration, cancellationToken)
                    as INamedTypeSymbol;
                if (!DerivesFrom(type, DomainOntologyTypeName))
                {
                    continue;
                }

                var define = declaration.Members.OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(method =>
                        semanticModel.GetDeclaredSymbol(method, cancellationToken)
                            is IMethodSymbol methodSymbol
                        && IsDomainOntologyDefineOverride(methodSymbol));
                if (define is null)
                {
                    continue;
                }

                var defineParameterSyntax = define.ParameterList.Parameters.SingleOrDefault();
                var defineBuilderParameter = defineParameterSyntax is null
                    ? null
                    : semanticModel.GetDeclaredSymbol(
                        defineParameterSyntax,
                        cancellationToken) as IParameterSymbol;
                var domainClosureFailure = GetDefineClosureFailure(
                    define,
                    semanticModel,
                    cancellationToken);

                var hasDomainName = TryGetDomainName(
                    type!,
                    compilation,
                    cancellationToken,
                    out var domainName);
                domainName = hasDomainName ? domainName : "<dynamic-domain>";
                var defineInvocations = define.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .ToImmutableArray();
                if (hasDomainName)
                {
                    var lattice = ParseAuthorityLattice(
                        domainName,
                        defineInvocations,
                        semanticModel,
                        defineBuilderParameter,
                        domainClosureFailure,
                        cancellationToken);
                    if (!lattices.TryGetValue(domainName, out var domainLattices))
                    {
                        domainLattices = new List<OntologyAuthorityLattice>();
                        lattices.Add(domainName, domainLattices);
                    }

                    domainLattices.Add(lattice);
                }

                foreach (var objectInvocation in defineInvocations)
                {
                    if (!TryGetObjectDeclaration(
                        objectInvocation,
                        semanticModel,
                        defineBuilderParameter,
                        cancellationToken,
                        out var objectTypeName,
                        out var configureLambda,
                        out var objectParameter,
                        out var objectIdentityFailure))
                    {
                        continue;
                    }

                    var objectClosureFailure = GetLambdaBuilderClosureFailure(
                        configureLambda,
                        objectParameter,
                        semanticModel,
                        "object configuration",
                        cancellationToken);

                    foreach (var actionInvocation in configureLambda.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>())
                    {
                        if (!IsActionRoot(
                            actionInvocation,
                            objectParameter,
                            semanticModel,
                            cancellationToken)
                            || !ActionCompositionAnalyzer.TryParseWorkflowFluentAction(
                                actionInvocation,
                                domainName,
                                objectTypeName,
                                semanticModel,
                                cancellationToken,
                                out var parsed))
                        {
                            continue;
                        }

                        actions.Add(OntologyActionContract.FromParsed(
                            parsed,
                            domainClosureFailure
                            ?? objectIdentityFailure
                            ?? objectClosureFailure
                            ?? (!hasDomainName && parsed.HasWorkflowBinding
                                ? "the ontology domain name is not statically closed"
                                : null)));
                        InventoryFluentBindings(
                            actionInvocation,
                            semanticModel,
                            cancellationToken,
                            inventoriedBindings);
                    }
                }

                foreach (var invocation in defineInvocations)
                {
                    if (inventoriedBindings.Contains(invocation)
                        || !ActionCompositionAnalyzer.TryParseStandaloneWorkflowBinding(
                            invocation,
                            semanticModel,
                            cancellationToken,
                            out var workflowName,
                            out var bindingFailure))
                    {
                        continue;
                    }

                    actions.Add(OntologyActionContract.InvalidWorkflowBinding(
                        domainName,
                        workflowName,
                        bindingFailure
                            ?? (hasDomainName
                                ? "the workflow binding is not part of one direct Action(...) fluent chain"
                                : "the ontology domain name is not statically closed"),
                        invocation.GetLocation()));
                    inventoriedBindings.Add(invocation);
                }

                InventoryDirectDescriptorActions(
                    define,
                    semanticModel,
                    domainClosureFailure,
                    cancellationToken,
                    actions,
                    parsedDirectActions);
            }

            foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (parsedDirectActions.Contains(creation)
                    || semanticModel.GetTypeInfo(creation, cancellationToken).Type?.ToDisplayString()
                        != ActionDescriptorTypeName
                    || !ActionCompositionAnalyzer.TryParseWorkflowActionDescriptor(
                        creation,
                        semanticModel,
                        cancellationToken,
                        out var parsed,
                        out _))
                {
                    continue;
                }

                // A constructor merely appearing in source is not an ontology-catalog
                // declaration. Only descriptors rooted through DomainOntology.Define ->
                // ObjectTypeFromDescriptor can represent executable graph actions. Keep
                // an unrooted workflow binding visible as an invalid declaration so it
                // fails closed, but never let an unrelated descriptor satisfy a leaf.
                parsedDirectActions.Add(creation);
                if (parsed.HasWorkflowBinding)
                {
                    actions.Add(OntologyActionContract.FromParsed(
                        parsed,
                        "a direct ActionDescriptor workflow binding is not rooted in "
                        + "DomainOntology.Define through ObjectTypeFromDescriptor"));
                }
            }

            foreach (var withExpression in root.DescendantNodes().OfType<WithExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (semanticModel.GetTypeInfo(withExpression, cancellationToken).Type?.ToDisplayString()
                        != ActionDescriptorTypeName
                    || !TryParseDescriptorWithWorkflowBinding(
                        withExpression,
                        semanticModel,
                        cancellationToken,
                        out var workflowName,
                        out var bindingFailure))
                {
                    continue;
                }

                actions.Add(OntologyActionContract.InvalidWorkflowBinding(
                    "<dynamic-domain>",
                    workflowName,
                    bindingFailure
                        ?? "an ActionDescriptor with-expression cannot be associated with one statically closed action contract",
                    withExpression.GetLocation()));
            }
        }

        // A workflow binding is a semantic declaration even when authoring has been factored
        // through a helper. The closed parser above deliberately accepts only Action(...) chains
        // nested directly inside Define -> Object's configure lambda. Inventory the whole
        // compilation after that parse and reject every remaining BoundToWorkflow call. Without
        // this backstop, Define(builder) => Register(builder) and
        // Object(..., obj => ConfigureActions(obj)) silently disappear from the action catalog.
        foreach (var tree in compilation.SyntaxTrees.OrderBy(item => item.FilePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var root = tree.GetRoot(cancellationToken);
            var semanticModel = compilation.GetSemanticModel(tree);
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (inventoriedBindings.Contains(invocation)
                    || !ActionCompositionAnalyzer.TryParseStandaloneWorkflowBinding(
                        invocation,
                        semanticModel,
                        cancellationToken,
                        out var workflowName,
                        out var bindingFailure))
                {
                    continue;
                }

                actions.Add(OntologyActionContract.InvalidWorkflowBinding(
                    TryGetContainingDomainName(
                        invocation,
                        semanticModel,
                        compilation,
                        cancellationToken,
                        out var domainName)
                            ? domainName
                            : "<dynamic-domain>",
                    workflowName,
                    bindingFailure
                        ?? "the workflow binding is not part of one direct Action(...) fluent chain",
                    invocation.GetLocation()));
                inventoriedBindings.Add(invocation);
            }
        }

        return new OntologyActionCatalog(
            actions.OrderBy(item => item.Identity.DomainName, StringComparer.Ordinal)
                .ThenBy(item => item.Identity.ObjectTypeName, StringComparer.Ordinal)
                .ThenBy(item => item.Identity.ActionName, StringComparer.Ordinal)
                .ThenBy(item => item.Location.SourceSpan.Start)
                .ToImmutableArray(),
            lattices.ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.ToImmutableArray(),
                StringComparer.Ordinal));
    }

    internal ImmutableArray<OntologyActionContract> Resolve(ActionIdentity identity) =>
        Actions.Where(action => action.Identity.Equals(identity)).ToImmutableArray();

    internal ImmutableArray<OntologyAuthorityLattice> ResolveLattice(string domainName) =>
        AuthorityLattices.TryGetValue(domainName, out var lattices)
            ? lattices
            : ImmutableArray<OntologyAuthorityLattice>.Empty;

    private static void InventoryDirectDescriptorActions(
        MethodDeclarationSyntax define,
        SemanticModel semanticModel,
        string? domainClosureFailure,
        CancellationToken cancellationToken,
        List<OntologyActionContract> actions,
        HashSet<SyntaxNode> parsedDirectActions)
    {
        var parameterSyntax = define.ParameterList.Parameters.SingleOrDefault();
        var builderParameter = parameterSyntax is null
            ? null
            : semanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) as IParameterSymbol;
        if (builderParameter is null)
        {
            return;
        }

        foreach (var invocation in define.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (semanticModel.GetOperation(invocation, cancellationToken)
                    is not IInvocationOperation operation
                || operation.TargetMethod.ContainingType.ToDisplayString() != OntologyBuilderTypeName
                || operation.TargetMethod.Name != "ObjectTypeFromDescriptor"
                || !ReferencesParameter(operation.Instance, builderParameter)
                || operation.Arguments.FirstOrDefault(argument =>
                        argument.Parameter?.Name == "descriptor")?.Syntax
                    is not ArgumentSyntax descriptorArgument)
            {
                continue;
            }

            if (!TryResolveObjectTypeDescriptorCreation(
                    descriptorArgument.Expression,
                    define,
                    semanticModel,
                    builderParameter,
                    cancellationToken,
                    out var descriptorCreation))
            {
                continue;
            }

            var descriptorIdentityFailure = TryGetObjectTypeDescriptorIdentity(
                descriptorCreation,
                semanticModel,
                cancellationToken,
                out var descriptorDomainName,
                out var descriptorObjectTypeName)
                    ? null
                    : "the containing ObjectTypeDescriptor identity is dynamic or empty";
            var actionsInitializerFailure = HasClosedActionsInitializer(
                descriptorCreation,
                semanticModel,
                cancellationToken)
                    ? null
                    : "the ObjectTypeDescriptor Actions collection is dynamic or conditionally populated";
            foreach (var actionCreation in descriptorCreation.DescendantNodes()
                .OfType<BaseObjectCreationExpressionSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (semanticModel.GetTypeInfo(actionCreation, cancellationToken).Type?.ToDisplayString()
                        != ActionDescriptorTypeName
                    || !IsInActionsInitializer(
                        actionCreation,
                        descriptorCreation,
                        semanticModel,
                        cancellationToken)
                    || !parsedDirectActions.Add(actionCreation)
                    || !ActionCompositionAnalyzer.TryParseWorkflowActionDescriptor(
                        actionCreation,
                        semanticModel,
                        cancellationToken,
                        out var parsed,
                        out _))
                {
                    continue;
                }

                var subjectFailure = descriptorIdentityFailure;
                if (subjectFailure is null
                    && (!string.Equals(
                            parsed.DomainName,
                            descriptorDomainName,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            parsed.ObjectTypeName,
                            descriptorObjectTypeName,
                            StringComparison.Ordinal)))
                {
                    subjectFailure = $"action subject '{parsed.DomainName}/{parsed.ObjectTypeName}' "
                        + $"does not match containing ObjectTypeDescriptor subject "
                        + $"'{descriptorDomainName}/{descriptorObjectTypeName}'";
                }

                actions.Add(OntologyActionContract.FromParsed(
                    parsed,
                    domainClosureFailure ?? actionsInitializerFailure ?? subjectFailure));
            }
        }
    }

    private static bool HasClosedActionsInitializer(
        BaseObjectCreationExpressionSyntax descriptorCreation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var assignment = descriptorCreation.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(candidate =>
                semanticModel.GetSymbolInfo(candidate.Left, cancellationToken).Symbol
                    is IPropertySymbol property
                && property.Name == "Actions"
                && property.ContainingType.ToDisplayString() == ObjectTypeDescriptorTypeName);
        if (assignment is null)
        {
            return true;
        }

        var value = Unwrap(assignment.Right);
        IEnumerable<ExpressionSyntax>? elements = value switch
        {
            CollectionExpressionSyntax collection
                when collection.Elements.All(element => element is ExpressionElementSyntax) =>
                collection.Elements.Cast<ExpressionElementSyntax>().Select(element => element.Expression),
            ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
            ImplicitArrayCreationExpressionSyntax { Initializer: { } initializer } =>
                initializer.Expressions,
            _ => null,
        };
        return elements is not null && elements.All(element =>
        {
            var candidate = Unwrap(element) as BaseObjectCreationExpressionSyntax;
            return candidate is not null
                && semanticModel.GetTypeInfo(candidate, cancellationToken).Type?.ToDisplayString()
                    == ActionDescriptorTypeName;
        });
    }

    private static bool TryGetObjectTypeDescriptorIdentity(
        BaseObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string domainName,
        out string objectTypeName)
    {
        domainName = null!;
        objectTypeName = null!;
        var operation = semanticModel.GetOperation(creation, cancellationToken)
            as IObjectCreationOperation;
        if (operation is null)
        {
            return false;
        }

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter?.Name == "domainName"
                && TryGetConstantString(argument.Value, out var parsedDomainName))
            {
                domainName = parsedDomainName;
            }
            else if (argument.Parameter?.Name == "name"
                && TryGetConstantString(argument.Value, out var parsedObjectTypeName))
            {
                objectTypeName = parsedObjectTypeName;
            }
        }

        if (creation.Initializer is not null)
        {
            foreach (var assignment in creation.Initializer.Expressions
                .OfType<AssignmentExpressionSyntax>())
            {
                if (semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol
                        is not IPropertySymbol property
                    || property.ContainingType.ToDisplayString() != ObjectTypeDescriptorTypeName)
                {
                    continue;
                }

                var constant = semanticModel.GetConstantValue(assignment.Right, cancellationToken);
                if (property.Name == "DomainName")
                {
                    domainName = constant is { HasValue: true, Value: string domainValue }
                        && !string.IsNullOrWhiteSpace(domainValue)
                            ? domainValue
                            : null!;
                }
                else if (property.Name == "Name")
                {
                    objectTypeName = constant is { HasValue: true, Value: string objectValue }
                        && !string.IsNullOrWhiteSpace(objectValue)
                            ? objectValue
                            : null!;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(domainName)
            && !string.IsNullOrWhiteSpace(objectTypeName);
    }

    private static bool ReferencesParameter(
        IOperation? operation,
        IParameterSymbol parameter)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation is IParameterReferenceOperation reference
            && SymbolEqualityComparer.Default.Equals(reference.Parameter, parameter);
    }

    private static bool IsInActionsInitializer(
        BaseObjectCreationExpressionSyntax actionCreation,
        BaseObjectCreationExpressionSyntax descriptorCreation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in actionCreation.Ancestors()
            .TakeWhile(ancestor => ancestor != descriptorCreation)
            .OfType<AssignmentExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol
                    is IPropertySymbol property
                && property.Name == "Actions"
                && property.ContainingType.ToDisplayString() == ObjectTypeDescriptorTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetDefineClosureFailure(
        MethodDeclarationSyntax define,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var parameterSyntax = define.ParameterList.Parameters.SingleOrDefault();
        var parameter = parameterSyntax is null
            ? null
            : semanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) as IParameterSymbol;
        if (parameter is null)
        {
            return "the ontology Define method does not expose one statically resolvable builder parameter";
        }

        if (define.ExpressionBody is null
            && (define.Body is null
                || define.Body.Statements.Any(statement =>
                    statement is not ExpressionStatementSyntax
                    && !IsClosedObjectDescriptorLocal(
                        statement,
                        define,
                        semanticModel,
                        parameter,
                        cancellationToken))))
        {
            return "the ontology Define method contains conditional or non-linear control flow";
        }

        return HasClosedOntologyBuilderUses(define, parameter, semanticModel, cancellationToken)
            ? null
            : "the ontology Define builder escapes the statically closed ontology grammar";
    }

    private static string? GetLambdaBuilderClosureFailure(
        LambdaExpressionSyntax lambda,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        string scope,
        CancellationToken cancellationToken)
    {
        if (lambda.Body is BlockSyntax block
            && block.Statements.Any(statement => statement is not ExpressionStatementSyntax))
        {
            return $"the ontology {scope} contains conditional or non-linear control flow";
        }

        return HasClosedOntologyBuilderUses(lambda, parameter, semanticModel, cancellationToken)
            ? null
            : $"the ontology {scope} builder escapes the statically closed ontology grammar";
    }

    private static bool HasClosedOntologyBuilderUses(
        SyntaxNode scope,
        IParameterSymbol parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol
                    is not IParameterSymbol referenced
                || !SymbolEqualityComparer.Default.Equals(referenced, parameter))
            {
                continue;
            }

            if (identifier.Ancestors()
                    .TakeWhile(ancestor => ancestor != scope)
                    .Any(ancestor => ancestor is AnonymousFunctionExpressionSyntax
                        or LocalFunctionStatementSyntax)
                || !IsClosedOntologyBuilderUse(
                    identifier,
                    scope,
                    semanticModel,
                    cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClosedOntologyBuilderUse(
        IdentifierNameSyntax identifier,
        SyntaxNode scope,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ExpressionSyntax current = identifier;
        while (true)
        {
            while (current.Parent is ParenthesizedExpressionSyntax parenthesized
                && parenthesized.Expression == current)
            {
                current = parenthesized;
            }

            if (current.Parent is MemberAccessExpressionSyntax member
                && member.Expression == current
                && member.Parent is InvocationExpressionSyntax invocation
                && semanticModel.GetOperation(invocation, cancellationToken)
                    is IInvocationOperation operation
                && string.Equals(
                    operation.TargetMethod.ContainingNamespace.ToDisplayString(),
                    "Strategos.Ontology.Builder",
                    StringComparison.Ordinal)
                && string.Equals(
                    operation.TargetMethod.ContainingAssembly.Name,
                    "Strategos.Ontology",
                    StringComparison.Ordinal))
            {
                current = invocation;
                continue;
            }

            return current.Parent is ExpressionStatementSyntax
                || (scope is LambdaExpressionSyntax lambda
                    && ReferenceEquals(current, lambda.Body))
                || (scope is MethodDeclarationSyntax method
                    && ReferenceEquals(current, method.ExpressionBody?.Expression));
        }
    }

    private static bool DerivesFrom(INamedTypeSymbol? type, string baseTypeName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsClosedObjectDescriptorLocal(
        StatementSyntax statement,
        MethodDeclarationSyntax define,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        CancellationToken cancellationToken)
    {
        if (statement is not LocalDeclarationStatementSyntax localDeclaration
            || localDeclaration.UsingKeyword != default
            || localDeclaration.AwaitKeyword != default
            || localDeclaration.Declaration.Variables.Count != 1)
        {
            return false;
        }

        var declarator = localDeclaration.Declaration.Variables[0];
        if (declarator.Initializer is null
            || semanticModel.GetDeclaredSymbol(declarator, cancellationToken)
                is not ILocalSymbol local
            || local.RefKind != RefKind.None
            || Unwrap(declarator.Initializer.Value)
                is not BaseObjectCreationExpressionSyntax descriptorCreation
            || semanticModel.GetTypeInfo(descriptorCreation, cancellationToken).Type?.ToDisplayString()
                != ObjectTypeDescriptorTypeName)
        {
            return false;
        }

        var references = define.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    local))
            .ToImmutableArray();
        if (references.Length != 1)
        {
            return false;
        }

        var argument = references[0].AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
        if (argument?.Parent?.Parent is not InvocationExpressionSyntax invocation
            || semanticModel.GetOperation(invocation, cancellationToken)
                is not IInvocationOperation operation
            || operation.TargetMethod.ContainingType.ToDisplayString() != OntologyBuilderTypeName
            || operation.TargetMethod.Name != "ObjectTypeFromDescriptor"
            || !ReferencesParameter(operation.Instance, builderParameter))
        {
            return false;
        }

        var descriptorArgument = operation.Arguments.FirstOrDefault(candidate =>
            candidate.Parameter?.Name == "descriptor");
        return descriptorArgument is not null
            && ReferencesLocal(descriptorArgument.Value, local);
    }

    private static bool TryResolveObjectTypeDescriptorCreation(
        ExpressionSyntax expression,
        MethodDeclarationSyntax define,
        SemanticModel semanticModel,
        IParameterSymbol builderParameter,
        CancellationToken cancellationToken,
        out BaseObjectCreationExpressionSyntax descriptorCreation)
    {
        var unwrapped = Unwrap(expression);
        if (unwrapped is BaseObjectCreationExpressionSyntax inlineCreation
            && semanticModel.GetTypeInfo(inlineCreation, cancellationToken).Type?.ToDisplayString()
                == ObjectTypeDescriptorTypeName)
        {
            descriptorCreation = inlineCreation;
            return true;
        }

        var operation = semanticModel.GetOperation(unwrapped, cancellationToken);
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        if (operation is not ILocalReferenceOperation reference
            || reference.Local.DeclaringSyntaxReferences.Length != 1
            || reference.Local.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken)
                is not VariableDeclaratorSyntax declarator
            || declarator.Parent?.Parent is not LocalDeclarationStatementSyntax declaration
            || !ReferenceEquals(declaration.Parent, define.Body)
            || !IsClosedObjectDescriptorLocal(
                declaration,
                define,
                semanticModel,
                builderParameter,
                cancellationToken)
            || declarator.Initializer is null
            || Unwrap(declarator.Initializer.Value)
                is not BaseObjectCreationExpressionSyntax localCreation)
        {
            descriptorCreation = null!;
            return false;
        }

        descriptorCreation = localCreation;
        return true;
    }

    private static bool ReferencesLocal(IOperation? operation, ILocalSymbol local)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation is ILocalReferenceOperation reference
            && SymbolEqualityComparer.Default.Equals(reference.Local, local);
    }

    private static bool IsDomainOntologyDefineOverride(IMethodSymbol method)
    {
        if (!method.IsOverride
            || method.Name != "Define"
            || method.Parameters.Length != 1)
        {
            return false;
        }

        for (var overridden = method.OverriddenMethod;
             overridden is not null;
             overridden = overridden.OverriddenMethod)
        {
            if (overridden.Name == "Define"
                && overridden.ReturnsVoid
                && overridden.Parameters.Length == 1
                && overridden.Parameters[0].Type.ToDisplayString() == OntologyBuilderTypeName
                && overridden.ContainingType.ToDisplayString() == DomainOntologyTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDomainName(
        INamedTypeSymbol type,
        Compilation compilation,
        CancellationToken cancellationToken,
        out string domainName)
    {
        foreach (var propertySymbol in type.GetMembers("DomainName")
            .OfType<IPropertySymbol>()
            .OrderBy(item => item.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(item => item.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue))
        {
            var property = propertySymbol.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault();
            if (property is null)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(property.SyntaxTree);
            ExpressionSyntax? expression = property.ExpressionBody?.Expression;
            if (expression is null)
            {
                var returnExpressions = property.AccessorList?.Accessors
                    .Where(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                    .SelectMany(accessor => accessor.DescendantNodes().OfType<ReturnStatementSyntax>())
                    .Select(statement => statement.Expression)
                    .Where(item => item is not null)
                    .Cast<ExpressionSyntax>()
                    .ToImmutableArray() ?? ImmutableArray<ExpressionSyntax>.Empty;
                if (returnExpressions.Length != 1)
                {
                    continue;
                }

                expression = returnExpressions[0];
            }

            if (expression is null)
            {
                continue;
            }

            var value = semanticModel.GetConstantValue(expression, cancellationToken);
            if (value is { HasValue: true, Value: string text }
                && !string.IsNullOrWhiteSpace(text))
            {
                domainName = text;
                return true;
            }
        }

        domainName = null!;
        return false;
    }

    private static bool TryGetContainingDomainName(
        SyntaxNode node,
        SemanticModel semanticModel,
        Compilation compilation,
        CancellationToken cancellationToken,
        out string domainName)
    {
        foreach (var declaration in node.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            var type = semanticModel.GetDeclaredSymbol(declaration, cancellationToken) as INamedTypeSymbol;
            if (type is not null
                && DerivesFrom(type, DomainOntologyTypeName)
                && TryGetDomainName(type, compilation, cancellationToken, out domainName))
            {
                return true;
            }
        }

        domainName = null!;
        return false;
    }

    private static bool TryParseDescriptorWithWorkflowBinding(
        WithExpressionSyntax withExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string? workflowName,
        out string? failureReason)
    {
        workflowName = null;
        failureReason = null;
        var bindingTypeMayBeWorkflow = false;
        var hasBoundWorkflowAssignment = false;
        foreach (var assignment in withExpression.Initializer.Expressions
            .OfType<AssignmentExpressionSyntax>())
        {
            var property = semanticModel.GetSymbolInfo(assignment.Left, cancellationToken).Symbol
                as IPropertySymbol;
            if (property?.ContainingType.ToDisplayString() != ActionDescriptorTypeName)
            {
                continue;
            }

            if (property.Name == "BindingType")
            {
                var operation = semanticModel.GetOperation(assignment.Right, cancellationToken);
                while (operation is IConversionOperation conversion)
                {
                    operation = conversion.Operand;
                }

                if (operation is IFieldReferenceOperation field
                    && field.Field.ContainingType.ToDisplayString() == ActionBindingTypeName)
                {
                    bindingTypeMayBeWorkflow = string.Equals(
                        field.Field.Name,
                        "Workflow",
                        StringComparison.Ordinal);
                }
                else
                {
                    // A dynamic binding kind might evaluate to Workflow. Treat it as a binding so
                    // the closed proof cannot silently assume the descriptor is unbound.
                    bindingTypeMayBeWorkflow = true;
                    failureReason ??= "the ActionDescriptor with-expression binding type is dynamic";
                }

                continue;
            }

            if (property.Name != "BoundWorkflow")
            {
                continue;
            }

            hasBoundWorkflowAssignment = true;
            var workflowOperation = semanticModel.GetOperation(assignment.Right, cancellationToken);
            while (workflowOperation is IConversionOperation conversion)
            {
                workflowOperation = conversion.Operand;
            }

            if (workflowOperation is not IObjectCreationOperation creation
                || creation.Type?.ToDisplayString() != WorkflowBindingReferenceTypeName)
            {
                failureReason ??= "the ActionDescriptor with-expression workflow identity is dynamic";
                continue;
            }

            var workflowArgument = creation.Arguments
                .FirstOrDefault(argument => argument.Parameter?.Name == "workflowId");
            if (workflowArgument is null
                || !TryGetConstantString(workflowArgument.Value, out var parsedWorkflowName))
            {
                failureReason ??= "the ActionDescriptor with-expression workflow identity is dynamic or empty";
                continue;
            }

            workflowName = parsedWorkflowName;
        }

        var hasWorkflowBinding = bindingTypeMayBeWorkflow || hasBoundWorkflowAssignment;
        if (hasWorkflowBinding && workflowName is null)
        {
            failureReason ??= "the ActionDescriptor with-expression does not name a statically closed workflow";
        }

        return hasWorkflowBinding;
    }

    private static void InventoryFluentBindings(
        InvocationExpressionSyntax actionInvocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ISet<InvocationExpressionSyntax> inventoriedBindings)
    {
        foreach (var chained in ActionCompositionAnalyzer.EnumerateWorkflowFluentChain(
            actionInvocation).Skip(1))
        {
            if (ActionCompositionAnalyzer.TryParseStandaloneWorkflowBinding(
                chained,
                semanticModel,
                cancellationToken,
                out _,
                out _))
            {
                inventoriedBindings.Add(chained);
            }
        }
    }

    private static bool TryGetObjectDeclaration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IParameterSymbol? defineBuilderParameter,
        CancellationToken cancellationToken,
        out string objectTypeName,
        out LambdaExpressionSyntax configureLambda,
        out IParameterSymbol objectParameter,
        out string? identityFailure)
    {
        identityFailure = null;
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        var operation = semanticModel.GetOperation(invocation, cancellationToken) as IInvocationOperation;
        if (method?.Name != "Object"
            || method.TypeArguments.Length != 1
            || method.ContainingType.ToDisplayString() != OntologyBuilderTypeName
            || defineBuilderParameter is null
            || operation is null
            || !ReferencesParameter(operation.Instance, defineBuilderParameter))
        {
            objectTypeName = null!;
            configureLambda = null!;
            objectParameter = null!;
            return false;
        }

        objectTypeName = method.TypeArguments[0] is INamedTypeSymbol namedObjectType
            ? namedObjectType.MetadataName
            : method.TypeArguments[0].Name;
        var nameExpression = operation?.Arguments
            .FirstOrDefault(argument => argument.Parameter?.Name == "name")
            ?.Syntax is ArgumentSyntax nameArgument
                ? nameArgument.Expression
                : null;
        if (nameExpression is not null)
        {
            var explicitName = semanticModel.GetConstantValue(nameExpression, cancellationToken);
            if (!explicitName.HasValue
                || explicitName.Value is not string descriptorName
                || string.IsNullOrWhiteSpace(descriptorName))
            {
                objectTypeName = "<dynamic-object>";
                identityFailure = "the ontology object name is not a compile-time non-empty string";
            }
            else
            {
                objectTypeName = descriptorName;
            }
        }

        var lambdaExpression = operation?.Arguments
            .FirstOrDefault(argument => argument.Parameter?.Name == "configure")
            ?.Syntax is ArgumentSyntax configureArgument
                ? configureArgument.Expression
                : null;
        configureLambda = (Unwrap(lambdaExpression) as LambdaExpressionSyntax)!;
        var parameterSyntax = configureLambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter,
            ParenthesizedLambdaExpressionSyntax parenthesized
                when parenthesized.ParameterList.Parameters.Count == 1 =>
                parenthesized.ParameterList.Parameters[0],
            _ => null,
        };
        objectParameter = parameterSyntax is null
            ? null!
            : (semanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) as IParameterSymbol)!;
        return configureLambda is not null && objectParameter is not null;
    }

    private static bool IsActionRoot(
        InvocationExpressionSyntax invocation,
        IParameterSymbol objectParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method?.Name != "Action"
            || method.ContainingType.OriginalDefinition.ToDisplayString() != ObjectBuilderTypeName
            || invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSymbolInfo(member.Expression, cancellationToken).Symbol,
            objectParameter);
    }

    private static OntologyAuthorityLattice ParseAuthorityLattice(
        string domainName,
        ImmutableArray<InvocationExpressionSyntax> invocations,
        SemanticModel semanticModel,
        IParameterSymbol? defineBuilderParameter,
        string? closureFailure,
        CancellationToken cancellationToken)
    {
        var axes = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
        var authorities = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        string? invalidReason = closureFailure;

        foreach (var invocation in invocations)
        {
            var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
            if (method?.ContainingType.ToDisplayString() == OntologyBuilderTypeName
                && method.Name == "AuthorityAxis")
            {
                var operation = semanticModel.GetOperation(invocation, cancellationToken)
                    as IInvocationOperation;
                if (defineBuilderParameter is null
                    || operation is null
                    || !ReferencesParameter(operation.Instance, defineBuilderParameter))
                {
                    continue;
                }

                if (!TryGetConstantStringArgument(operation, "name", out var axisName)
                    || !TryGetConstantStringArguments(operation, "levels", out var levels)
                    || levels.IsEmpty)
                {
                    invalidReason ??= "an authority axis is dynamic or empty";
                    continue;
                }

                if (levels.Distinct(StringComparer.Ordinal).Count() != levels.Length)
                {
                    invalidReason ??= $"authority axis '{axisName}' contains duplicate levels";
                }

                if (axes.ContainsKey(axisName))
                {
                    invalidReason ??= $"authority axis '{axisName}' is duplicated";
                }
                else
                {
                    axes.Add(axisName, levels);
                }

                continue;
            }

            var authorityOperation = semanticModel.GetOperation(invocation, cancellationToken)
                as IInvocationOperation;
            if (method?.ContainingType.ToDisplayString() != OntologyBuilderTypeName
                || method.Name != "Authority"
                || defineBuilderParameter is null
                || authorityOperation is null
                || !ReferencesParameter(authorityOperation.Instance, defineBuilderParameter))
            {
                continue;
            }

            if (!TryGetConstantStringArgument(
                authorityOperation,
                "name",
                out var authorityName))
            {
                invalidReason ??= "an authority name is dynamic or empty";
                continue;
            }

            var coordinates = new Dictionary<string, string>(StringComparer.Ordinal);
            ExpressionSyntax current = invocation;
            while (current.Parent is MemberAccessExpressionSyntax member
                && member.Expression == current
                && member.Parent is InvocationExpressionSyntax chained)
            {
                var chainedMethod = semanticModel.GetSymbolInfo(chained, cancellationToken).Symbol as IMethodSymbol;
                var chainedOperation = semanticModel.GetOperation(chained, cancellationToken)
                    as IInvocationOperation;
                if (chainedMethod?.Name == "At")
                {
                    if (chainedOperation is null
                        || !TryGetConstantStringArgument(
                            chainedOperation,
                            "axisName",
                            out var coordinateAxis)
                        || !TryGetConstantStringArgument(
                            chainedOperation,
                            "levelName",
                            out var coordinateLevel))
                    {
                        invalidReason ??= $"authority '{authorityName}' has a dynamic axis or level";
                        current = chained;
                        continue;
                    }

                    if (coordinates.ContainsKey(coordinateAxis))
                    {
                        invalidReason ??= $"authority '{authorityName}' repeats axis '{coordinateAxis}'";
                    }
                    else
                    {
                        coordinates.Add(coordinateAxis, coordinateLevel);
                    }
                }

                current = chained;
            }

            if (authorities.ContainsKey(authorityName))
            {
                invalidReason ??= $"authority '{authorityName}' is duplicated";
            }
            else
            {
                authorities.Add(authorityName, coordinates);
            }
        }

        foreach (var authority in authorities)
        {
            if (!new HashSet<string>(authority.Value.Keys, StringComparer.Ordinal).SetEquals(axes.Keys))
            {
                invalidReason ??= $"authority '{authority.Key}' does not name every axis";
                continue;
            }

            foreach (var coordinate in authority.Value)
            {
                if (!axes.TryGetValue(coordinate.Key, out var levels)
                    || !levels.Contains(coordinate.Value, StringComparer.Ordinal))
                {
                    invalidReason ??= $"authority '{authority.Key}' names unknown level '{coordinate.Value}'";
                }
            }
        }

        return new OntologyAuthorityLattice(domainName, axes, authorities, invalidReason);
    }

    private static bool TryGetConstantStringArgument(
        IInvocationOperation operation,
        string parameterName,
        out string value)
    {
        var argument = operation.Arguments.FirstOrDefault(item => item.Parameter?.Name == parameterName);
        if (argument is null
            || !TryGetConstantString(argument.Value, out value))
        {
            value = null!;
            return false;
        }

        return true;
    }

    private static bool TryGetConstantStringArguments(
        IInvocationOperation operation,
        string parameterName,
        out ImmutableArray<string> values)
    {
        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var argument in operation.Arguments.Where(item => item.Parameter?.Name == parameterName))
        {
            if (!TryAppendConstantStrings(argument.Value, builder))
            {
                values = default;
                return false;
            }
        }

        values = builder.ToImmutable();
        return true;
    }

    private static bool TryAppendConstantStrings(
        IOperation operation,
        ImmutableArray<string>.Builder builder)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        if (operation is IArrayCreationOperation { Initializer: { } initializer })
        {
            foreach (var element in initializer.ElementValues)
            {
                if (!TryAppendConstantStrings(element, builder))
                {
                    return false;
                }
            }

            return true;
        }

        if (!TryGetConstantString(operation, out var value))
        {
            return false;
        }

        builder.Add(value);
        return true;
    }

    private static bool TryGetConstantString(IOperation operation, out string value)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        if (operation.ConstantValue is { HasValue: true, Value: string text }
            && !string.IsNullOrWhiteSpace(text))
        {
            value = text;
            return true;
        }

        value = null!;
        return false;
    }

    private static ExpressionSyntax? Unwrap(ExpressionSyntax? expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

internal sealed class OntologyActionContract
{
    private OntologyActionContract(
        ActionIdentity identity,
        bool hasWorkflowBinding,
        string? boundWorkflowName,
        OntologyPredicateContract requirement,
        OntologyPredicateContract guarantee,
        ImmutableArray<string> frame,
        string? requiredAuthority,
        string? compensatingActionName,
        string? invalidReason,
        Location location)
    {
        Identity = identity;
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

    internal ActionIdentity Identity { get; }

    internal bool HasWorkflowBinding { get; }

    internal string? BoundWorkflowName { get; }

    internal OntologyPredicateContract Requirement { get; }

    internal OntologyPredicateContract Guarantee { get; }

    internal ImmutableArray<string> Frame { get; }

    internal string? RequiredAuthority { get; }

    internal string? CompensatingActionName { get; }

    internal string? InvalidReason { get; }

    internal Location Location { get; }

    internal static OntologyActionContract FromParsed(
        ActionCompositionAnalyzer.WorkflowActionContractSyntax parsed,
        string? additionalInvalidReason = null) => new(
            new ActionIdentity(parsed.DomainName, parsed.ObjectTypeName, parsed.ActionName),
            parsed.HasWorkflowBinding,
            parsed.BoundWorkflowName,
            OntologyPredicateContract.FromParsed(parsed.Requirement),
            OntologyPredicateContract.FromParsed(parsed.Guarantee),
            parsed.Frame,
            parsed.RequiredAuthority,
            parsed.CompensatingActionName,
            parsed.InvalidReason ?? additionalInvalidReason,
            parsed.Location);

    internal static OntologyActionContract InvalidWorkflowBinding(
        string domainName,
        string? workflowName,
        string invalidReason,
        Location location) => new(
            new ActionIdentity(domainName, "<unresolved-object>", "<unresolved-action>"),
            hasWorkflowBinding: true,
            workflowName,
            OntologyPredicateContract.True,
            OntologyPredicateContract.True,
            ImmutableArray<string>.Empty,
            requiredAuthority: null,
            compensatingActionName: null,
            invalidReason,
            location);
}

internal sealed class OntologyPredicateContract
{
    private OntologyPredicateContract(
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

    internal LogicFormula Formula { get; }

    internal ImmutableDictionary<string, ImmutableArray<string>> AtomReads { get; }

    internal ImmutableArray<string> OpaqueKeys { get; }

    internal string? InvalidReason { get; }

    internal static OntologyPredicateContract True { get; } = new(
        LogicFormula.True,
        ImmutableDictionary<string, ImmutableArray<string>>.Empty,
        ImmutableArray<string>.Empty,
        invalidReason: null);

    internal static OntologyPredicateContract FromParsed(
        ActionCompositionAnalyzer.WorkflowPredicateSyntax parsed) => new(
            parsed.Formula,
            parsed.AtomReads,
            parsed.OpaqueKeys,
            parsed.InvalidReason);
}

internal sealed class ActionIdentity : IEquatable<ActionIdentity>
{
    internal ActionIdentity(string domainName, string objectTypeName, string actionName)
    {
        DomainName = domainName;
        ObjectTypeName = objectTypeName;
        ActionName = actionName;
    }

    internal string DomainName { get; }

    internal string ObjectTypeName { get; }

    internal string ActionName { get; }

    public bool Equals(ActionIdentity? other) =>
        other is not null
        && string.Equals(DomainName, other.DomainName, StringComparison.Ordinal)
        && string.Equals(ObjectTypeName, other.ObjectTypeName, StringComparison.Ordinal)
        && string.Equals(ActionName, other.ActionName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ActionIdentity);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = StringComparer.Ordinal.GetHashCode(DomainName);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ObjectTypeName);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ActionName);
            return hash;
        }
    }

    public override string ToString() => $"{DomainName}/{ObjectTypeName}/{ActionName}";
}

internal sealed class OntologyAuthorityLattice
{
    private readonly Dictionary<string, ImmutableArray<string>> axes;
    private readonly Dictionary<string, Dictionary<string, string>> authorities;

    internal OntologyAuthorityLattice(
        string domainName,
        Dictionary<string, ImmutableArray<string>> axes,
        Dictionary<string, Dictionary<string, string>> authorities,
        string? invalidReason)
    {
        DomainName = domainName;
        this.axes = axes;
        this.authorities = authorities;
        InvalidReason = invalidReason;
    }

    internal string DomainName { get; }

    internal string? InvalidReason { get; }

    internal bool TryJoinAtMost(
        IEnumerable<string?> candidateAuthorities,
        string? boundAuthority,
        out bool isAtMost,
        out string? failureReason)
    {
        var candidates = candidateAuthorities.Where(item => item is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToImmutableArray();
        if (InvalidReason is not null)
        {
            isAtMost = false;
            failureReason = InvalidReason;
            return false;
        }

        Dictionary<string, string>? bound = null;
        if (boundAuthority is not null && !authorities.TryGetValue(boundAuthority, out bound))
        {
            isAtMost = false;
            failureReason = $"bound action requires unknown authority '{boundAuthority}'";
            return false;
        }

        if (candidates.IsEmpty)
        {
            isAtMost = true;
            failureReason = null;
            return true;
        }

        if (boundAuthority is null)
        {
            isAtMost = false;
            failureReason = null;
            return true;
        }

        var candidatesResolved = new List<Dictionary<string, string>>();
        foreach (var candidate in candidates)
        {
            if (!authorities.TryGetValue(candidate, out var resolved))
            {
                isAtMost = false;
                failureReason = $"workflow step requires unknown authority '{candidate}'";
                return false;
            }

            candidatesResolved.Add(resolved);
        }

        foreach (var axis in axes.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var candidateRank = candidatesResolved.Max(item => axis.Value.IndexOf(item[axis.Key]));
            var boundRank = axis.Value.IndexOf(bound![axis.Key]);
            if (candidateRank > boundRank)
            {
                isAtMost = false;
                failureReason = null;
                return true;
            }
        }

        isAtMost = true;
        failureReason = null;
        return true;
    }
}
