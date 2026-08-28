// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkModel.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Generator IR for one diagnostic-fork edge (DR-9, #151): where a workflow may fork a
/// diagnostic remediation path, which closed triggers may fork it (each paired with its
/// declaration-side evidence-ref schema), the upper bound on the forks the edge may
/// spawn, and the compensation seed the fork routes to.
/// </summary>
/// <remarks>
/// <para>
/// This is the GENERATOR half of the fork edge. It is re-parsed from the fluent
/// <c>AllowDiagnosticFork(...)</c> chain by <c>DiagnosticForkExtractor</c> (Roslyn syntax),
/// mirroring the runtime builder IR <c>Strategos.Definitions.DiagnosticForkDefinition</c>.
/// The saga LOWERING that emits fork guards/events from this model is deferred to a later
/// task (#151); this record only models the edge so it can be attached to
/// <see cref="WorkflowModel.DiagnosticForks"/>.
/// </para>
/// <para>
/// INV-8: every step/type reference on this shape is a string moniker — the anchor step
/// ids, the compensation seed, and each trigger's evidence field names are all plain
/// strings. The trigger itself is carried as its enum member NAME
/// (<see cref="PermittedForkTriggerModel.TriggerName"/>), extracted syntactically from the
/// <c>ForkTrigger.X</c> member access; nothing here carries a runtime <c>Type</c> or an
/// assembly-qualified name.
/// </para>
/// </remarks>
/// <param name="AnchorStepMonikers">
/// The anchor step monikers — the step ids where this workflow may fork (INV-8: simple-name
/// step id monikers, never CLR types). At least one: a fork edge with nowhere to fork is
/// unrepresentable.
/// </param>
/// <param name="PermittedTriggers">
/// The closed triggers permitted to fork this workflow, each paired with its declaration-side
/// evidence-ref schema. At least one: the edge is inexpressible without a permitted trigger
/// (DR-7).
/// </param>
/// <param name="CompensationSeedMoniker">
/// The compensation seed moniker (INV-8: a plain string moniker, never a CLR type) — the seed
/// the fork routes compensation to. Required and non-empty.
/// </param>
/// <param name="MaxForks">
/// The upper bound on the forks this edge may spawn. The generated guard will enforce it
/// (DR-9; the loop <c>MaxIterations</c> forced-exit precedent). At least 1: a bound of 0
/// forbids the very fork the edge exists to permit.
/// </param>
internal sealed record DiagnosticForkModel(
    IReadOnlyList<string> AnchorStepMonikers,
    IReadOnlyList<PermittedForkTriggerModel> PermittedTriggers,
    string CompensationSeedMoniker,
    int MaxForks)
{
    /// <summary>
    /// Gets the number of anchor step monikers declared on this edge.
    /// </summary>
    public int AnchorCount => AnchorStepMonikers.Count;

    /// <summary>
    /// Gets the number of permitted triggers declared on this edge.
    /// </summary>
    public int PermittedTriggerCount => PermittedTriggers.Count;

    /// <summary>
    /// Creates a diagnostic-fork model, validating the DR-7 floor: at least one non-empty
    /// anchor, at least one permitted trigger, a non-empty compensation seed, and a
    /// <paramref name="maxForks"/> bound of at least 1.
    /// </summary>
    /// <param name="anchorStepMonikers">The anchor step monikers (at least one, each non-empty).</param>
    /// <param name="permittedTriggers">The permitted triggers (at least one).</param>
    /// <param name="compensationSeedMoniker">The compensation seed moniker (non-empty).</param>
    /// <param name="maxForks">The upper bound on forks (at least 1).</param>
    /// <returns>A validated <see cref="DiagnosticForkModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="anchorStepMonikers"/> or <paramref name="permittedTriggers"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there is no anchor, an anchor is null/whitespace, there is no permitted
    /// trigger, or <paramref name="compensationSeedMoniker"/> is null/whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxForks"/> is less than 1.
    /// </exception>
    public static DiagnosticForkModel Create(
        IReadOnlyList<string> anchorStepMonikers,
        IReadOnlyList<PermittedForkTriggerModel> permittedTriggers,
        string compensationSeedMoniker,
        int maxForks)
    {
        ThrowHelper.ThrowIfNull(anchorStepMonikers, nameof(anchorStepMonikers));
        ThrowHelper.ThrowIfNull(permittedTriggers, nameof(permittedTriggers));
        ThrowHelper.ThrowIfNullOrWhiteSpace(compensationSeedMoniker, nameof(compensationSeedMoniker));
        ThrowHelper.ThrowIfLessThan(maxForks, 1, nameof(maxForks));

        if (anchorStepMonikers.Count == 0)
        {
            throw new ArgumentException(
                "A diagnostic fork must declare at least one anchor step.",
                nameof(anchorStepMonikers));
        }

        foreach (var anchor in anchorStepMonikers)
        {
            if (string.IsNullOrWhiteSpace(anchor))
            {
                throw new ArgumentException(
                    "Anchor step monikers must be non-empty.",
                    nameof(anchorStepMonikers));
            }
        }

        if (permittedTriggers.Count == 0)
        {
            throw new ArgumentException(
                "A diagnostic fork must declare at least one permitted trigger.",
                nameof(permittedTriggers));
        }

        var duplicates = FindDuplicateTriggerNames(permittedTriggers.Select(static t => t.TriggerName));
        if (duplicates.Count > 0)
        {
            throw new ArgumentException(
                "A diagnostic fork must declare each trigger at most once. Duplicate: "
                    + duplicates[0] + ".",
                nameof(permittedTriggers));
        }

        return new DiagnosticForkModel(
            AnchorStepMonikers: [.. anchorStepMonikers],
            PermittedTriggers: [.. permittedTriggers],
            CompensationSeedMoniker: compensationSeedMoniker,
            MaxForks: maxForks);
    }

    /// <summary>
    /// Canonicalises a compensation-seed moniker into a C# identifier suffix, the same
    /// way fork-path saga properties sanitize <c>ForkId</c> for
    /// <c>Fork_{id}_Path{n}State</c>. The seed is authored DSL text (or free-form JSON
    /// on the import path), so it may carry ANY character; every character that C# does
    /// not accept in an identifier — '-', ' ', '.', '/', '"' and the rest — is mapped to
    /// '_'. The resulting token is the suffix of <see cref="CountPropertyName"/>, which
    /// the saga emitter writes as a property name and as an expression identifier, so a
    /// partial sanitizer produces a saga that does not compile.
    /// </summary>
    /// <remarks>
    /// The mapping is deliberately many-to-one: <c>"foo-bar"</c>, <c>"foo bar"</c> and
    /// <c>"foo_bar"</c> all canonicalise to <c>foo_bar</c>. That is not a silent merge —
    /// <see cref="FindDuplicateCompensationSeeds"/> keys on this same canonical value, so
    /// two edges that would land on one counter are reported as a duplicate compensation
    /// seed rather than sharing a tally.
    /// </remarks>
    /// <param name="compensationSeedMoniker">The authored compensation-seed moniker.</param>
    /// <returns>The identifier-safe moniker used as the <c>DiagnosticForkCount_</c> suffix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compensationSeedMoniker"/> is null.</exception>
    public static string SanitizeCompensationSeedMoniker(string compensationSeedMoniker)
    {
        ThrowHelper.ThrowIfNull(compensationSeedMoniker, nameof(compensationSeedMoniker));

        var buffer = new System.Text.StringBuilder(compensationSeedMoniker.Length);
        foreach (var c in compensationSeedMoniker)
        {
            buffer.Append(SyntaxFacts.IsIdentifierPartCharacter(c) ? c : '_');
        }

        return buffer.ToString();
    }

    /// <summary>
    /// Gets the per-edge saga counter property name, keyed by the canonicalised
    /// compensation-seed moniker (<c>DiagnosticForkCount_{seed}</c>). 2.10.0 keyed the
    /// same tally positionally as <c>DiagnosticForkCount_{i}</c>; 2.11.0 renames the
    /// persisted Marten field, so an in-flight saga written by 2.10.0 carries only the
    /// positional one. See <see cref="LegacyPositionalCountPropertyName"/> for the
    /// fold-forward shim that keeps the <c>maxForks</c> bound honest across that upgrade.
    /// </summary>
    public string CountPropertyName =>
        "DiagnosticForkCount_" + SanitizeCompensationSeedMoniker(CompensationSeedMoniker);

    /// <summary>
    /// Returns the 2.10.0 positional counter name (<c>DiagnosticForkCount_{i}</c>) that the
    /// saga must keep reading so an upgrade does not silently reset an edge's admitted-fork
    /// tally to <c>0</c> and admit another <c>MaxForks</c> forks. Marten deserialises the
    /// stored document into the regenerated saga type and drops members it no longer knows,
    /// so without this property the pre-upgrade count is simply lost.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the positional name is already the seed-keyed
    /// name of some edge (a seed of <c>"0"</c> canonicalises to <c>DiagnosticForkCount_0</c>).
    /// Emitting both would be a duplicate member; the seed-keyed property wins and the
    /// legacy value for that index is not recoverable — an accepted, bounded loss over a
    /// saga type that would not compile.
    /// </remarks>
    /// <param name="forks">Every diagnostic-fork edge on the workflow, in declaration order.</param>
    /// <param name="edgeIndex">The zero-based declaration index of this edge — the 2.10.0 key.</param>
    /// <returns>The legacy positional property name, or null when it collides.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="forks"/> is null.</exception>
    public static string? LegacyPositionalCountPropertyName(
        IReadOnlyList<DiagnosticForkModel> forks,
        int edgeIndex)
    {
        ThrowHelper.ThrowIfNull(forks, nameof(forks));

        var legacyName = "DiagnosticForkCount_" + edgeIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var fork in forks)
        {
            if (string.Equals(fork.CountPropertyName, legacyName, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return legacyName;
    }

    /// <summary>
    /// Returns each compensation seed whose sanitized moniker appears more than
    /// once, in first-seen-collision order. Empty seeds are ignored. Used by the
    /// C# extractor and the JSON-import bridge so two edges that would share a
    /// <c>DiagnosticForkCount_{seed}</c> property are rejected rather than
    /// merged onto one counter (#156.3).
    /// </summary>
    /// <param name="compensationSeeds">The compensation-seed monikers declared across edges.</param>
    /// <returns>The colliding seeds (the later original of each sanitized-key clash), or empty.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="compensationSeeds"/> is null.</exception>
    public static IReadOnlyList<string> FindDuplicateCompensationSeeds(IEnumerable<string> compensationSeeds)
    {
        ThrowHelper.ThrowIfNull(compensationSeeds, nameof(compensationSeeds));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seed in compensationSeeds)
        {
            if (string.IsNullOrEmpty(seed))
            {
                continue;
            }

            var key = SanitizeCompensationSeedMoniker(seed);
            if (!seen.Add(key) && reported.Add(key))
            {
                duplicates.Add(seed);
            }
        }

        return duplicates;
    }

    /// <summary>
    /// Returns each trigger name that appears more than once, in first-seen order.
    /// Empty names are ignored. Used by the C# extractor and the JSON-import bridge
    /// so a duplicate <c>PermitTrigger</c> is rejected rather than first-wins-deduped (#156.2).
    /// </summary>
    /// <param name="triggerNames">The trigger names declared on one edge.</param>
    /// <returns>The duplicated names, or an empty list when every name is unique.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="triggerNames"/> is null.</exception>
    public static IReadOnlyList<string> FindDuplicateTriggerNames(IEnumerable<string> triggerNames)
    {
        ThrowHelper.ThrowIfNull(triggerNames, nameof(triggerNames));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in triggerNames)
        {
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (!seen.Add(name) && reported.Add(name))
            {
                duplicates.Add(name);
            }
        }

        return duplicates;
    }
}

/// <summary>
/// Generator IR pairing of one closed fork trigger the workflow may fork on with the
/// DECLARATION-side evidence-ref schema for that trigger — the NAMES of the evidence fields a
/// future fork occurrence must carry to justify it (DR-7 / DR-8, #151).
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the runtime builder IR
/// <c>Strategos.Definitions.PermittedForkTriggerDefinition</c>. The trigger is carried as its
/// enum member NAME (e.g. <c>"RatificationFailure"</c>), extracted syntactically from the
/// <c>ForkTrigger.X</c> member access in the DSL — the generator parses over syntax and has no
/// reference to the closed <c>ForkTrigger</c> CLR enum, and the wire-vocabulary (snake_case)
/// mapping is applied at lowering, not in this IR.
/// </para>
/// <para>
/// This is the DECLARATION half: field-NAME declarations, never runtime VALUES. Every entry is
/// a plain string moniker (INV-8).
/// </para>
/// </remarks>
/// <param name="TriggerName">
/// The closed trigger's enum member name (e.g. <c>"RatificationFailure"</c>, <c>"GateContradiction"</c>).
/// </param>
/// <param name="RequiredEvidenceFields">
/// The evidence FIELD NAMES a future fork occurrence must carry for this trigger (declaration
/// side — NOT runtime values). Each is a plain string moniker (INV-8). At least one field.
/// </param>
internal sealed record PermittedForkTriggerModel(
    string TriggerName,
    IReadOnlyList<string> RequiredEvidenceFields)
{
    /// <summary>
    /// Creates a permitted-fork-trigger model, validating that a non-empty trigger name and at
    /// least one non-empty evidence field name are declared.
    /// </summary>
    /// <param name="triggerName">The closed trigger's enum member name (non-empty).</param>
    /// <param name="requiredEvidenceFields">
    /// The evidence field names the trigger's occurrences must carry (at least one, each non-empty).
    /// </param>
    /// <returns>A validated <see cref="PermittedForkTriggerModel"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredEvidenceFields"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="triggerName"/> is null/whitespace, or when
    /// <paramref name="requiredEvidenceFields"/> is empty or contains a null/whitespace field name.
    /// </exception>
    public static PermittedForkTriggerModel Create(
        string triggerName,
        IReadOnlyList<string> requiredEvidenceFields)
    {
        ThrowHelper.ThrowIfNullOrWhiteSpace(triggerName, nameof(triggerName));
        ThrowHelper.ThrowIfNull(requiredEvidenceFields, nameof(requiredEvidenceFields));

        if (requiredEvidenceFields.Count == 0)
        {
            throw new ArgumentException(
                "A permitted fork trigger must declare at least one evidence field name.",
                nameof(requiredEvidenceFields));
        }

        foreach (var field in requiredEvidenceFields)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                throw new ArgumentException(
                    "Evidence field names must be non-empty.",
                    nameof(requiredEvidenceFields));
            }
        }

        return new PermittedForkTriggerModel(
            TriggerName: triggerName,
            RequiredEvidenceFields: [.. requiredEvidenceFields]);
    }
}
