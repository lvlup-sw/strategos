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
 *
 * Presence of this block is the "stamped from telemetry" state. That is
 * distinct from GateDeclaration omitting `reliability` (never stamped). A
 * stamped block MAY report `sampleSize: 0` — measured, zero observations —
 * so `@minValue(0)` is the correct floor (#156.1). Tightening to
 * `@minValue(1)` would collapse those two states and is a constraint-narrow
 * on the shipped 0.7.0 contract; do not re-open it.
 */
@jsonSchema
model GateReliability {
  /** Measured false-positive rate of the gate, a fraction in the closed unit interval. */
  @minValue(0)
  @maxValue(1)
  fpr: float32;

  /**
   * Number of observations the measurement was computed over. Zero is valid:
   * the block is present (stamped) but no observations have been recorded yet.
   * Omitting the reliability block on GateDeclaration is the "never stamped"
   * state; do not treat `sampleSize: 0` as equivalent to absence.
   * Keep `@minValue(0)` (#156.1).
   */
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
 * authoring channel may supply it. Presence of the block is not the same as
 * `sampleSize ≥ 1`: a stamped block may report zero observations (see
 * GateReliability). When present, its `source` provenance is mandatory.
 */
@jsonSchema
model GateDeclaration {
  /** The typed gate class this declaration evaluates (DR-1 — the shared gate identity). */
  class: GateClass;

  /** Stable identifier for this gate declaration. */
  id: string;

  /**
   * Measured-reliability telemetry — present ONLY when stamped from telemetry;
   * never hand-authored. A present block with `sampleSize: 0` is "measured,
   * zero observations," not "unmeasured."
   */
  reliability?: GateReliability;
}
