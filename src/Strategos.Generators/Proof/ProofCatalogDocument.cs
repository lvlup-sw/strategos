// -----------------------------------------------------------------------
// <copyright file="ProofCatalogDocument.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Strategos.Analyzers.Proof;

namespace Strategos.Generators.Proof;

// =============================================================================
// The portable proof catalog, as the generator holds it (#204).
//
// This is the in-memory shape on BOTH sides of the boundary: the producing
// compilation builds one from what it declared, and the consuming compilation
// reads one back out of a referenced assembly. The wire format is
// Contracts' ProofCatalogV1; ProofCatalogWriter and ProofCatalogReader are the
// only code that knows about it.
//
// Everything here is ordinal and language-neutral. There is no CLR Type on this
// shape, by construction: an identity is three strings, a frame entry is a
// property name, and an authority is a name plus the coordinate it resolves to.
// A catalog that could carry a Type would be a catalog only .NET could read.
// =============================================================================

/// <summary>One action contract as it crosses an assembly boundary.</summary>
internal sealed class ProofCatalogAction
{
    internal ProofCatalogAction(
        ActionIdentity identity,
        string? boundWorkflowName,
        OntologyPredicateContract requirement,
        OntologyPredicateContract guarantee,
        ImmutableArray<string> frame,
        string? requiredAuthority,
        ImmutableArray<KeyValuePair<string, string>> authorityCoordinates,
        string? compensatingActionName)
    {
        Identity = identity;
        BoundWorkflowName = boundWorkflowName;
        Requirement = requirement;
        Guarantee = guarantee;
        Frame = frame.IsDefault ? ImmutableArray<string>.Empty : frame;
        RequiredAuthority = requiredAuthority;
        AuthorityCoordinates = authorityCoordinates.IsDefault
            ? ImmutableArray<KeyValuePair<string, string>>.Empty
            : authorityCoordinates;
        CompensatingActionName = compensatingActionName;
    }

    /// <summary>Gets the ordinal three-part action identity.</summary>
    internal ActionIdentity Identity { get; }

    /// <summary>
    /// Gets the ordinal workflow name this action claims to implement, or
    /// <see langword="null"/> when it binds none.
    /// </summary>
    internal string? BoundWorkflowName { get; }

    /// <summary>Gets the precondition.</summary>
    internal OntologyPredicateContract Requirement { get; }

    /// <summary>Gets the post-state guarantee.</summary>
    internal OntologyPredicateContract Guarantee { get; }

    /// <summary>Gets the frame — every resource the action may change.</summary>
    internal ImmutableArray<string> Frame { get; }

    /// <summary>Gets the authority literal the action demands, if any.</summary>
    internal string? RequiredAuthority { get; }

    /// <summary>
    /// Gets the axis-to-level coordinate <see cref="RequiredAuthority"/> resolves to,
    /// ordered by axis. Empty when the action demands no authority.
    /// </summary>
    /// <remarks>
    /// Resolved at EXPORT, where the declaring lattice is in hand. Two requirements
    /// then compare without either side resolving a name against a lattice it may
    /// not have, which is the whole reason the wire carries a coordinate rather than
    /// a name. The name travels alongside as provenance, and a reader that has both
    /// can check them against each other.
    /// </remarks>
    internal ImmutableArray<KeyValuePair<string, string>> AuthorityCoordinates { get; }

    /// <summary>Gets the name of the action that undoes this one, if declared.</summary>
    internal string? CompensatingActionName { get; }

    /// <summary>Projects a compilation-local contract into its portable form.</summary>
    /// <param name="contract">The contract the local catalog parsed.</param>
    /// <param name="coordinates">The coordinate its authority resolves to.</param>
    /// <returns>The portable action.</returns>
    internal static ProofCatalogAction FromLocal(
        OntologyActionContract contract,
        ImmutableArray<KeyValuePair<string, string>> coordinates) => new(
        contract.Identity,
        contract.BoundWorkflowName,
        contract.Requirement,
        contract.Guarantee,
        contract.Frame,
        contract.RequiredAuthority,
        coordinates,
        contract.CompensatingActionName);
}

/// <summary>
/// A whole assembly's exported proof catalog: every action contract it declares,
/// plus the authority lattices those contracts' authorities are read against.
/// </summary>
/// <remarks>
/// The lattices travel with the contracts deliberately. An action whose
/// <c>RequiredAuthority</c> arrives without the lattice that orders it is an
/// authority nobody downstream can compare, and comparing authorities is one of
/// the five refinement obligations. Shipping the contract without its lattice
/// would produce a catalog that looks complete and proves nothing.
/// </remarks>
internal sealed class ProofCatalogDocument
{
    internal ProofCatalogDocument(
        string catalogId,
        ImmutableArray<ProofCatalogAction> actions,
        ImmutableArray<OntologyAuthorityLattice> authorityLattices,
        string? contentHash = null)
    {
        CatalogId = catalogId;
        Actions = actions.IsDefault ? ImmutableArray<ProofCatalogAction>.Empty : actions;
        AuthorityLattices = authorityLattices.IsDefault
            ? ImmutableArray<OntologyAuthorityLattice>.Empty
            : authorityLattices;
        ContentHash = contentHash;
    }

    /// <summary>Gets the ordinal identity of the assembly this catalog describes.</summary>
    internal string CatalogId { get; }

    /// <summary>
    /// Gets the action contracts, ordered by identity. The order is part of the
    /// content hash, so two builds of the same declarations produce the same bytes.
    /// </summary>
    internal ImmutableArray<ProofCatalogAction> Actions { get; }

    /// <summary>Gets the authority lattices, ordered by domain name.</summary>
    internal ImmutableArray<OntologyAuthorityLattice> AuthorityLattices { get; }

    /// <summary>
    /// Gets the SHA-256 over the catalog's canonical bytes, or <see langword="null"/>
    /// on a catalog that has been built but not yet stamped.
    /// </summary>
    internal string? ContentHash { get; }

    /// <summary>
    /// Builds a catalog from what a compilation declared, in canonical order.
    /// </summary>
    /// <remarks>
    /// Actions with an <c>InvalidReason</c> are excluded. An invalid contract is one
    /// the producing compilation already refused to reason about, and exporting it
    /// would hand a consumer a contract that looks provable and is not. Its absence
    /// is what a consumer sees, and a binding whose contract is absent fails closed
    /// there — the same verdict, reported where the binding is.
    /// </remarks>
    /// <param name="catalogId">The producing assembly's name.</param>
    /// <param name="catalog">The compilation-local ontology catalog.</param>
    /// <param name="exportFailures">
    /// Each action that could not be exported, with the reason. The caller decides
    /// which of these matter: a contract whose binding this compilation can prove on
    /// its own never needed to travel.
    /// </param>
    /// <returns>The catalog to export, unstamped.</returns>
    internal static ProofCatalogDocument FromLocalCatalog(
        string catalogId,
        OntologyActionCatalog catalog,
        out ImmutableArray<(ActionIdentity Identity, string Reason)> exportFailures)
    {
        var lattices = catalog.AuthorityLattices
            .SelectMany(pair => pair.Value)
            .Where(lattice => lattice.InvalidReason is null)
            .OrderBy(lattice => lattice.DomainName, StringComparer.Ordinal)
            .ToImmutableArray();

        var byDomain = new Dictionary<string, OntologyAuthorityLattice>(StringComparer.Ordinal);
        foreach (var lattice in lattices)
        {
            byDomain[lattice.DomainName] = lattice;
        }

        var failures = ImmutableArray.CreateBuilder<(ActionIdentity, string)>();
        var actions = ImmutableArray.CreateBuilder<ProofCatalogAction>();

        foreach (var contract in catalog.Actions
            .Where(action => action.InvalidReason is null)
            .OrderBy(action => action.Identity.DomainName, StringComparer.Ordinal)
            .ThenBy(action => action.Identity.ObjectTypeName, StringComparer.Ordinal)
            .ThenBy(action => action.Identity.ActionName, StringComparer.Ordinal))
        {
            var unexportable = ProofCatalogProjection.DescribeUnexportable(contract.Requirement.Formula)
                ?? ProofCatalogProjection.DescribeUnexportable(contract.Guarantee.Formula);
            if (unexportable is not null)
            {
                failures.Add((contract.Identity, unexportable));
                continue;
            }

            var coordinates = ImmutableArray<KeyValuePair<string, string>>.Empty;
            if (contract.RequiredAuthority is { } authority)
            {
                // An authority that cannot be resolved to a coordinate here cannot be
                // compared anywhere downstream. Exporting it as a bare name would hand
                // a consumer an obligation it has no way to discharge, so the export
                // fails instead and the producing build says why.
                if (!byDomain.TryGetValue(contract.Identity.DomainName, out var lattice))
                {
                    failures.Add((
                        contract.Identity,
                        $"it requires authority '{authority}', but domain "
                        + $"'{contract.Identity.DomainName}' declares no authority lattice"));
                    continue;
                }

                var resolved = lattice.Authorities
                    .Where(pair => string.Equals(pair.Key, authority, StringComparison.Ordinal))
                    .Select(pair => pair.Value)
                    .FirstOrDefault();
                if (resolved is null)
                {
                    failures.Add((
                        contract.Identity,
                        $"it requires authority '{authority}', which the "
                        + $"'{contract.Identity.DomainName}' authority lattice does not define"));
                    continue;
                }

                coordinates = resolved
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToImmutableArray();
            }

            actions.Add(ProofCatalogAction.FromLocal(contract, coordinates));
        }

        exportFailures = failures.ToImmutable();
        return new ProofCatalogDocument(catalogId, actions.ToImmutable(), lattices);
    }

    /// <summary>Returns this catalog with its content hash stamped.</summary>
    /// <param name="contentHash">The lowercase-hex SHA-256 of the canonical bytes.</param>
    /// <returns>The stamped catalog.</returns>
    internal ProofCatalogDocument WithContentHash(string contentHash) =>
        new(CatalogId, Actions, AuthorityLattices, contentHash);

    /// <summary>Gets the exported actions that claim to implement a workflow.</summary>
    /// <returns>Every action carrying a workflow binding.</returns>
    internal IEnumerable<ProofCatalogAction> BoundActions() =>
        Actions.Where(action => !string.IsNullOrEmpty(action.BoundWorkflowName));
}
