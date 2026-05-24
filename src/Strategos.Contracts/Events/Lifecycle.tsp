import "@typespec/json-schema";
import "./Enums.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

// ─── Coding-attempt lifecycle ────────────────────────────────────────────────

/** Data payload for `CodingAttemptStarted` events — a container began a TDD attempt. */
@jsonSchema
model CodingAttemptStartedData {
  taskId: string;
  attemptNumber: int32;
  containerId: string;
}

/** Outcome of a coding attempt. */
@jsonSchema
enum CodingAttemptOutcome {
  success,
  tests_failed,
  budget_exhausted,
  loop_detected,
}

/** Aggregate test counts for a coding attempt. */
@jsonSchema
model TestResultSummary {
  passed: int32;
  failed: int32;
  coverage?: float64;
}

/** Data payload for `CodingAttemptCompleted` events. */
@jsonSchema
model CodingAttemptCompletedData {
  taskId: string;
  attemptNumber: int32;
  outcome: CodingAttemptOutcome;
  testResults?: TestResultSummary;
}

/** Data payload for `TaskProgressed` events — a TDD phase transition. */
@jsonSchema
model TaskProgressedData {
  taskId: string;
  tddPhase: TddPhase;
  @maxLength(500)
  detail?: string;
}

/** Data payload for `TestResult` events — raw test-run counts. */
@jsonSchema
model TaskTestResultData {
  taskId: string;
  passed: int32;
  failed: int32;
  coverage?: float64;
}

/** Data payload for `ContainerDestroyed` events — a coding container was torn down. */
@jsonSchema
model ContainerDestroyedData {
  taskId: string;
  containerId: string;
  totalDuration: float64;
  totalTokens: int32;
}

// ─── Task lifecycle ──────────────────────────────────────────────────────────

/** Reference to a test file produced by a task. */
@jsonSchema
model TestRef {
  name: string;
  file: string;
}

/** Test evidence attached to task completion. */
@jsonSchema
model TestEvidence {
  type: "test" | "build" | "typecheck" | "manual";
  output: string;
  passed: boolean;
}

/** Data payload for `task.completed` events. */
@jsonSchema
model TaskCompletedData {
  taskId: string;
  acceptanceTestRef?: string;
  artifacts?: string[];
  duration?: float64;
  evidence?: TestEvidence;
  verified?: boolean;
  implements?: string[];
  tests?: TestRef[];
  files?: string[];
}

/** Data payload for `task.failed` events. */
@jsonSchema
model TaskFailedData {
  taskId: string;
  @maxLength(500)
  error: string;
  diagnostics?: Record<unknown>;
}

/** Data payload for `task.escalation` events — remediation exhausted, hand-off requested. */
@jsonSchema
model TaskEscalationData {
  taskId: string;
  escalationTarget: "exarchos" | "human";
  suggestedAction: "local-teammate" | "human-guidance" | "re-dispatch";
  @maxLength(500)
  reason?: string;
}
