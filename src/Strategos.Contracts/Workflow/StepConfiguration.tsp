import "@typespec/json-schema";
import "./StepDefinition.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Wire-IR retry configuration (mirrors the builder's `RetryConfiguration`).
 * Durations are serialized as ISO-8601 duration strings (language-neutral; not
 * a .NET `TimeSpan`).
 */
@jsonSchema
model RetryConfiguration {
  /** Maximum retry attempts. */
  maxAttempts: int32;

  /** Initial backoff delay (ISO-8601 duration). */
  initialDelay?: string;

  /** Exponential backoff multiplier. */
  backoffMultiplier?: float64;

  /** Maximum backoff delay (ISO-8601 duration). */
  maxDelay?: string;

  /** Whether to apply jitter to backoff. */
  useJitter?: boolean;
}

/**
 * Wire-IR compensation configuration (mirrors the builder's
 * `CompensationConfiguration`). The compensation step CLR type reduces to a
 * simple-name moniker (LB-2).
 */
@jsonSchema
model CompensationConfiguration {
  /** Simple-name CLR moniker of the compensation step type (LB-2). */
  compensationStepType: string;

  /** Whether compensation is required on failure. */
  requiredOnFailure?: boolean;

  /** Compensation timeout (ISO-8601 duration), if set. */
  timeout?: string;
}

/**
 * Wire-IR validation definition (mirrors the builder's `ValidationDefinition`).
 * `predicateExpression` is a **declarative description** of the predicate, not
 * executable code (LB-1) — it is the same string the builder stores for
 * visualization / diagnostics.
 */
@jsonSchema
model ValidationDefinition {
  /** Declarative predicate description (not executable code, LB-1). */
  predicateExpression: string;

  /** Error message surfaced when validation fails. */
  errorMessage: string;
}

/**
 * Wire-IR low-confidence handler (mirrors the builder's
 * `LowConfidenceHandlerDefinition`).
 */
@jsonSchema
model LowConfidenceHandlerDefinition {
  /** Stable handler identifier. */
  handlerId: string;

  /** Steps run when confidence is below threshold. */
  handlerSteps: StepDefinition[];

  /** Whether the handler terminates the workflow. */
  isTerminal: boolean;

  /** Step id where the handler rejoins the main flow, if any. */
  rejoinStepId?: string;
}

/**
 * Wire-IR step configuration tree (mirrors the builder's
 * `StepConfigurationDefinition`). All members are optional — an unconfigured
 * step omits the whole object.
 */
@jsonSchema
model StepConfigurationDefinition {
  /** Confidence threshold below which the low-confidence handler fires. */
  confidenceThreshold?: float64;

  /** Low-confidence handler. */
  onLowConfidence?: LowConfidenceHandlerDefinition;

  /** Compensation configuration. */
  compensation?: CompensationConfiguration;

  /** Retry configuration. */
  retry?: RetryConfiguration;

  /** Step timeout (ISO-8601 duration), if set. */
  timeout?: string;

  /** Validation guard. */
  validation?: ValidationDefinition;
}
