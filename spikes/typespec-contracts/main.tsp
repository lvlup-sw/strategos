import "@typespec/json-schema";

using JsonSchema;

/**
 * LevelUp SDLC Event Contracts
 *
 * Canonical schema definitions for workflow events shared between
 * Exarchos (local harness) and Basileus (agent host platform).
 *
 * These TypeSpec models emit:
 *   - JSON Schema (consumed by both products)
 *   - C# records (via Strategos.Contracts NuGet, consumed by Basileus)
 *   - Zod schemas (via json-schema-to-zod, consumed by Exarchos)
 */
@jsonSchema
namespace LevelUp.Sdlc.Contracts;

// ─── Enums ─────────────────────────────────────────────────────────────────

/** Source system that emitted the event. */
enum EventSource {
  exarchos,
  basileus,
}

/** Workflow types supported by the SDLC pipeline. */
enum WorkflowType {
  feature,
  debug,
  refactor,
  oneshot,
  hotfix,
  discovery,
}

/** TDD cycle phase for task progress tracking. */
enum TddPhase {
  red,
  green,
  refactor,
}

/** Review verdict outcomes. */
enum ReviewVerdict {
  pass,
  fail,
  blocked,
}

/** Review finding severity levels. */
enum FindingSeverity {
  critical,
  major,
  minor,
  suggestion,
}

/** Compensation action status. */
enum CompensationStatus {
  executed,
  skipped,
  failed,
  `dry-run`,
}

// ─── Base Event Envelope ───────────────────────────────────────────────────

/**
 * Base envelope for all SDLC workflow events.
 *
 * Both Exarchos (JSONL files) and Basileus (Marten streams)
 * use this envelope. The `type` field discriminates the `data` payload.
 */
model SdlcEventEnvelope {
  /** Stream identifier for event routing (typically the feature/workflow ID). */
  @minLength(1)
  @maxLength(100)
  streamId: string;

  /** Monotonically increasing sequence number within the stream. */
  @minValue(1)
  sequence: int64;

  /** ISO 8601 UTC timestamp when the event occurred. */
  timestamp: utcDateTime;

  /** Event type discriminator (dot-separated, lowercase). */
  @minLength(1)
  type: string;

  /** Correlation ID linking related events across workflows. */
  @maxLength(200)
  correlationId?: string;

  /** Causation ID for event chain tracking. */
  @maxLength(200)
  causationId?: string;

  /** Agent identifier that produced this event. */
  @maxLength(200)
  agentId?: string;

  /** Agent role (e.g., "orchestrator", "coder", "reviewer"). */
  @maxLength(50)
  agentRole?: string;

  /** Source system that emitted this event. */
  source?: EventSource;

  /** Schema version for forward compatibility. */
  @maxLength(20)
  schemaVersion?: string;

  /** Event-specific data payload. */
  data?: Record<unknown>;
}

// ─── Workflow Events ───────────────────────────────────────────────────────

/** Data payload for `workflow.started` events. */
model WorkflowStartedData {
  featureId: string;
  workflowType: WorkflowType;
  designPath?: string;
  synthesisPolicy?: "always" | "never" | "on-request";
}

/** Data payload for `workflow.transition` events. */
model PhaseTransitionData {
  from: string;
  to: string;
  trigger: string;
  featureId: string;
}

/** Data payload for `workflow.checkpoint` events. */
model CheckpointData {
  counter: int32;
  phase: string;
  featureId: string;
}

/** Data payload for `workflow.cancel` events. */
model WorkflowCancelData {
  from: string;
  to: string;
  trigger: string;
  featureId: string;
  reason?: string;
}

/** Data payload for `workflow.compensation` events. */
model CompensationData {
  featureId: string;
  actionId: string;
  status: CompensationStatus;
  message: string;
}

// ─── Task Events ───────────────────────────────────────────────────────────

/** Data payload for `task.assigned` events. */
model TaskAssignedData {
  taskId: string;
  title: string;
  branch?: string;
  worktree?: string;
  assignee?: string;
}

/** Data payload for `task.completed` events. */
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

/** Test evidence attached to task completion. */
model TestEvidence {
  type: "test" | "build" | "typecheck" | "manual";
  output: string;
  passed: boolean;
}

/** Reference to a test file. */
model TestRef {
  name: string;
  file: string;
}

/** Data payload for `task.failed` events. */
model TaskFailedData {
  taskId: string;
  @maxLength(500)
  error: string;
  diagnostics?: Record<unknown>;
}

/** Data payload for `task.progressed` events. */
model TaskProgressedData {
  taskId: string;
  tddPhase: TddPhase;
  @maxLength(500)
  detail?: string;
}

// ─── Quality Gate Events ───────────────────────────────────────────────────

/** Data payload for `gate.executed` events. */
model GateExecutedData {
  gateName: string;
  layer: string;
  passed: boolean;
  duration?: float64;
  details?: Record<unknown>;
}

// ─── Team Events ───────────────────────────────────────────────────────────

/** Information about a teammate in an SDLC workflow team. */
model TeammateInfo {
  name: string;
  role: string;
  @encodedName("application/json", "model")
  llmModel?: string;
}

/** Data payload for `team.spawned` events. */
model TeamSpawnedData {
  teamSize: int32;
  teammateNames: string[];
  taskCount: int32;
  dispatchMode: string;
}

/** Data payload for `team.task.completed` events. */
model TeamTaskCompletedData {
  taskId: string;
  teammateName: string;
  durationMs: float64;
  filesChanged: string[];
  testsPassed: boolean;
  qualityGateResults: Record<unknown>;
}

// ─── Review Events ─────────────────────────────────────────────────────────

/** Data payload for `review.completed` events. */
model ReviewCompletedData {
  stage: "spec-review" | "quality-review" | "security-review";
  verdict: ReviewVerdict;
  findingsCount: int32;
  summary: string;
}

/** Data payload for `review.finding` events. */
model ReviewFindingData {
  pr: int32;
  source: "coderabbit" | "self-hosted";
  severity: FindingSeverity;
  filePath: string;
  lineRange?: int32[];
  message: string;
  rule?: string;
}

// ─── Shepherd Events ───────────────────────────────────────────────────────

/** Data payload for `shepherd.iteration` events. */
model ShepherdIterationData {
  iteration: int32;
  prsAssessed: int32;
  fixesApplied: int32;
  status: string;
}
