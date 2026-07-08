using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Closed response union for an ontology answering surface (DR-11): either a cited
/// <see cref="Answer"/> or a recorded abstention (<see cref="NoAnswerRecorded"/>).
/// MCP clients dispatch on the <c>answerKind</c> discriminator to pick the branch,
/// mechanically mirroring the <see cref="QueryResultUnion"/> <c>[JsonPolymorphic]</c>
/// pattern.
/// </summary>
/// <remarks>
/// The leaf constructors are INTERNAL: the ONLY producer is
/// <see cref="OntologyAnswerComposer"/>. Because the union cannot be constructed
/// outside that chokepoint, a free-text uncited answer is unrepresentable — the
/// retrieval layer decides <see cref="Answer"/> vs <see cref="NoAnswerRecorded"/> by
/// construction, never a silent null.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "answerKind")]
[JsonDerivedType(typeof(Answer), "answer")]
[JsonDerivedType(typeof(NoAnswerRecorded), "no_answer_recorded")]
public abstract record OntologyAnswerUnion;
