import "@typespec/json-schema";
import "./ActionPredicates.tsp";
import "./Authority.tsp";
import "../Workflow/ActionReference.tsp";
import "../Workflow/WorkflowDefinitionV1.tsp";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * The action contract as a DOCUMENT, and the portable catalog that carries it
 * across an assembly boundary (#193 kernel, consumed by #204).
 *
 * Until now the wire carried only REFERENCES to actions (`ActionReferenceV1`)
 * and the authored-TypeSpec decorators (`x-strategos-requires-v1` and its
 * siblings on an operation's schema). Neither is a document a referencing
 * compilation can read: the first names an action without describing it, the
 * second describes an action authored HERE. `ActionContractV1` is the contract
 * itself, detached from the operation that declared it.
 */

/** The ontology object an action acts on. Mirrors `ActionSubject`. */
model ActionSubjectV1 {
  /** Ontology domain name. */
  @minLength(1)
  @pattern(".*\\S.*")
  domainName: string;

  /** Ontology object type name. */
  @minLength(1)
  @pattern(".*\\S.*")
  objectTypeName: string;
}

/**
 * One action's full contract: what it acts on, what it needs, what it
 * guarantees, what it touches, what authority it demands, how it is undone,
 * and whether repeating it changes anything.
 *
 * The seven members are the action calculus's own vocabulary (#164, #167,
 * #168, #169), projected verbatim. `AcceptsType` and `ReturnsType` are NOT
 * projected — see `TypedContractRefV1` for the slot that replaces them.
 */
model ActionContractV1 {
  /** The object this action acts on. */
  subject: ActionSubjectV1;

  /** The action name, unique within its subject. */
  @minLength(1)
  @pattern(".*\\S.*")
  name: string;

  /** Free prose. Excluded from `contentHash`, like every other description. */
  description?: string;

  /** Typed preconditions, hard and soft (#168). */
  requires: ActionRequirementV1[];

  /** Typed post-state guarantees (#168). */
  ensures: ActionGuaranteeV1[];

  /** The frame: every resource this action can change (#164). */
  touches: ActionResourceV1[];

  /**
   * The authority coordinate this action demands. Omitted when the action
   * demands none.
   */
  authority?: AuthorityRequirementV1;

  /**
   * The action that undoes this one (#169). A full reference, because an
   * inverse can live on another object. Omitted when no inverse is declared
   * or derivable.
   */
  inverse?: ActionReferenceV1;

  /** Whether repeating the action has the same observable effect. */
  idempotent: boolean;
}

/** Every action contract a catalog declares. The action side of #204's manifest. */
model ActionCatalogV1 {
  /**
   * The contracts, in a stable ordinal order by (subject.domainName,
   * subject.objectTypeName, name). Order is part of `contentHash`.
   */
  actions: ActionContractV1[];
}

/**
 * The portable proof manifest — the document #204 emits from one assembly and
 * reads from a referencing one, so a binding whose declarations live in a
 * referenced package can still be machine-checked.
 *
 * This root is frozen HERE, with the types it composes, so #204 is purely
 * emission, consumption and proof, with no further Contracts change.
 *
 * Fail-closed is the consuming analyzer's contract, not this schema's: an
 * unknown `schemaVersion`, a duplicate identity, an opaque contract or an
 * incomplete lattice must be rejected by the reader. JSON Schema cannot
 * express those rules and does not try to.
 */
model ProofCatalogV1 {
  /** Manifest schema version: pinned literal `1.0`. */
  schemaVersion: "1.0";

  /**
   * Ordinal identity of the assembly or package this manifest describes.
   * Compared ordinally when catalogs merge, never parsed.
   */
  @minLength(1)
  @pattern(".*\\S.*")
  catalogId: string;

  /**
   * SHA-256 over the manifest's structural fields, lowercase hex. Prose is
   * excluded, on the `OntologyGraphHasher` rules. #204 reads this for tamper
   * and skew detection. Omitted when the producer does not stamp one.
   */
  @pattern("^[0-9a-f]{64}$")
  contentHash?: string;

  /** The action contracts this catalog declares. */
  actions: ActionCatalogV1;

  /**
   * The authority lattices the action contracts' coordinates are read against,
   * one per domain. An action whose authority names an axis absent here is an
   * incomplete lattice, and the reader must fail closed on it.
   */
  authorities?: DomainAuthorityLatticeV1[];

  /**
   * The workflow definitions this catalog declares, so a referencing
   * compilation can resolve a `BoundToWorkflow` name it cannot see in source.
   */
  workflows?: WorkflowDefinitionV1[];
}
