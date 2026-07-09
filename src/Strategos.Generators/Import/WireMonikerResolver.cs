// -----------------------------------------------------------------------
// <copyright file="WireMonikerResolver.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Diagnostics;

namespace Strategos.Generators.Import;

// =============================================================================
// DR-13 (#100) — wire-moniker → CLR step-type resolution.
//
// The JSON import wire IR carries step types as plain simple-name string
// monikers (INV-8; see WireDtos). This resolver is the ONLY place those
// monikers are turned into a compile-time type identity: it resolves a moniker
// to exactly one accessible INamedTypeSymbol implementing the workflow-step
// contract (Strategos.Abstractions.IWorkflowStep<TState>) against the
// compilation symbol table, and surfaces a STABLE diagnostic for the two ways a
// moniker can fail to bind — no candidate (unresolvable) or 2+ candidates
// (ambiguous).
//
// INV-8: resolution CONSUMES the moniker string and yields a Roslyn symbol
// (a compile-time INamedTypeSymbol) — never a CLR System.Type, and nothing is
// written back onto the wire DTO. The moniker field on the DTO stays a string.
//
// SCOPE: this is the resolver only. The wire-IR -> WorkflowModel bridge that
// consumes the resolved symbol is task 017; carrier/semantic rejection
// (dangling gateId, reliability-bearing carrier steps) is task 018.
// =============================================================================

/// <summary>
/// Resolves a wire simple-name step moniker to exactly one accessible
/// <see cref="INamedTypeSymbol"/> implementing the workflow-step contract, against a
/// <see cref="Compilation"/>'s symbol table.
/// </summary>
/// <remarks>
/// <para>
/// A candidate is any type in the compilation symbol table (source or referenced metadata)
/// whose simple name equals the moniker, that implements
/// <c>Strategos.Abstractions.IWorkflowStep&lt;TState&gt;</c>, and that is accessible from the
/// compilation's own assembly (where the generator's output is compiled). The step-contract
/// filter is what keeps unrelated same-named BCL types (e.g. <c>List</c>) from being candidates.
/// </para>
/// <para>
/// Resolution is a pure function of (compilation, moniker): the same moniker resolves to the same
/// canonical symbol every time. It does NOT rename, wrap, or synthesize per-import types, so a
/// moniker shared across two imported workflow definitions maps to the SAME CLR type — the
/// one-step-type-per-workflow-definition collision (the CS0101 class) is preserved for the
/// downstream generator to surface, not masked here.
/// </para>
/// <para>
/// The resolver walks the merged global namespace per call; a caller resolving many monikers over
/// one compilation (the task-017 bridge) may cache the walk. Kept out of scope here.
/// </para>
/// </remarks>
internal static class WireMonikerResolver
{
    /// <summary>
    /// Deterministic, human-readable fully-qualified name format for candidate listing — namespace
    /// and containing types, no <c>global::</c> prefix. Ordering the display strings ordinally makes
    /// the ambiguity diagnostic stable regardless of symbol-table enumeration order.
    /// </summary>
    private static readonly SymbolDisplayFormat FullyQualifiedName = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    /// <summary>
    /// Resolves <paramref name="moniker"/> against <paramref name="compilation"/>.
    /// </summary>
    /// <param name="compilation">The compilation whose symbol table is searched.</param>
    /// <param name="moniker">The wire simple-name step moniker to resolve.</param>
    /// <param name="jsonFilePath">The import file path, threaded into the failure diagnostics.</param>
    /// <returns>
    /// A <see cref="WireMonikerResolution"/>: resolved (carrying the single symbol), unresolvable, or
    /// ambiguous (each failure carrying the stable diagnostic). The moniker is consumed as a string;
    /// no CLR <see cref="System.Type"/> is produced (INV-8).
    /// </returns>
    public static WireMonikerResolution Resolve(
        Compilation compilation,
        string moniker,
        string jsonFilePath)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        var candidates = FindStepCandidates(compilation, moniker).ToList();

        if (candidates.Count == 0)
        {
            return WireMonikerResolution.Unresolved(Diagnostic.Create(
                WorkflowDiagnostics.UnresolvableStepMoniker,
                Location.None,
                jsonFilePath,
                moniker ?? string.Empty));
        }

        if (candidates.Count > 1)
        {
            var listed = string.Join(
                ", ",
                candidates
                    .Select(c => c.ToDisplayString(FullyQualifiedName))
                    .OrderBy(name => name, StringComparer.Ordinal));

            return WireMonikerResolution.Ambiguous(Diagnostic.Create(
                WorkflowDiagnostics.AmbiguousStepMoniker,
                Location.None,
                jsonFilePath,
                moniker ?? string.Empty,
                listed));
        }

        return WireMonikerResolution.Resolved(candidates[0]);
    }

    /// <summary>
    /// Enumerates every accessible <c>IWorkflowStep&lt;TState&gt;</c> implementation in the
    /// compilation symbol table whose simple name equals <paramref name="moniker"/>.
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> FindStepCandidates(Compilation compilation, string moniker)
    {
        if (string.IsNullOrEmpty(moniker))
        {
            yield break;
        }

        foreach (var type in EnumerateTypes(compilation.GlobalNamespace))
        {
            if (!string.Equals(type.Name, moniker, StringComparison.Ordinal))
            {
                continue;
            }

            // A moniker names a concrete step implementation, not an interface that extends the
            // contract. The IWorkflowStep interface itself never lists itself in AllInterfaces, so
            // it is excluded regardless.
            if (type.TypeKind == TypeKind.Interface)
            {
                continue;
            }

            if (!ImplementsWorkflowStep(type))
            {
                continue;
            }

            if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
            {
                continue;
            }

            yield return type;
        }
    }

    /// <summary>
    /// Depth-first walk of every named type reachable from <paramref name="root"/> — namespaces,
    /// their types, and nested types — across source and referenced metadata (the merged global
    /// namespace is the compilation symbol table).
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol root)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            switch (current)
            {
                case INamespaceSymbol ns:
                    foreach (var member in ns.GetMembers())
                    {
                        stack.Push(member);
                    }

                    break;

                case INamedTypeSymbol type:
                    yield return type;
                    foreach (var nested in type.GetTypeMembers())
                    {
                        stack.Push(nested);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Determines whether <paramref name="type"/> implements
    /// <c>Strategos.Abstractions.IWorkflowStep&lt;TState&gt;</c> (any state type argument). Mirrors
    /// the C#-authored path's step-contract recognition so imported and authored steps are judged
    /// by one rule.
    /// </summary>
    private static bool ImplementsWorkflowStep(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(IsWorkflowStepInterface);

    /// <summary>
    /// Determines whether <paramref name="iface"/> is the generic
    /// <c>Strategos.Abstractions.IWorkflowStep&lt;&gt;</c> interface, matched on its open definition's
    /// metadata name + containing namespace (robust to display formatting).
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
}

/// <summary>
/// The outcome of resolving a wire step moniker (DR-13).
/// </summary>
internal enum WireMonikerOutcome
{
    /// <summary>The moniker bound to exactly one accessible step type.</summary>
    Resolved,

    /// <summary>The moniker bound to no accessible step type.</summary>
    Unresolvable,

    /// <summary>The moniker bound to two or more accessible step types sharing the simple name.</summary>
    Ambiguous,
}

/// <summary>
/// The result of <see cref="WireMonikerResolver.Resolve"/>: either a single resolved symbol, or a
/// failure carrying the stable diagnostic.
/// </summary>
/// <remarks>
/// INV-8: the resolved identity is a Roslyn <see cref="INamedTypeSymbol"/> (a compile-time symbol),
/// never a CLR <see cref="System.Type"/>. This type intentionally exposes no <see cref="System.Type"/>
/// member so a CLR type cannot leak back into contract state through the resolver.
/// </remarks>
internal sealed class WireMonikerResolution
{
    private WireMonikerResolution(
        WireMonikerOutcome outcome,
        INamedTypeSymbol? symbol,
        Diagnostic? diagnostic)
    {
        this.Outcome = outcome;
        this.Symbol = symbol;
        this.Diagnostic = diagnostic;
    }

    /// <summary>Gets the classification of this resolution.</summary>
    public WireMonikerOutcome Outcome { get; }

    /// <summary>Gets a value indicating whether the moniker bound to exactly one step type.</summary>
    public bool IsResolved => this.Outcome == WireMonikerOutcome.Resolved;

    /// <summary>
    /// Gets the resolved step type symbol when <see cref="IsResolved"/> is <see langword="true"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public INamedTypeSymbol? Symbol { get; }

    /// <summary>
    /// Gets the stable failure diagnostic when the moniker did not bind to exactly one type;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public Diagnostic? Diagnostic { get; }

    /// <summary>Creates a resolved result carrying the single bound symbol.</summary>
    public static WireMonikerResolution Resolved(INamedTypeSymbol symbol) =>
        new WireMonikerResolution(WireMonikerOutcome.Resolved, symbol, diagnostic: null);

    /// <summary>Creates an unresolvable result carrying the miss diagnostic.</summary>
    public static WireMonikerResolution Unresolved(Diagnostic diagnostic) =>
        new WireMonikerResolution(WireMonikerOutcome.Unresolvable, symbol: null, diagnostic);

    /// <summary>Creates an ambiguous result carrying the ambiguity diagnostic.</summary>
    public static WireMonikerResolution Ambiguous(Diagnostic diagnostic) =>
        new WireMonikerResolution(WireMonikerOutcome.Ambiguous, symbol: null, diagnostic);
}
