using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// The <c>answerKind: "noAnswerRecorded"</c> branch of <see cref="OntologyAnswerUnion"/>
/// (DR-11): a recorded abstention. The composer emits this when retrieval matched
/// nothing to cite; <see cref="NearestRecords"/> surfaces the closest non-matching
/// records (may be empty) so the caller sees WHAT was searched — never a silent null.
/// Produced ONLY by <see cref="OntologyAnswerComposer"/>.
/// </summary>
public sealed record NoAnswerRecorded : OntologyAnswerUnion
{
    /// <summary>
    /// Constructs a recorded abstention. INTERNAL so the composer is the sole producer.
    /// </summary>
    internal NoAnswerRecorded(IReadOnlyList<RecordRef> nearestRecords, ResponseMeta meta)
    {
        ArgumentNullException.ThrowIfNull(nearestRecords);
        ArgumentNullException.ThrowIfNull(meta);

        NearestRecords = nearestRecords;
        Meta = meta;
    }

    /// <summary>
    /// The nearest non-matching records (may be empty). Each is a polyglot
    /// <see cref="RecordRef"/> moniker (INV-8), never a CLR type.
    /// </summary>
    [JsonPropertyName("nearestRecords")]
    public IReadOnlyList<RecordRef> NearestRecords { get; init; }

    /// <summary>INV-3: the per-response <c>_meta</c> envelope.</summary>
    [JsonPropertyName("_meta")]
    public ResponseMeta Meta { get; init; }
}
