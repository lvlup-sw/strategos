// -----------------------------------------------------------------------
// <copyright file="ContextModelExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts context models from workflow DSL definitions.
/// </summary>
/// <remarks>
/// This extractor parses <c>WithContext()</c> calls from the fluent DSL
/// and produces <see cref="ContextModel"/> instances for saga code generation.
/// </remarks>
internal static class ContextModelExtractor
{
    /// <summary>
    /// Extracts context models from the workflow DSL for context assembler generation.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>
    /// A dictionary mapping step names to their context models.
    /// Only steps with context configuration are included.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static IReadOnlyList<(string StepName, ContextModel Context)> Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // Find all WithContext() method calls
        var withContextInvocations = context.AllInvocations
            .Where(inv => SyntaxHelper.IsMethodCall(inv, "WithContext"))
            .ToList();

        if (withContextInvocations.Count == 0)
        {
            return [];
        }

        var results = new List<(string StepName, ContextModel Context)>();

        foreach (var invocation in withContextInvocations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (TryParseWithContext(invocation, context.SemanticModel, out var stepName, out var contextModel, context.CancellationToken))
            {
                results.Add((stepName, contextModel));
            }
        }

        return results;
    }

    private static bool TryParseWithContext(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string stepName,
        out ContextModel contextModel,
        CancellationToken cancellationToken)
    {
        stepName = string.Empty;
        contextModel = default!;

        // Find the preceding step by walking the invocation chain backwards
        if (!TryFindPrecedingStepName(invocation, semanticModel, out stepName, cancellationToken))
        {
            return false;
        }

        // Get the configuration lambda from WithContext(c => c.Literal(...))
        var configLambda = GetConfigurationLambda(invocation);
        if (configLambda is null)
        {
            return false;
        }

        // Parse context sources from the lambda
        var sources = ParseContextSources(configLambda, semanticModel, cancellationToken);
        if (sources.Count == 0)
        {
            return false;
        }

        contextModel = new ContextModel(sources);
        return true;
    }

    private static LambdaExpressionSyntax? GetConfigurationLambda(InvocationExpressionSyntax invocation)
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

    private static List<ContextSourceModel> ParseContextSources(
        LambdaExpressionSyntax configLambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var sources = new List<ContextSourceModel>();

        // Find all method invocations within the lambda
        var allInvocations = configLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        foreach (var inv in allInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(inv, "FromLiteral"))
            {
                if (TryParseLiteralSource(inv, out var literalSource))
                {
                    sources.Add(literalSource);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "FromState"))
            {
                if (TryParseStateSource(inv, semanticModel, out var stateSource))
                {
                    sources.Add(stateSource);
                }
            }
            else if (SyntaxHelper.IsMethodCall(inv, "FromRetrieval"))
            {
                if (TryParseRetrievalSource(inv, semanticModel, out var retrievalSource, cancellationToken))
                {
                    sources.Add(retrievalSource);
                }
            }
        }

        return sources;
    }

    private static bool TryParseLiteralSource(
        InvocationExpressionSyntax invocation,
        out LiteralContextSourceModel literalSource)
    {
        literalSource = default!;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return false;
        }

        // Extract the string literal value
        if (arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
        {
            literalSource = new LiteralContextSourceModel(literal.Token.ValueText);
            return true;
        }

        return false;
    }

    private static bool TryParseStateSource(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out StateContextSourceModel stateSource)
    {
        stateSource = default!;

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return false;
        }

        // Extract the property selector lambda: s => s.PropertyName
        LambdaExpressionSyntax? selectorLambda = arguments[0].Expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parens => parens,
            _ => null
        };

        if (selectorLambda is null)
        {
            return false;
        }

        // Extract property path from the lambda body
        var body = selectorLambda.Body;
        var propertyPath = ExtractPropertyPath(body);
        if (string.IsNullOrEmpty(propertyPath))
        {
            return false;
        }

        // Determine the property type from the expression
        var typeInfo = semanticModel.GetTypeInfo(body);
        var propertyType = typeInfo.Type?.ToDisplayString() ?? "object";

        // Build the access expression (e.g., "state.CustomerName")
        var accessExpression = BuildAccessExpression(selectorLambda, body);

        stateSource = new StateContextSourceModel(propertyPath, propertyType, accessExpression);
        return true;
    }

    private static bool TryParseRetrievalSource(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out RetrievalContextSourceModel retrievalSource,
        CancellationToken cancellationToken)
    {
        retrievalSource = default!;

        // Extract the collection type from FromRetrieval<TCollection>
        if (!TryGetGenericTypeArgument(invocation, semanticModel, out var collectionTypeName))
        {
            return false;
        }

        // Get the configuration lambda from FromRetrieval<T>(r => r.Query(...).TopK(...))
        var configLambda = GetRetrievalConfigLambda(invocation);
        if (configLambda is null)
        {
            return false;
        }

        // Parse retrieval configuration
        string? queryExpression = null;
        string? literalQuery = null;
        var topK = 5; // Default
        var minRelevance = 0.7m; // Default
        var filters = new List<RetrievalFilterModel>();

        // Find all invocations in the config lambda
        var configInvocations = configLambda
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToList();

        foreach (var configInv in configInvocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(configInv, "Query"))
            {
                ParseQueryConfig(configInv, semanticModel, out literalQuery, out queryExpression);
            }
            else if (SyntaxHelper.IsMethodCall(configInv, "TopK"))
            {
                topK = ParseIntArgument(configInv, topK);
            }
            else if (SyntaxHelper.IsMethodCall(configInv, "MinRelevance"))
            {
                minRelevance = ParseDecimalArgument(configInv, minRelevance);
            }
            else if (SyntaxHelper.IsMethodCall(configInv, "Filter"))
            {
                if (TryParseFilter(configInv, out var filter))
                {
                    filters.Add(filter);
                }
            }
        }

        retrievalSource = new RetrievalContextSourceModel(
            collectionTypeName,
            queryExpression,
            literalQuery,
            topK,
            minRelevance,
            filters);
        return true;
    }

    private static LambdaExpressionSyntax? GetRetrievalConfigLambda(InvocationExpressionSyntax invocation)
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

    private static void ParseQueryConfig(
        InvocationExpressionSyntax queryInvocation,
        SemanticModel semanticModel,
        out string? literalQuery,
        out string? queryExpression)
    {
        literalQuery = null;
        queryExpression = null;

        var arguments = queryInvocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return;
        }

        var arg = arguments[0].Expression;

        // Check if it's a string literal
        if (arg is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
        {
            literalQuery = literal.Token.ValueText;
        }
        else if (arg is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
        {
            // It's a dynamic query expression
            queryExpression = arg.ToString();
        }
    }

    private static int ParseIntArgument(InvocationExpressionSyntax invocation, int defaultValue)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return defaultValue;
        }

        if (arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression))
        {
            if (literal.Token.Value is int intValue)
            {
                return intValue;
            }
        }

        return defaultValue;
    }

    private static decimal ParseDecimalArgument(InvocationExpressionSyntax invocation, decimal defaultValue)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
        {
            return defaultValue;
        }

        if (arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression))
        {
            if (literal.Token.Value is decimal decValue)
            {
                return decValue;
            }

            if (literal.Token.Value is double doubleValue)
            {
                return (decimal)doubleValue;
            }

            if (literal.Token.Value is float floatValue)
            {
                return (decimal)floatValue;
            }
        }

        return defaultValue;
    }

    private static bool TryParseFilter(
        InvocationExpressionSyntax filterInvocation,
        out RetrievalFilterModel filter)
    {
        filter = default!;

        var arguments = filterInvocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return false;
        }

        // First argument is the key
        if (arguments[0].Expression is not LiteralExpressionSyntax keyLiteral
            || !keyLiteral.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
        {
            return false;
        }

        var key = keyLiteral.Token.ValueText;

        // Second argument is the value (static string or lambda)
        var valueArg = arguments[1].Expression;

        if (valueArg is LiteralExpressionSyntax valueLiteral
            && valueLiteral.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
        {
            filter = new RetrievalFilterModel(key, valueLiteral.Token.ValueText, null);
            return true;
        }
        else if (valueArg is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
        {
            filter = new RetrievalFilterModel(key, null, valueArg.ToString());
            return true;
        }

        return false;
    }

    private static bool TryGetGenericTypeArgument(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        out string typeName)
    {
        typeName = string.Empty;

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

        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            typeName = namedType.Name;
            return true;
        }

        typeName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
        return !string.IsNullOrEmpty(typeName);
    }

    private static bool TryFindPrecedingStepName(
        InvocationExpressionSyntax withContextInvocation,
        SemanticModel semanticModel,
        out string stepName,
        CancellationToken cancellationToken)
    {
        stepName = string.Empty;

        // Form 1 — chained on the workflow builder:
        //   .StartWith<Step>().WithContext(c => ...)
        // WithContext is invoked on the RESULT of the preceding StartWith/Then,
        // so we walk back through the member-access chain to find it.
        if (withContextInvocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var previousExpression = memberAccess.Expression;

            while (previousExpression is InvocationExpressionSyntax previousInvocation)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (SyntaxHelper.IsMethodCall(previousInvocation, "StartWith") ||
                    SyntaxHelper.IsMethodCall(previousInvocation, "Then"))
                {
                    if (TryGetStepTypeName(previousInvocation, semanticModel, out stepName))
                    {
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
        }

        // Form 2 — inside a step-configuration lambda:
        //   .StartWith<Step>(step => step.WithContext(ctx => ...))
        // Here WithContext is invoked on the lambda parameter, not on a chain,
        // so the backward walk above finds nothing. Instead walk UP the syntax
        // ancestors to the nearest enclosing StartWith/Then invocation whose
        // generic type argument names the configured step.
        return TryFindEnclosingStepName(withContextInvocation, semanticModel, out stepName, cancellationToken);
    }

    /// <summary>
    /// Resolves the step name for a <c>WithContext</c> call written inside a
    /// step-configuration lambda (<c>StartWith&lt;Step&gt;(s =&gt; s.WithContext(...))</c>)
    /// by walking up to the enclosing <c>StartWith</c>/<c>Then</c> invocation and
    /// reading its generic type argument.
    /// </summary>
    private static bool TryFindEnclosingStepName(
        InvocationExpressionSyntax withContextInvocation,
        SemanticModel semanticModel,
        out string stepName,
        CancellationToken cancellationToken)
    {
        stepName = string.Empty;

        foreach (var ancestor in withContextInvocation.Ancestors().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SyntaxHelper.IsMethodCall(ancestor, "StartWith") ||
                SyntaxHelper.IsMethodCall(ancestor, "Then"))
            {
                if (TryGetStepTypeName(ancestor, semanticModel, out stepName))
                {
                    return true;
                }
            }
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

        if (memberAccess.Name is not GenericNameSyntax genericName)
        {
            return false;
        }

        var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArgument is null)
        {
            return false;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is INamedTypeSymbol namedType)
        {
            stepName = namedType.Name;
            return true;
        }

        stepName = SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
        return !string.IsNullOrEmpty(stepName);
    }

    private static string ExtractPropertyPath(SyntaxNode body)
    {
        // Handle s.Property, s.A.B.C etc.
        if (body is MemberAccessExpressionSyntax memberAccess)
        {
            var parts = new List<string>();
            var current = memberAccess;

            while (current is not null)
            {
                parts.Insert(0, current.Name.Identifier.Text);

                if (current.Expression is MemberAccessExpressionSyntax inner)
                {
                    current = inner;
                }
                else
                {
                    // We've reached the parameter (e.g., 's')
                    break;
                }
            }

            return string.Join(".", parts);
        }

        return string.Empty;
    }

    private static string BuildAccessExpression(LambdaExpressionSyntax lambda, SyntaxNode body)
    {
        // Get the parameter name from the lambda
        var parameterName = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax parens => parens.ParameterList.Parameters.FirstOrDefault()?.Identifier.Text ?? "state",
            _ => "state"
        };

        // Build the full expression
        return body.ToString();
    }
}
