import "@typespec/json-schema";

using JsonSchema;

namespace LevelUp.Strategos.Contracts;

/**
 * Closed enum of the gate CLASSES a workflow's quality/merge gates evaluate
 * (#150, DR-1). This is the single typed gate identity shared across the
 * contract boundary so both runtimes consume ONE gate vocabulary.
 *
 * Member names are PascalCase; the wire VALUES are the exact snake_case tokens
 * cross-repo consumers match on. Exarchos's TypeScript enum and Basileus's C#
 * enum round-trip against these **by value** (INV-8 polyglot identity) — never by
 * ordinal. Adding a class is an additive minor; renaming or removing one is a
 * breaking major.
 */
@jsonSchema
enum GateClass {
  /** Static type checking. */
  Typecheck: "typecheck",

  /** Lint / style analysis. */
  Lint: "lint",

  /** A scoped (focused) test run. */
  ScopedTest: "scoped_test",

  /** The full test suite. */
  FullSuite: "full_suite",

  /** Mutation-adequacy testing. */
  MutationAdequacy: "mutation_adequacy",

  /** The merge-gate evaluation. */
  MergeGate: "merge_gate",

  /** LLM-as-judge evaluation. */
  LlmJudge: "llm_judge",
}
