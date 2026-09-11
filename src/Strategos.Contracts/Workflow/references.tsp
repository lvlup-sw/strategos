import "../Ontology/decorators.mjs";

using TypeSpec.Reflection;

namespace LevelUp.Strategos.Contracts;

/**
 * Declares that a string property is a REFERENCE into a collection elsewhere in
 * the same document: its value must equal the `idField` of some entry in
 * `collection`.
 *
 * This is the referential-integrity rule core JSON Schema cannot express (#219).
 * Before this decorator the rule lived twice in prose and twice in hand-written
 * code — once here as a doc comment, once in `Import/WireToModelBridge.cs` as
 * AGWF032, once in a consumer `superRefine`. The decorator makes it a
 * declaration: emitted into JSON Schema as the `x-strategos-references-v1`
 * extension key, and read by the Zod emitter, which lowers it into a
 * root-level `superRefine` nobody hand-writes.
 *
 * `collection` is a JSON Pointer rooted at the document that OWNS the
 * collection — `/gates`, not `/steps/0/gates`. The emitter resolves which root
 * document that is by finding the one root schema that both declares the
 * pointer's head segment and reaches the annotated property.
 *
 * An `x-strategos-*` key is inert to a generic JSON Schema validator: a
 * validator that does not know the key ignores it and accepts a dangling
 * reference, exactly as it does today. The rule binds where a reader reads it.
 *
 * Authoring is checked before emission. A blank or non-rooted `collection`, a
 * blank `idField`, and a target property that is not a string are all rejected
 * by `tsp compile` with a library diagnostic.
 */
extern dec references(
  target: ModelProperty,
  collection: valueof string,
  idField: valueof string
);
