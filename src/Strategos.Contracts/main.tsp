import "@typespec/json-schema";
import "./Events/Enums.tsp";
import "./Events/Envelope.tsp";
import "./Events/IntentEnvelope.tsp";
import "./Events/MergeGateDecision.tsp";
import "./Events/JourneyResult.tsp";
import "./Events/Lifecycle.tsp";
import "./Events/OntologicalLifecycle.tsp";
import "./Events/FabricDelegation.tsp";
import "./Events/ForkOccurrence.tsp";
import "./Events/OntologyAbstained.tsp";
import "./Workflow/ForkTrigger.tsp";
import "./Workflow/WorkflowDefinitionV1.tsp";
import "./Workflow/WorkflowRef.tsp";
import "./Workflow/WorkflowCatalog.tsp";
import "./Workflow/WorkflowAuthority.tsp";
import "./Workflow/references.tsp";
import "./Diagnostics/CheckNode.tsp";
import "./Diagnostics/Enforcement.tsp";
import "./Diagnostics/InvariantEntry.tsp";
import "./Diagnostics/AgwfCatalog.tsp";
import "./Gates/GateClass.tsp";
import "./Gates/GateDeclaration.tsp";
import "./Ontology/AbstentionResponse.tsp";
import "./Ontology/ActionPredicates.tsp";
import "./Ontology/Actions.tsp";
import "./Ontology/Authority.tsp";
import "./Ontology/ActionContract.tsp";
import "./Ontology/decorators.tsp";

using JsonSchema;

/**
 * LevelUp.Strategos.Contracts — cross-product schema substrate.
 *
 * TypeSpec is the single canonical source. The build emits:
 *   - JSON Schema (language-neutral, embedded as NuGet content)
 *   - C# records (compiled into the DLL; Basileus references it)
 *   - Zod schemas (Generated/zod/, read by Exarchos; #219)
 *
 * Strategos owns every projection of these types. Exarchos consumes and pins
 * the emitted Zod; it does not derive one.
 *
 * Phase 0 carries only a trivial probe model that proves the pipeline
 * end-to-end (JSON Schema + a sealed/init-only C# record). The three schema
 * families (events #36, workflow IR #50, invariants #98) land in later phases.
 */
@jsonSchema
namespace LevelUp.Strategos.Contracts;

/**
 * Phase 0 pipeline probe — the minimal model that exercises the full toolchain:
 * a scalar, a bounded int32 sequence (int64 is avoided per the spike finding),
 * and a collection (proving `IReadOnlyList<T>` emission for INV-7).
 */
model PipelineProbe {
  /** Stable identifier for the probe instance. */
  @minLength(1)
  id: string;

  /** Monotonic sequence — int32 (not int64) to avoid the JSON Schema string coercion. */
  @minValue(0)
  sequence: int32;

  /** Free-form tags, proving collection emission. */
  tags: string[];
}
