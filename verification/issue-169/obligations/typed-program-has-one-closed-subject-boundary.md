# typed-program-has-one-closed-subject-boundary

## Why this obligation exists

Composition is deliberately one-subject in v2.13. The shared `OntologyActionCatalog` resolves exact
`(DomainName,ObjectTypeName,ActionName)` triples only from rooted declarations in the current Roslyn
compilation. `WorkflowBindingProofAnalyzer` evaluates every binding that claims rollback safety and
rejects cross-subject, opaque, missing, ambiguous, or dynamic boundaries.

## Failure scenario and boundary

The generator uses CLR type identity or a partial name and combines an `Order` forward action with a
`Payment` inverse. Another false green is selecting one resolvable binding while ignoring a second
opaque or cross-subject binding that claims the same workflow.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Artifacts: rooted catalog fail-closed suite; workflow multiple-binding tests at
  `WorkflowBindingProofAnalyzerTests.cs:683`, `:716`, `:745`, `:774`, and `:809`.
- Current disposition: Unproven pending protected execution and catalog drift kill.

## Refutation attempts for Stage 3

1. Fall back from exact triple to action name only.
2. Accept the first binding and ignore a second cross-subject binding.
3. Use runtime/reflection types outside the compilation-local boundary and confirm static proof does
   not silently succeed.

## Open questions

None.

## Investigation Log

No unresolved question. Referenced-assembly/runtime ontology contributions are intentionally outside
static proof and must receive graph-freeze/runtime validation instead of a fabricated compile pass.
