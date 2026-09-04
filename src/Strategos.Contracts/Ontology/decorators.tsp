import "./decorators.mjs";

using TypeSpec.Reflection;

namespace LevelUp.Strategos.Ontology;

/**
 * Declares the ontology object that owns an operation. Metadata is emitted as
 * JSON Schema extensions and consumed by Strategos.Contracts.Codegen.
 */
extern dec objectKind(
  target: Operation,
  domainName: valueof string,
  objectName: valueof string,
  kind: valueof string
);

/** Declares the authority literal required to invoke an operation. */
extern dec authority(target: Operation, name: valueof string);

/** Declares a relation precondition and the link path used to resolve it. */
extern dec relation(
  target: Operation,
  name: valueof string,
  ...linkPath: valueof string[]
);

/** Declares the clients allowed to surface an operation. */
extern dec clients(target: Operation, ...names: valueof string[]);

/** Declares whether a client must obtain confirmation before dispatch. */
extern dec confirm(target: Operation, required: valueof boolean);

/** Declares that an operation has no externally observable write effect. */
extern dec readOnly(target: Operation);

/** Declares that repeating an operation has the same observable effect. */
extern dec idempotent(target: Operation);
