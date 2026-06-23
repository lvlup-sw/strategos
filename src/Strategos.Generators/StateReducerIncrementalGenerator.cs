// -----------------------------------------------------------------------
// <copyright file="StateReducerIncrementalGenerator.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Emitters;
using Strategos.Generators.Helpers;
using Strategos.Generators.Models;

namespace Strategos.Generators;

/// <summary>
/// Incremental source generator that produces state reducer classes
/// from records/structs marked with [WorkflowState] attribute.
/// </summary>
[Generator]
public sealed class StateReducerIncrementalGenerator : IIncrementalGenerator
{
    private const string WorkflowStateAttributeFullName = "Strategos.Attributes.WorkflowStateAttribute";
    private const string AppendAttributeFullName = "Strategos.Attributes.AppendAttribute";
    private const string MergeAttributeFullName = "Strategos.Attributes.MergeAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes/records/structs with [WorkflowState] attribute
        var stateDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                WorkflowStateAttributeFullName,
                predicate: static (node, _) => IsValidTargetNode(node),
                transform: static (ctx, ct) => TransformToResult(ctx, ct));

        // Register source output for each state type
        context.RegisterSourceOutput(stateDeclarations, static (spc, result) =>
        {
            // Report diagnostics
            foreach (var diagnostic in result.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            // Generate source if model is valid
            if (result.Model is not null)
            {
                var source = StateReducerEmitter.Emit(result.Model);
                var hintName = $"{result.Model.ReducerClassName}.g.cs";
                GeneratedCodeStamper.AddStampedSource(spc, hintName, source);
            }
        });
    }

    private static bool IsValidTargetNode(SyntaxNode node)
    {
        return node is RecordDeclarationSyntax or ClassDeclarationSyntax or StructDeclarationSyntax;
    }

    private static StateReducerGeneratorResult TransformToResult(
        GeneratorAttributeSyntaxContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var diagnostics = new List<Diagnostic>();

        // Get type symbol
        var symbol = context.TargetSymbol as INamedTypeSymbol;
        if (symbol is null)
        {
            return new StateReducerGeneratorResult(null, diagnostics);
        }

        // Get namespace
        var ns = symbol.ContainingNamespace?.ToDisplayString();
        if (string.IsNullOrEmpty(ns) || ns == "<global namespace>")
        {
            return new StateReducerGeneratorResult(null, diagnostics);
        }

        // Get type name
        var typeName = symbol.Name;

        // Extract properties with their kinds (and validate attribute usage)
        var properties = ExtractProperties(symbol, context.SemanticModel.Compilation, diagnostics);

        var model = new StateModel(
            TypeName: typeName,
            Namespace: ns!,
            Properties: properties);

        return new StateReducerGeneratorResult(model, diagnostics);
    }

    private static IReadOnlyList<StatePropertyModel> ExtractProperties(
        INamedTypeSymbol symbol,
        Compilation compilation,
        List<Diagnostic> diagnostics)
    {
        var properties = new List<StatePropertyModel>();

        // Cache well-known types once per compilation context for all properties
        var wellKnownTypes = WellKnownTypes.FromCompilation(compilation);

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
            {
                continue;
            }

            // Skip non-public properties
            if (property.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            // Skip write-only properties
            if (property.GetMethod is null)
            {
                continue;
            }

            // Determine property kind based on attributes (with validation)
            var kind = GetPropertyKind(property, wellKnownTypes, diagnostics);

            properties.Add(new StatePropertyModel(
                Name: property.Name,
                TypeName: property.Type.ToDisplayString(),
                Kind: kind));
        }

        return properties;
    }

    private static StatePropertyKind GetPropertyKind(
        IPropertySymbol property,
        WellKnownTypes wellKnownTypes,
        List<Diagnostic> diagnostics)
    {
        foreach (var attribute in property.GetAttributes())
        {
            var fullName = attribute.AttributeClass?.ToDisplayString();

            if (fullName == AppendAttributeFullName)
            {
                // Validate that the property type implements IEnumerable<T>
                if (!IsCollectionType(property.Type, wellKnownTypes))
                {
                    var location = GetPropertyLocation(property);
                    diagnostics.Add(Diagnostic.Create(
                        StateReducerDiagnostics.AppendOnNonCollection,
                        location,
                        property.Name,
                        property.Type.ToDisplayString()));
                }

                return StatePropertyKind.Append;
            }

            if (fullName == MergeAttributeFullName)
            {
                // Validate that the property type is a dictionary type
                if (!IsDictionaryType(property.Type, wellKnownTypes))
                {
                    var location = GetPropertyLocation(property);
                    diagnostics.Add(Diagnostic.Create(
                        StateReducerDiagnostics.MergeOnNonDictionary,
                        location,
                        property.Name,
                        property.Type.ToDisplayString()));
                }

                return StatePropertyKind.Merge;
            }
        }

        return StatePropertyKind.Standard;
    }

    private static bool IsCollectionType(ITypeSymbol type, WellKnownTypes wellKnownTypes)
    {
        // Check if type implements IEnumerable<T> (but not string)
        if (type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        if (wellKnownTypes.EnumerableT is null)
        {
            return false;
        }

        // Check if type itself is IEnumerable<T> (for interface types)
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var originalDef = namedType.OriginalDefinition;
            if (originalDef.Equals(wellKnownTypes.EnumerableT, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        // Check if the type implements IEnumerable<T>
        return type.AllInterfaces.Any(i =>
            i.OriginalDefinition.Equals(wellKnownTypes.EnumerableT, SymbolEqualityComparer.Default));
    }

    private static bool IsDictionaryType(ITypeSymbol type, WellKnownTypes wellKnownTypes)
    {
        // Check if type implements IReadOnlyDictionary<TKey, TValue> or IDictionary<TKey, TValue>
        if (wellKnownTypes.ReadOnlyDictT is null && wellKnownTypes.DictT is null)
        {
            return false;
        }

        // Check if type itself is the dictionary type (for interface types)
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var originalDef = namedType.OriginalDefinition;
            if ((wellKnownTypes.ReadOnlyDictT is not null && originalDef.Equals(wellKnownTypes.ReadOnlyDictT, SymbolEqualityComparer.Default))
                || (wellKnownTypes.DictT is not null && originalDef.Equals(wellKnownTypes.DictT, SymbolEqualityComparer.Default)))
            {
                return true;
            }
        }

        // Check if type implements the dictionary interface
        return type.AllInterfaces.Any(i =>
            (wellKnownTypes.ReadOnlyDictT is not null && i.OriginalDefinition.Equals(wellKnownTypes.ReadOnlyDictT, SymbolEqualityComparer.Default))
            || (wellKnownTypes.DictT is not null && i.OriginalDefinition.Equals(wellKnownTypes.DictT, SymbolEqualityComparer.Default)));
    }

    /// <summary>
    /// Caches well-known type symbols from the compilation to avoid repeated lookups.
    /// </summary>
    private sealed class WellKnownTypes
    {
        /// <summary>
        /// Gets the IEnumerable{T} type symbol.
        /// </summary>
        public INamedTypeSymbol? EnumerableT { get; init; }

        /// <summary>
        /// Gets the IReadOnlyDictionary{TKey, TValue} type symbol.
        /// </summary>
        public INamedTypeSymbol? ReadOnlyDictT { get; init; }

        /// <summary>
        /// Gets the IDictionary{TKey, TValue} type symbol.
        /// </summary>
        public INamedTypeSymbol? DictT { get; init; }

        /// <summary>
        /// Creates a new <see cref="WellKnownTypes"/> instance from the compilation.
        /// </summary>
        /// <param name="compilation">The compilation to extract type symbols from.</param>
        /// <returns>A new <see cref="WellKnownTypes"/> instance with cached type symbols.</returns>
        public static WellKnownTypes FromCompilation(Compilation compilation) => new()
        {
            EnumerableT = compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1"),
            ReadOnlyDictT = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2"),
            DictT = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2"),
        };
    }

    private static Location GetPropertyLocation(IPropertySymbol property)
    {
        var syntaxRef = property.DeclaringSyntaxReferences.FirstOrDefault();
        return syntaxRef?.GetSyntax().GetLocation() ?? Location.None;
    }

    /// <summary>
    /// Result of transforming a state declaration, including model and diagnostics.
    /// </summary>
    private sealed record StateReducerGeneratorResult(
        StateModel? Model,
        IReadOnlyList<Diagnostic> Diagnostics);
}
