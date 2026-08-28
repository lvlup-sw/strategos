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
 * Ground truth = exactly **32 defined codes** with gaps preserved as gaps
 * (INV-5: never renumber). Member NAMES are the wire identity: the enum
 * round-trips by name and never by ordinal, so a member may be added but never
 * renamed or reordered without a major bump.
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
  DeclaredButInert: "AGWF022",
  MalformedWorkflowJson: "AGWF023",
  UnsupportedSchemaVersion: "AGWF024",
  UnresolvableStepMoniker: "AGWF025",
  AmbiguousStepMoniker: "AGWF026",
  ImportRejectedDelegateStep: "AGWF027",
  ImportRejectedBranchPoint: "AGWF028",
  ImportRejectedLoop: "AGWF029",
  ImportRejectedValidationPredicate: "AGWF030",
  ImportRejectedApprovalContext: "AGWF031",
  ImportDanglingGateId: "AGWF032",
  ImportReliabilityBearingGate: "AGWF033",
  ImportForkTriggerWithoutEvidence: "AGWF034",
  UnreachableTermination: "AGWF035",
  PathEndTypeCollision: "AGWF036",
  DuplicatePermittedForkTrigger: "AGWF037",
  DuplicateCompensationSeed: "AGWF038",
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
  remediation: "Step '{0}' appears multiple times in workflow '{1}'. Each EffectiveName (the instance name, or the step type when none is given) must be unique.";
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

/** AGWF022 — Declared-but-inert step configuration. */
@jsonSchema
model AgwfEntryDeclaredButInert {
  id: "AGWF022";
  severity: "warning";
  summary: "Declared-but-inert step configuration";
  remediation: "Step '{0}' in workflow '{1}' declares {2}, which the generator does not lower for this step kind, so the configuration is inert. Remove it or move the step to a position where the configuration is lowered.";
  since: "2.9.0";
}

/** AGWF023 — Malformed workflow import JSON (DR-12). */
@jsonSchema
model AgwfEntryMalformedWorkflowJson {
  id: "AGWF023";
  severity: "error";
  summary: "Malformed workflow import JSON";
  remediation: "Workflow import file '{0}' is not well-formed JSON and was skipped: {1}";
  since: "2.10.0";
}

/** AGWF024 — Unsupported workflow schema version (DR-12). */
@jsonSchema
model AgwfEntryUnsupportedSchemaVersion {
  id: "AGWF024";
  severity: "error";
  summary: "Unsupported workflow schema version";
  remediation: "Workflow import file '{0}' declares schemaVersion '{1}'. Only schemaVersion '1.0' is supported.";
  since: "2.10.0";
}

/** AGWF025 — Unresolvable workflow step moniker (DR-13). */
@jsonSchema
model AgwfEntryUnresolvableStepMoniker {
  id: "AGWF025";
  severity: "error";
  summary: "Unresolvable workflow step moniker";
  remediation: "Workflow import file '{0}' references step moniker '{1}', which does not resolve to any accessible workflow step type in the compilation. Add the step type (implementing IWorkflowStep<TState>) or correct the moniker.";
  since: "2.10.0";
}

/** AGWF026 — Ambiguous workflow step moniker (DR-13). */
@jsonSchema
model AgwfEntryAmbiguousStepMoniker {
  id: "AGWF026";
  severity: "error";
  summary: "Ambiguous workflow step moniker";
  remediation: "Workflow import file '{0}' references step moniker '{1}', which resolves to more than one workflow step type: {2}. Rename all but one, or make the others inaccessible, so the moniker binds a single type.";
  since: "2.10.0";
}

/** AGWF027 — Imported delegate (lambda) step is not supported (DR-14). */
@jsonSchema
model AgwfEntryImportRejectedDelegateStep {
  id: "AGWF027";
  severity: "error";
  summary: "Imported delegate (lambda) step is not supported";
  remediation: "Workflow import file '{0}' declares a delegate (lambda) step at {1} (step '{2}'). A lambda step body is dropped on export (LB-1) and cannot be re-bound on import, so the workflow is rejected and no saga is generated. Replace it with a named IWorkflowStep<TState> step type.";
  since: "2.10.0";
}

/** AGWF028 — Imported branch point is not supported (DR-14). */
@jsonSchema
model AgwfEntryImportRejectedBranchPoint {
  id: "AGWF028";
  severity: "error";
  summary: "Imported branch point is not supported";
  remediation: "Workflow import file '{0}' declares a branch point at {1} (branch point '{2}'). A conditional branch point carries a runtime-bound condition that is not importable, so the workflow is rejected and no saga is generated.";
  since: "2.10.0";
}

/** AGWF029 — Imported loop is not supported (DR-14). */
@jsonSchema
model AgwfEntryImportRejectedLoop {
  id: "AGWF029";
  severity: "error";
  summary: "Imported loop is not supported";
  remediation: "Workflow import file '{0}' declares a loop at {1} (loop '{2}'). A RepeatUntil loop carries a runtime-bound exit condition that is not importable, so the workflow is rejected and no saga is generated.";
  since: "2.10.0";
}

/** AGWF030 — Imported validation predicate is not supported (DR-14). */
@jsonSchema
model AgwfEntryImportRejectedValidationPredicate {
  id: "AGWF030";
  severity: "error";
  summary: "Imported validation predicate is not supported";
  remediation: "Workflow import file '{0}' declares a validation predicate at {1} (step '{2}'). A declarative validation predicate carries no re-bindable executable body (LB-1), so the workflow is rejected and no saga is generated.";
  since: "2.10.0";
}

/** AGWF031 — Imported approval context is not supported (DR-14). */
@jsonSchema
model AgwfEntryImportRejectedApprovalContext {
  id: "AGWF031";
  severity: "error";
  summary: "Imported approval context is not supported";
  remediation: "Workflow import file '{0}' declares a context-bearing approval at {1} (approval '{2}'). Approval context is dropped on export (LB-1) and cannot be re-bound on import, so the workflow is rejected and no saga is generated. Use a context-free approval.";
  since: "2.10.0";
}

/** AGWF032 — Imported gate id does not resolve (DR-3). */
@jsonSchema
model AgwfEntryImportDanglingGateId {
  id: "AGWF032";
  severity: "error";
  summary: "Imported gate id does not resolve";
  remediation: "Workflow import file '{0}' references gate id '{2}' at {1}, which is not declared in the workflow's gates[]. The dangling gate reference is rejected and no saga is generated; declare the gate in gates[] or remove the gateId.";
  since: "2.10.0";
}

/** AGWF033 — Imported gate declaration carries reliability (DR-2). */
@jsonSchema
model AgwfEntryImportReliabilityBearingGate {
  id: "AGWF033";
  severity: "error";
  summary: "Imported gate declaration carries reliability";
  remediation: "Workflow import file '{0}' declares a reliability block at {1} (gate '{2}'). Gate reliability enters a definition only from measured telemetry, never from authored JSON, so the workflow is rejected and no saga is generated. Remove the reliability block.";
  since: "2.10.0";
}

/** AGWF034 — Imported fork trigger declares no required evidence fields (DR-8). */
@jsonSchema
model AgwfEntryImportForkTriggerWithoutEvidence {
  id: "AGWF034";
  severity: "error";
  summary: "Imported fork trigger declares no required evidence fields";
  remediation: "Workflow import file '{0}' declares a diagnostic-fork permitted trigger at {1} (trigger '{2}') with no required evidence fields. A permitted fork trigger must declare at least one required evidence field (wire @minItems(1)) so the DR-8 no-unjustified-fork guard has an evidence floor to enforce; the workflow is rejected and no saga is generated. Add the evidence field(s) the trigger requires.";
  since: "2.10.0";
}

/** AGWF035 — Workflow termination is unreachable (#155). */
@jsonSchema
model AgwfEntryUnreachableTermination {
  id: "AGWF035";
  severity: "error";
  summary: "Workflow termination is unreachable";
  remediation: "Workflow '{1}' has an unreachable-termination pair '{0}' / '{2}'. Either a main-flow step chains to an off-flow successor, or a rejoin last step never dispatches the declared terminal.";
  since: "2.11.0";
}

/** AGWF036 — Path-end type collision (#190). */
@jsonSchema
model AgwfEntryPathEndTypeCollision {
  id: "AGWF036";
  severity: "error";
  summary: "Path-end type collision";
  remediation: "Step type '{0}' is used on more than one exclusive path in workflow '{1}' under distinct instance names. Routing maps key by step type, so instance names do not disambiguate; use distinct step types.";
  since: "2.11.0";
}

/** AGWF037 — Duplicate permitted fork trigger (#156.2). */
@jsonSchema
model AgwfEntryDuplicatePermittedForkTrigger {
  id: "AGWF037";
  severity: "error";
  summary: "Duplicate permitted fork trigger";
  remediation: "Workflow '{0}' declares permitted trigger '{2}' more than once on one diagnostic-fork edge at {1}. Two same-trigger declarations can carry different evidence schemas; declare each trigger at most once.";
  since: "2.11.0";
}

/** AGWF038 — Duplicate diagnostic-fork compensation seed (#156.3). */
@jsonSchema
model AgwfEntryDuplicateCompensationSeed {
  id: "AGWF038";
  severity: "error";
  summary: "Duplicate diagnostic-fork compensation seed";
  remediation: "Workflow '{0}' declares compensation seed '{2}' more than once on diagnostic-fork edges at {1}. Two edges that share a seed cannot share a DiagnosticForkCount counter; use distinct compensation seeds.";
  since: "2.11.0";
}
