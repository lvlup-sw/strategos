import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * `ontology.abstained` audit event (DR-17) — the occurrence-side sibling of the
 * `NoAnswerRecorded` arm of AbstentionResponse (DR-16). Recorded whenever the
 * ontology answering surface abstains: retrieval matched nothing to cite, so the
 * surface returns a recorded abstention rather than a free-text uncited answer.
 *
 * Envelope-compatible: `type` pins the SdlcEventEnvelope discriminator to
 * `ontology.abstained`, so the event routes through the shared event envelope.
 *
 * The payload carries the COUNT of nearest records, never their identities or
 * contents. The abstention's RecordRef monikers stay out of the audit stream — a
 * record identity cannot be exfiltrated through audit, and the count is enough to
 * see that a search happened and how wide it reached.
 *
 * Emitted server-side by the ontology answering surface at the point it records
 * the abstention; it is never authored by a client and never round-tripped from
 * the wire into the surface.
 */
@jsonSchema
model OntologyAbstained {
  /** Envelope type discriminator — pins this event to `ontology.abstained` (satisfies SdlcEventEnvelope.type). */
  type: "ontology.abstained";

  /** How many nearest non-matching records the abstention surfaced — a COUNT, never the record identities or contents (no data exfiltration through audit). */
  @minValue(0)
  nearestRecordsCount: int32;
}
