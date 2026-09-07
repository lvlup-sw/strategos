// -----------------------------------------------------------------------
// <copyright file="FluentDslParseContext.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Provides a pre-computed context for parsing fluent DSL workflow definitions.
/// Caches common lookups to avoid repeated traversal of the syntax tree.
/// </summary>
internal sealed class FluentDslParseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentDslParseContext"/> class.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for condition ID generation (optional).</param>
    /// <param name="allInvocations">All invocation expressions in the type.</param>
    /// <param name="finallyInvocation">The terminal Finally call, if present.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private FluentDslParseContext(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string? workflowName,
        IReadOnlyList<InvocationExpressionSyntax> allInvocations,
        InvocationExpressionSyntax? finallyInvocation,
        string? definitionClosureFailure,
        CancellationToken cancellationToken)
    {
        TypeDeclaration = typeDeclaration;
        SemanticModel = semanticModel;
        WorkflowName = workflowName;
        AllInvocations = allInvocations;
        FinallyInvocation = finallyInvocation;
        DefinitionClosureFailure = definitionClosureFailure;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the type declaration containing the workflow definition.
    /// </summary>
    public SyntaxNode TypeDeclaration { get; }

    /// <summary>
    /// Gets the semantic model for type resolution.
    /// </summary>
    public SemanticModel SemanticModel { get; }

    /// <summary>
    /// Gets the workflow name for condition ID generation.
    /// </summary>
    public string? WorkflowName { get; }

    /// <summary>
    /// Gets all invocation expressions in the type declaration.
    /// </summary>
    public IReadOnlyList<InvocationExpressionSyntax> AllInvocations { get; }

    /// <summary>
    /// Gets the terminal Finally call, or null if not found.
    /// </summary>
    public InvocationExpressionSyntax? FinallyInvocation { get; }

    /// <summary>
    /// Gets the stable reason why the authored <c>Definition</c> member could not be reduced to
    /// one direct fluent expression, or <see langword="null"/> when it is statically closed.
    /// </summary>
    public string? DefinitionClosureFailure { get; }

    /// <summary>
    /// Gets the cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Creates a new parse context with pre-computed lookups.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the workflow definition.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="workflowName">The workflow name for condition ID generation (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new <see cref="FluentDslParseContext"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="typeDeclaration"/> or <paramref name="semanticModel"/> is null.
    /// </exception>
    public static FluentDslParseContext Create(
        SyntaxNode typeDeclaration,
        SemanticModel semanticModel,
        string? workflowName,
        CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(typeDeclaration, nameof(typeDeclaration));
        ThrowHelper.ThrowIfNull(semanticModel, nameof(semanticModel));

        // Keep the whole-type walk only as an error-tolerant fallback. Once the authored
        // Definition value has been identified, every extractor must be scoped to that value:
        // another member of the attributed type may contain perfectly real Strategos calls, but
        // those calls are not part of the workflow being generated or proved.
        var typeInvocations = typeDeclaration
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        // Preserve the generator's established best-effort syntax extraction for ordinary
        // workflows, including error-tolerant IDE compilations.  The stricter Definition walk
        // is proof metadata: a bound workflow fails closed through DefinitionClosureFailure,
        // but an unbound workflow must not lose all of its generated steps merely because its
        // semantic model is incomplete (or because a legacy parse fixture is intentionally
        // syntactic-only).
        var (definitionExpression, closedFinally, definitionClosureFailure) = typeDeclaration is TypeDeclarationSyntax type
            ? FindDefinitionFinally(type, semanticModel, cancellationToken)
            : (null, null, null);
        var allInvocations = definitionExpression is null
            ? typeInvocations
            : definitionExpression
                .DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .ToList();
        var syntacticFinally = allInvocations
            .FirstOrDefault(inv => SyntaxHelper.IsMethodCall(inv, "Finally"));
        var finallyInvocation = closedFinally ?? syntacticFinally;

        return new FluentDslParseContext(
            typeDeclaration,
            semanticModel,
            workflowName,
            allInvocations,
            finallyInvocation,
            definitionClosureFailure,
            cancellationToken);
    }

    private static (
        ExpressionSyntax? DefinitionExpression,
        InvocationExpressionSyntax? Finally,
        string? Failure) FindDefinitionFinally(
        TypeDeclarationSyntax typeDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var expressions = ImmutableArray.CreateBuilder<ExpressionSyntax>();
        var hasNonlinearGetter = false;
        foreach (var member in typeDeclaration.Members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (member)
            {
                case PropertyDeclarationSyntax property
                    when property.Identifier.ValueText == "Definition":
                    if (property.ExpressionBody?.Expression is { } propertyExpression)
                    {
                        expressions.Add(propertyExpression);
                    }

                    if (property.AccessorList is not null)
                    {
                        foreach (var getter in property.AccessorList.Accessors
                            .Where(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)))
                        {
                            if (getter.ExpressionBody?.Expression is { } getterExpression)
                            {
                                expressions.Add(getterExpression);
                            }

                            if (getter.Body is not null)
                            {
                                // A block getter is closed only when its body is literally one
                                // return statement. Looking only at top-level returns allowed a
                                // nested conditional return to choose a different (usually
                                // smaller) runtime topology while proof certified the final one.
                                if (getter.Body.Statements.Count == 1
                                    && getter.Body.Statements[0] is ReturnStatementSyntax
                                    {
                                        Expression: { } returnedExpression,
                                    })
                                {
                                    expressions.Add(returnedExpression);
                                }
                                else
                                {
                                    hasNonlinearGetter = true;
                                }
                            }
                        }
                    }

                    break;

                case FieldDeclarationSyntax field:
                    expressions.AddRange(field.Declaration.Variables
                        .Where(variable => variable.Identifier.ValueText == "Definition")
                        .Select(variable => variable.Initializer?.Value)
                        .Where(expression => expression is not null)
                        .Cast<ExpressionSyntax>());
                    break;
            }
        }

        if (hasNonlinearGetter)
        {
            return (
                null,
                null,
                "workflow Definition block getter must contain exactly one direct return statement");
        }

        if (expressions.Count != 1)
        {
            return (
                null,
                null,
                "workflow Definition must have exactly one statically visible value expression");
        }

        var expression = StripTransparent(expressions[0]);
        if (expression is not InvocationExpressionSyntax invocation
            || !IsWorkflowFinally(invocation, semanticModel, cancellationToken))
        {
            return (
                expression,
                null,
                "workflow Definition must end in one direct Strategos Finally<TStep> call");
        }

        return (expression, invocation, null);
    }

    private static bool IsWorkflowFinally(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        return method is not null
            && string.Equals(method.Name, "Finally", StringComparison.Ordinal)
            && string.Equals(
                method.ContainingType.OriginalDefinition.Name,
                "IWorkflowBuilder",
                StringComparison.Ordinal)
            && string.Equals(
                method.ContainingNamespace.ToDisplayString(),
                "Strategos.Builders",
                StringComparison.Ordinal)
            && string.Equals(method.ContainingAssembly.Name, "Strategos", StringComparison.Ordinal);
    }

    private static ExpressionSyntax StripTransparent(ExpressionSyntax expression) =>
        SyntaxHelper.StripTransparent(expression);
}
