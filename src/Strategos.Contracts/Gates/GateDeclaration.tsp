import "@typespec/json-schema";
import "./GateClass.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Measured-reliability telemetry for a gate (#150, DR-2).
 *
 * This block is NEVER hand-authored: it is populated only from measured gate
 * telemetry, so `source` — the PROVENANCE of the measurement — is REQUIRED. A
 * reliability block that omits `source` is rejected at the schema boundary
 * (there is no anonymous, unattributable reliability number). The authoring
 * channel (the builder DSL / import) surfaces no reliability field at all, so
 * nothing on the write path can smuggle a value in; reliability is stamped only
 * from telemetry.
 */
@jsonSchema
model GateReliability {
  /** Measured false-positive rate of the gate, a fraction in the closed unit interval. */
  @minValue(0)
  @maxValue(1)
  fpr: float32;

  /** Number of observations the measurement was computed over. */
  @minValue(0)
  sampleSize: int32;

  /** When the measurement was taken (UTC). */
  asOf: utcDateTime;

  /**
   * Provenance of the measurement — where this reliability number came from.
   * REQUIRED: reliability is telemetry-measured, never hand-authored, so a
   * reliability block without a source is invalid.
   */
  source: string;
}

/**
 * A single gate declaration on a workflow (#150, DR-2): the typed gate CLASS it
 * evaluates, a stable id, and — ONLY when measured — its reliability telemetry.
 *
 * `class` is the single typed gate identity (DR-1, GateClass) both runtimes
 * consume; it is non-optional. `reliability` is optional because it is stamped
 * from telemetry after the fact — a freshly-declared gate carries none, and no
 * authoring channel may supply it. When present, its `source` provenance is
 * mandatory (see GateReliability).
 */
@jsonSchema
model GateDeclaration {
  /** The typed gate class this declaration evaluates (DR-1 — the shared gate identity). */
  class: GateClass;

  /** Stable identifier for this gate declaration. */
  id: string;

  /** Measured-reliability telemetry — present ONLY when measured; never hand-authored. */
  reliability?: GateReliability;
}
