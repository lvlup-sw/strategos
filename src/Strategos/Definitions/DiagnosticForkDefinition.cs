// =============================================================================
// <copyright file="DiagnosticForkDefinition.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Definitions;

/// <summary>
/// Immutable builder-IR definition of a diagnostic-fork edge (DR-7, #151): where a
/// workflow may fork a diagnostic remediation path, which closed triggers may fork it
/// (each paired with its declaration-side evidence-ref schema), the upper bound on the
/// forks the edge may spawn, and the compensation seed the fork routes to. This is the
/// in-memory half that <c>WorkflowDefinitionProjection</c> projects to the wire
/// <see cref="Strategos.Contracts.Generated.DiagnosticForkDefinition"/>.
/// </summary>
/// <remarks>
/// <para>
/// INV-8: every step/type reference on this shape is a string moniker — the anchor
/// step ids, the evidence field names, and the compensation seed are all plain
/// strings; the only typed reference is the closed <c>ForkTrigger</c> enum (a
/// snake_case wire vocabulary, not a CLR type). Nothing here carries a runtime
/// <c>Type</c>, an assembly-qualified name, or executable code.
/// </para>
/// </remarks>
public sealed record DiagnosticForkDefinition
{
    /// <summary>
    /// Gets the anchor step monikers — the step ids where this workflow may fork
    /// (INV-8: simple-name step id monikers, never CLR types). At least one anchor: a
    /// fork edge with nowhere to fork is unrepresentable.
    /// </summary>
    public IReadOnlyList<string> AnchorStepIds { get; init; } = [];

    /// <summary>
    /// Gets the closed triggers permitted to fork this workflow, each paired with its
    /// declaration-side evidence-ref schema. At least one: the edge is inexpressible
    /// without declaring a permitted trigger (DR-7).
    /// </summary>
    public IReadOnlyList<PermittedForkTriggerDefinition> PermittedTriggers { get; init; } = [];

    /// <summary>
    /// Gets the compensation seed moniker (INV-8: a plain string moniker, never a CLR
    /// type) — the seed the fork routes compensation to, composing with the existing
    /// Compensate / OnFailure merged trigger site (DR-9). Required and non-empty.
    /// </summary>
    public string CompensationSeed { get; init; } = string.Empty;

    /// <summary>
    /// Gets the upper bound on the forks this edge may spawn. The generated guard will
    /// enforce it (DR-9; the <see cref="LoopDefinition.MaxIterations"/> forced-exit
    /// precedent) — exceeding it routes to a blocked / human-escalation terminal. At
    /// least 1: a bound of 0 forbids the very fork the edge exists to permit.
    /// </summary>
    public int MaxForks { get; init; }

    /// <summary>
    /// Creates a diagnostic-fork definition, validating the DR-7 floor: at least one
    /// non-empty anchor, at least one permitted trigger, a non-empty compensation seed,
    /// and a <paramref name="maxForks"/> bound of at least 1.
    /// </summary>
    /// <param name="anchorStepIds">The anchor step monikers (at least one, each non-empty).</param>
    /// <param name="permittedTriggers">The permitted triggers (at least one).</param>
    /// <param name="compensationSeed">The compensation seed moniker (non-empty).</param>
    /// <param name="maxForks">The upper bound on forks (at least 1).</param>
    /// <returns>A validated diagnostic-fork definition.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="anchorStepIds"/> or <paramref name="permittedTriggers"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when there is no anchor, an anchor is null/whitespace, there is no
    /// permitted trigger, or <paramref name="compensationSeed"/> is null/whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxForks"/> is less than 1.
    /// </exception>
    public static DiagnosticForkDefinition Create(
        IReadOnlyList<string> anchorStepIds,
        IReadOnlyList<PermittedForkTriggerDefinition> permittedTriggers,
        string compensationSeed,
        int maxForks)
    {
        ArgumentNullException.ThrowIfNull(anchorStepIds, nameof(anchorStepIds));
        ArgumentNullException.ThrowIfNull(permittedTriggers, nameof(permittedTriggers));
        ArgumentException.ThrowIfNullOrWhiteSpace(compensationSeed, nameof(compensationSeed));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxForks, 1, nameof(maxForks));

        if (anchorStepIds.Count == 0)
        {
            throw new ArgumentException(
                "A diagnostic fork must declare at least one anchor step.",
                nameof(anchorStepIds));
        }

        foreach (var anchor in anchorStepIds)
        {
            if (string.IsNullOrWhiteSpace(anchor))
            {
                throw new ArgumentException(
                    "Anchor step ids must be non-empty.",
                    nameof(anchorStepIds));
            }
        }

        if (permittedTriggers.Count == 0)
        {
            throw new ArgumentException(
                "A diagnostic fork must declare at least one permitted trigger.",
                nameof(permittedTriggers));
        }

        return new DiagnosticForkDefinition
        {
            AnchorStepIds = [.. anchorStepIds],
            PermittedTriggers = [.. permittedTriggers],
            CompensationSeed = compensationSeed,
            MaxForks = maxForks,
        };
    }
}
