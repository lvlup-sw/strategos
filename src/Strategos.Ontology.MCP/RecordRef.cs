using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// The polyglot STRING identity of one ontology record (DR-11): a descriptor NAME
/// (never a CLR type — INV-8) paired with the projected record id. Modeled on
/// <see cref="TraversalEndpoint"/>'s string-pair identity so citations and nearest
/// records cross the wire as language-neutral monikers a non-.NET client
/// (basileus, exarchos) can consume without a shared type system.
/// </summary>
/// <param name="Descriptor">Descriptor name of the record's object type (INV-8: a name, never a CLR <see cref="System.Type"/>).</param>
/// <param name="RecordId">Projected id of the record instance.</param>
public sealed record RecordRef(
    [property: JsonPropertyName("descriptor")] string Descriptor,
    [property: JsonPropertyName("recordId")] string RecordId);
