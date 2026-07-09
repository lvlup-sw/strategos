// =============================================================================
// <copyright file="PermittedForkTriggerDefinition.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using ForkTrigger = Strategos.Contracts.Generated.ForkTrigger;

namespace Strategos.Definitions;

/// <summary>
/// Immutable builder-IR pairing of one closed <see cref="ForkTrigger"/> the workflow
/// may fork on with the DECLARATION-side evidence-ref schema for that trigger — the
/// NAMES of the evidence fields a future fork occurrence must carry to justify it
/// (DR-7 / DR-8, #151). This is the in-memory half that
/// <c>WorkflowDefinitionProjection</c> projects to the wire
/// <see cref="Strategos.Contracts.Generated.PermittedForkTrigger"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the DECLARATION half: field-NAME declarations, never runtime VALUES —
/// those exist only when a fork actually happens (the occurrence side,
/// <c>ForkOccurrence</c> / <c>ForkEvidence</c>, in the Events family). Every entry is
/// a plain string moniker (INV-8): an evidence field name, never a CLR type or a
/// runtime payload. The <see cref="Trigger"/> is the shared closed enum, a snake_case
/// wire vocabulary — not a CLR type.
/// </para>
/// </remarks>
public sealed record PermittedForkTriggerDefinition
{
    /// <summary>
    /// Gets the closed trigger this entry permits (DR-8 — the shared trigger identity).
    /// </summary>
    public required ForkTrigger Trigger { get; init; }

    /// <summary>
    /// Gets the evidence FIELD NAMES a future fork occurrence must carry for this
    /// trigger (declaration side — NOT runtime values). Each is a plain string moniker
    /// (INV-8). At least one field is present: a permitted trigger that names no
    /// evidence declares no justification schema.
    /// </summary>
    public IReadOnlyList<string> RequiredEvidenceFields { get; init; } = [];

    /// <summary>
    /// Creates a permitted-fork-trigger definition, validating that at least one
    /// non-empty evidence field name is declared.
    /// </summary>
    /// <param name="trigger">The closed trigger this entry permits.</param>
    /// <param name="requiredEvidenceFields">
    /// The evidence field names the trigger's occurrences must carry (at least one,
    /// each non-empty).
    /// </param>
    /// <returns>A validated permitted-fork-trigger definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredEvidenceFields"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="requiredEvidenceFields"/> is empty or contains a
    /// null/whitespace field name.
    /// </exception>
    public static PermittedForkTriggerDefinition Create(
        ForkTrigger trigger,
        IReadOnlyList<string> requiredEvidenceFields)
    {
        ArgumentNullException.ThrowIfNull(requiredEvidenceFields, nameof(requiredEvidenceFields));

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

        return new PermittedForkTriggerDefinition
        {
            Trigger = trigger,
            RequiredEvidenceFields = [.. requiredEvidenceFields],
        };
    }
}
