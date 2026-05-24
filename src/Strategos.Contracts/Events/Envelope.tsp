import "@typespec/json-schema";
import "./Enums.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Base envelope for all SDLC workflow events.
 *
 * Both Exarchos (JSONL files) and Basileus (Marten streams) use this envelope.
 * The `type` field is a free-form dot-separated discriminator and the `data`
 * payload is an open object: an unknown `type` is **representable and logged,
 * never rejected** (forward compatibility — a consumer that does not recognise
 * a type retains the raw envelope rather than dropping the event).
 */
@jsonSchema
model SdlcEventEnvelope {
  /** Stream identifier for event routing (typically the feature/workflow ID). */
  @minLength(1)
  @maxLength(100)
  streamId: string;

  /** Monotonically increasing sequence number within the stream (int32, not int64). */
  @minValue(0)
  sequence: int32;

  /** ISO 8601 UTC timestamp when the event occurred. */
  timestamp: utcDateTime;

  /** Event type discriminator (dot-separated, lowercase). Unknown values are logged, not rejected. */
  @minLength(1)
  type: string;

  /** Correlation ID linking related events across workflows. */
  @maxLength(200)
  correlationId?: string;

  /** Causation ID for event-chain tracking. */
  @maxLength(200)
  causationId?: string;

  /** Agent identifier that produced this event. */
  @maxLength(200)
  agentId?: string;

  /** Agent role (e.g. "orchestrator", "coder", "reviewer"). */
  @maxLength(50)
  agentRole?: string;

  /** Source system that emitted this event. */
  source?: EventSource;

  /** Schema version for forward compatibility. */
  @maxLength(20)
  schemaVersion?: string;

  /** Event-specific data payload — open object so unknown event shapes round-trip. */
  data?: Record<unknown>;
}
