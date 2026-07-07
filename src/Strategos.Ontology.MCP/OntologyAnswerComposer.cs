using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Strategos.Ontology.MCP.Internal;

namespace Strategos.Ontology.MCP;

/// <summary>
/// The SOLE producer of the <see cref="OntologyAnswerUnion"/> (DR-11). Strategos's
/// existing tools return result SETS, not answers; the composer is the primitive any
/// answering surface (a future Strategos answer tool, or a host embedding the ontology
/// layer) builds on.
/// </summary>
/// <remarks>
/// Because the union's leaf constructors are internal, this is the ONLY place an
/// <see cref="Answer"/> or <see cref="NoAnswerRecorded"/> can be constructed — the
/// mechanical chokepoint that makes a free-text uncited answer unrepresentable.
/// </remarks>
public sealed class OntologyAnswerComposer
{
    /// <summary>
    /// Decides <see cref="Answer"/> vs <see cref="NoAnswerRecorded"/> from a retrieval
    /// outcome. The null is decided by the RETRIEVAL layer by construction:
    /// <paramref name="matchedRecords"/> is what retrieval found to support the answer.
    /// When it is empty the composer abstains, surfacing <paramref name="nearestRecords"/>
    /// (what WAS searched) — it never returns a <see cref="NoAnswerRecorded"/> while
    /// matched results exist. When it is non-empty the composer produces a cited
    /// <see cref="Answer"/>; the guard clause in <see cref="Answer"/>'s constructor
    /// refuses an answer with empty citations, so no code path yields a free-text
    /// uncited answer.
    /// </summary>
    /// <param name="content">The answer text (used only when matched records exist).</param>
    /// <param name="matchedRecords">Records retrieval found to support the answer; empty ⇒ abstain.</param>
    /// <param name="nearestRecords">Closest non-matching records to surface on abstention.</param>
    /// <param name="meta">INV-3 <c>_meta</c> envelope stamped onto whichever branch is produced.</param>
    public OntologyAnswerUnion Compose(
        string content,
        IReadOnlyList<RecordRef> matchedRecords,
        IReadOnlyList<RecordRef> nearestRecords,
        ResponseMeta meta)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(matchedRecords);
        ArgumentNullException.ThrowIfNull(nearestRecords);
        ArgumentNullException.ThrowIfNull(meta);

        // Retrieval decided the null: nothing matched ⇒ abstain, surfacing the nearest
        // records so the caller sees what was searched (never a hidden result set).
        if (matchedRecords.Count == 0)
        {
            return new NoAnswerRecorded(nearestRecords, meta);
        }

        // Matched records present ⇒ cite them. The Answer constructor re-asserts the
        // non-empty invariant — a free-text uncited answer is unrepresentable.
        return new Answer(content, matchedRecords, meta);
    }

    /// <summary>
    /// The JSON Schema an answering surface advertises for the union. The
    /// <see cref="Answer"/> branch's <c>citations</c> array carries <c>minItems: 1</c>,
    /// advertising the non-empty guarantee the composer enforces at runtime;
    /// <see cref="NoAnswerRecorded"/>'s <c>nearestRecords</c> stays unconstrained.
    /// </summary>
    [RequiresUnreferencedCode("Schema generation reflects over the union types; not safe under trimming.")]
    [RequiresDynamicCode("Schema generation may require runtime code generation.")]
    public static JsonElement AdvertisedOutputSchema() =>
        JsonSchemaHelper.JsonSchemaForAnswerUnion();
}
