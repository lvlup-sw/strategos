import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The polyglot STRING identity of one ontology record (DR-16): a descriptor NAME
 * (INV-8 — never a CLR type) paired with the projected record id. Citations and
 * nearest-record monikers cross the wire as language-neutral string pairs a
 * non-.NET consumer (Exarchos, Basileus) can read without a shared type system.
 * The wire twin of the Strategos.Ontology.MCP `RecordRef` — same string pair, so
 * the two serialize to one shape.
 */
model RecordRef {
  /** Descriptor name of the record's object type (INV-8: a name, never a CLR type). */
  descriptor: string;

  /** Projected id of the record instance. */
  recordId: string;
}

/**
 * The `answerKind: "answer"` arm of `AbstentionResponse` (DR-16): a cited answer.
 * `citations` is NON-EMPTY (`@minItems(1)`) — a free-text uncited answer is
 * unrepresentable, so an answer that cites nothing fails schema validation. Each
 * citation is a polyglot `RecordRef` moniker (INV-8), never a CLR type. The wire
 * twin of the Strategos.Ontology.MCP `Answer`.
 */
model Answer {
  /** Discriminator — pins this arm to the `answer` branch of the union. */
  answerKind: "answer";

  /** The answer text. */
  content: string;

  /** The supporting records — NON-EMPTY (`@minItems(1)`). Each is a polyglot RecordRef moniker (INV-8), never a CLR type. */
  @minItems(1)
  citations: RecordRef[];
}

/**
 * The `answerKind: "noAnswerRecorded"` arm of `AbstentionResponse` (DR-16): a
 * recorded abstention. `nearestRecords` surfaces the closest non-matching records
 * (may be empty) so the caller sees WHAT was searched — never a silent null. Each
 * is a polyglot `RecordRef` moniker (INV-8), never a CLR type. The wire twin of
 * the Strategos.Ontology.MCP `NoAnswerRecorded`.
 */
model NoAnswerRecorded {
  /** Discriminator — pins this arm to the `noAnswerRecorded` branch of the union. */
  answerKind: "noAnswerRecorded";

  /** The nearest non-matching records (may be empty). Each is a polyglot RecordRef moniker (INV-8), never a CLR type. */
  nearestRecords: RecordRef[];
}

/**
 * Closed response union for an ontology answering surface (DR-16): either a cited
 * `Answer` or a recorded abstention (`NoAnswerRecorded`). Consumers dispatch on
 * the `answerKind` discriminator to pick the arm.
 *
 * The discriminated-union emission path lowers this to a `[JsonPolymorphic]`
 * abstract record carrying one `[JsonDerivedType]` per arm — the mechanical wire
 * twin of the Strategos.Ontology.MCP `OntologyAnswerUnion` (same `answerKind`
 * discriminator, same `answer` / `noAnswerRecorded` arms, same RecordRef shape),
 * so the C# union serializes to exactly this schema.
 *
 * Because the union is closed over exactly these two arms and the `answer` arm
 * pins `@minItems(1)` on its citations, a free-text uncited answer is
 * unrepresentable on the wire — the shape decides Answer vs NoAnswerRecorded by
 * construction, never a silent null.
 */
@jsonSchema
union AbstentionResponse {
  answer: Answer,
  noAnswerRecorded: NoAnswerRecorded,
}
