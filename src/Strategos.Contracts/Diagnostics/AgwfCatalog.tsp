import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * AGWF — the workflow source-generator diagnostic codes. **Single canonical
 * source** (#52): the codes and their metadata live here in TypeSpec; the
 * Roslyn analyzer (`WorkflowDiagnostics`) sources its `id` strings from the
 * generated `AgwfCode` C# enum, and the codegen pipeline emits the canonical
 * `agwf-catalog.json` data artifact + `docs/diagnostics/agwf.md` reference.
 *
 * Ground truth = exactly **15 defined codes** with gaps preserved as gaps
 * (INV-5: never renumber). Member names are the contract Exarchos's TypeScript
 * enum round-trips against *by name* (not ordinal).
 *
 * Representation = **R-lit** (DR-1): each entry is a literal-typed model whose
 * fields emit as JSON Schema `const`, read by `AgwfCatalogEmitter`. Adding a
 * code is a minor bump; renaming/removing one is a major bump.
 */
@jsonSchema
enum AgwfCode {
  EmptyWorkflowName: "AGWF001",
  NoStepsFound: "AGWF002",
  DuplicateStepName: "AGWF003",
  InvalidNamespace: "AGWF004",
  MissingStartWith: "AGWF009",
  MissingFinally: "AGWF010",
  ForkWithoutJoin: "AGWF012",
  LoopWithoutBody: "AGWF014",
  InvalidPersistenceMode: "AGWF015",
  EventSourcedRequiresState: "AGWF016",
  CompensateNotAStep: "AGWF017",
  ConfidenceThresholdOutOfRange: "AGWF018",
  RequireConfidenceWithoutHandler: "AGWF019",
  RetryMaxAttemptsBelowOne: "AGWF020",
  NonPositiveTimeout: "AGWF021",
}

/** AGWF001 — Empty workflow name. */
@jsonSchema
model AgwfEntryEmptyWorkflowName {
  id: "AGWF001";
  severity: "error";
  summary: "Empty workflow name";
  remediation: "Workflow name cannot be empty or whitespace";
  since: "0.2.0";
}

/** AGWF002 — No workflow steps found. */
@jsonSchema
model AgwfEntryNoStepsFound {
  id: "AGWF002";
  severity: "warning";
  summary: "No workflow steps found";
  remediation: "Could not find any steps in workflow '{0}'. Ensure the workflow uses StartWith<T>(), Then<T>(), and Finally<T>() methods.";
  since: "0.2.0";
}

/** AGWF003 — Duplicate step name. */
@jsonSchema
model AgwfEntryDuplicateStepName {
  id: "AGWF003";
  severity: "error";
  summary: "Duplicate step name";
  remediation: "Step '{0}' appears multiple times in workflow '{1}'. Each step type should be unique.";
  since: "0.2.0";
}

/** AGWF004 — Invalid namespace. */
@jsonSchema
model AgwfEntryInvalidNamespace {
  id: "AGWF004";
  severity: "error";
  summary: "Invalid namespace";
  remediation: "Workflow '{0}' must be declared in a namespace. Global namespace is not supported.";
  since: "0.2.0";
}

/** AGWF009 — Missing StartWith. */
@jsonSchema
model AgwfEntryMissingStartWith {
  id: "AGWF009";
  severity: "error";
  summary: "Missing StartWith";
  remediation: "Workflow '{0}' must begin with StartWith<T>(). Found '{1}' instead.";
  since: "0.2.0";
}

/** AGWF010 — Missing Finally. */
@jsonSchema
model AgwfEntryMissingFinally {
  id: "AGWF010";
  severity: "warning";
  summary: "Missing Finally";
  remediation: "Workflow '{0}' does not end with Finally<T>(). Consider adding a Finally step to mark workflow completion.";
  since: "0.2.0";
}

/** AGWF012 — Fork without Join. */
@jsonSchema
model AgwfEntryForkWithoutJoin {
  id: "AGWF012";
  severity: "error";
  summary: "Fork without Join";
  remediation: "Workflow '{0}' has a Fork that is not followed by Join. Every Fork must be closed with a Join<T>() call.";
  since: "0.2.0";
}

/** AGWF014 — Loop without body. */
@jsonSchema
model AgwfEntryLoopWithoutBody {
  id: "AGWF014";
  severity: "error";
  summary: "Loop without body";
  remediation: "Loop '{0}' in workflow '{1}' has no steps in its body. A loop must contain at least one step.";
  since: "0.2.0";
}

/** AGWF015 — Invalid persistence mode. */
@jsonSchema
model AgwfEntryInvalidPersistenceMode {
  id: "AGWF015";
  severity: "error";
  summary: "Invalid persistence mode";
  remediation: "Workflow '{0}' specifies an unrecognized Persistence value ({1}). Valid values are SagaDocument (0) and EventSourced (1).";
  since: "0.2.0";
}

/** AGWF016 — Event-sourced workflow requires state type. */
@jsonSchema
model AgwfEntryEventSourcedRequiresState {
  id: "AGWF016";
  severity: "error";
  summary: "Event-sourced workflow requires state type";
  remediation: "Workflow '{0}' uses PersistenceMode.EventSourced but no state type was found. Event-sourced workflows require a state type that implements IEventSourcedState<TState> with an ApplyEvent method.";
  since: "0.2.0";
}

/** AGWF017 — Compensation step is not a workflow step. */
@jsonSchema
model AgwfEntryCompensateNotAStep {
  id: "AGWF017";
  severity: "error";
  summary: "Compensation step is not a workflow step";
  remediation: "Step '{0}' in workflow '{1}' compensates with '{2}', which does not implement IWorkflowStep<TState>. Compensation types must be a registered workflow step.";
  since: "2.10.0";
}

/** AGWF018 — Confidence threshold out of range. */
@jsonSchema
model AgwfEntryConfidenceThresholdOutOfRange {
  id: "AGWF018";
  severity: "error";
  summary: "Confidence threshold out of range";
  remediation: "Step '{0}' in workflow '{1}' calls RequireConfidence({2}). The threshold must be between 0.0 and 1.0 inclusive.";
  since: "2.10.0";
}

/** AGWF019 — RequireConfidence without OnLowConfidence handler. */
@jsonSchema
model AgwfEntryRequireConfidenceWithoutHandler {
  id: "AGWF019";
  severity: "warning";
  summary: "RequireConfidence without OnLowConfidence handler";
  remediation: "Step '{0}' in workflow '{1}' calls RequireConfidence but declares no OnLowConfidence handler. Add OnLowConfidence(alt => ...) so low-confidence results have a routing path.";
  since: "2.10.0";
}

/** AGWF020 — Retry maxAttempts below one. */
@jsonSchema
model AgwfEntryRetryMaxAttemptsBelowOne {
  id: "AGWF020";
  severity: "error";
  summary: "Retry maxAttempts below one";
  remediation: "Step '{0}' in workflow '{1}' configures WithRetry({2}). The maxAttempts value must be at least 1.";
  since: "2.10.0";
}

/** AGWF021 — Non-positive timeout. */
@jsonSchema
model AgwfEntryNonPositiveTimeout {
  id: "AGWF021";
  severity: "error";
  summary: "Non-positive timeout";
  remediation: "Step '{0}' in workflow '{1}' configures WithTimeout with a non-positive duration. The timeout must be greater than zero.";
  since: "2.10.0";
}
