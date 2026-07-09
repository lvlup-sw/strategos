using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// The <c>answerKind: "answer"</c> branch of <see cref="OntologyAnswerUnion"/>
/// (DR-11): a cited answer. <see cref="Citations"/> is NON-EMPTY by construction —
/// the internal constructor guards it (a free-text uncited answer throws) and the
/// advertised output schema pins <c>minItems: 1</c>. Produced ONLY by
/// <see cref="OntologyAnswerComposer"/>.
/// </summary>
public sealed record Answer : OntologyAnswerUnion
{
    /// <summary>
    /// Constructs a cited answer. INTERNAL so the composer is the sole producer; the
    /// guard clause makes a free-text uncited answer unrepresentable.
    /// </summary>
    /// <exception cref="System.ArgumentException"><paramref name="citations"/> is empty.</exception>
    internal Answer(string content, IReadOnlyList<RecordRef> citations, ResponseMeta meta)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(meta);
        if (citations.Count == 0)
        {
            throw new ArgumentException(
                "An Answer must cite at least one record; a free-text uncited answer is unrepresentable (DR-11).",
                nameof(citations));
        }

        Content = content;
        Citations = citations;
        Meta = meta;
    }

    /// <summary>The answer text.</summary>
    [JsonPropertyName("content")]
    public string Content { get; init; }

    /// <summary>
    /// The supporting records (NON-EMPTY). Each is a polyglot <see cref="RecordRef"/>
    /// moniker (INV-8), never a CLR type.
    /// </summary>
    [JsonPropertyName("citations")]
    public IReadOnlyList<RecordRef> Citations { get; init; }

    /// <summary>INV-3: the per-response <c>_meta</c> envelope.</summary>
    [JsonPropertyName("_meta")]
    public ResponseMeta Meta { get; init; }
}
