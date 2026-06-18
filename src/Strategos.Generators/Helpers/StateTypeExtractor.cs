// -----------------------------------------------------------------------
// <copyright file="StateTypeExtractor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Helpers;

/// <summary>
/// Extracts the state type name from a workflow definition.
/// </summary>
internal static class StateTypeExtractor
{
    /// <summary>
    /// Extracts the state type name from the workflow definition (e.g., "OrderState" from Workflow&lt;OrderState&gt;).
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>The state type name, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static string? Extract(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        // Look for .Create("...") method call
        var createInvocation = context.AllInvocations
            .FirstOrDefault(inv => SyntaxHelper.IsMethodCall(inv, "Create"));

        if (createInvocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        // The receiver should be Workflow<TState> - look for the generic type argument
        if (memberAccess.Expression is GenericNameSyntax genericName)
        {
            // Get the first type argument (the state type)
            var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
            if (typeArgument is null)
            {
                return null;
            }

            // Resolve the symbol to get the type name
            var symbolInfo = context.SemanticModel.GetSymbolInfo(typeArgument);
            if (symbolInfo.Symbol is INamedTypeSymbol namedType)
            {
                return namedType.Name;
            }

            // Fallback to syntax-based name
            return SyntaxHelper.GetTypeNameFromSyntax(typeArgument);
        }

        return null;
    }

    /// <summary>
    /// Determines whether the workflow's state type exposes a public instance
    /// <c>Phase</c> property. Used by the failure-handler routing lowering: the
    /// saga only syncs <c>Phase = State.Phase</c> when the state type actually
    /// carries a phase, so a realistic state type (one that tracks its phase at
    /// the saga level only) never produces an uncompilable <c>State.Phase</c>
    /// reference.
    /// </summary>
    /// <param name="context">The parse context containing pre-computed lookups.</param>
    /// <returns>
    /// <see langword="true"/> when the resolved state type symbol declares (or
    /// inherits) a public, non-static <c>Phase</c> property; otherwise
    /// <see langword="false"/> (including when the symbol cannot be resolved).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public static bool StateHasPhaseProperty(FluentDslParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        context.CancellationToken.ThrowIfCancellationRequested();

        var createInvocation = context.AllInvocations
            .FirstOrDefault(inv => SyntaxHelper.IsMethodCall(inv, "Create"));

        if (createInvocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (memberAccess.Expression is not GenericNameSyntax genericName)
        {
            return false;
        }

        var typeArgument = genericName.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArgument is null)
        {
            return false;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(typeArgument);
        if (symbolInfo.Symbol is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Walk the type and its base types for a public, non-static Phase property.
        for (var current = namedType; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers("Phase"))
            {
                if (member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public })
                {
                    return true;
                }
            }
        }

        return false;
    }
}
