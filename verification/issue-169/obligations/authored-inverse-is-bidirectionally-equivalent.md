# authored-inverse-is-bidirectionally-equivalent

## Why this obligation exists

One implication permits either a too-strong inverse requirement or a too-weak restoration promise.
`ActionCalculus.AddEquivalenceFailures` at
`src/Strategos.Ontology/Descriptors/ActionCalculus.cs:841` is invoked for both requirement and
effective guarantee at lines 235 and 246. Equivalent orchestration appears in
`OntologyInverseContractAnalyzer` and `WorkflowBindingProofAnalyzer.ProveCompensationContracts`
(`src/Strategos.Generators/Proof/WorkflowBindingProofAnalyzer.cs:713`).

## Failure scenario and boundary

An inverse may compile while refusing a forward post-state or while guaranteeing only part of the
forward requirement. AONT216, AGWF044, and runtime analysis must agree on definite closed inputs.
Dynamic/unreadable inputs are not fabricated as valid; their runtime graph-freeze backstop is a
separate path.

## Assigned proof

- Rung: R3, deterministic structural analysis.
- Existing artifacts: the two finite-solver implication directions in all front ends; runtime vectors at
  `ActionInverseTests.cs:82` and `:106`; source vectors at
  `AONT216CompensationTests.cs:37`, `:58`, and `:78`; workflow vectors at
  `WorkflowBindingProofAnalyzerTests.cs:421` and `:909`. Proposed: one neutral corpus executed by all
  three adapters with positive, weaker, stronger, opaque, invalid, frame, subject, and authority
  vectors; independent examples are not called a shared corpus. Precedence/witness vectors may share
  the file but are owned by `inverse-failure-precedence-and-witnesses-are-stable`.
- Current disposition: Unproven. `mutation-evidence.md` binds a local kill of the
  authored-to-derived implication mutant to historical precursor `42b4ed7`/tree `2d0f3027`: 30
  focused tests ran, 29 passed, and
  `AuthoredGuaranteeMustRestoreTheForwardRequirement` failed for the exact missing direction. The
  guard is not a protected CI job, and common cross-front-end parity remains unbound.

## Refutation attempts for Stage 3

1. Delete derived-to-authored implication for requirements.
2. Delete authored-to-derived implication for guarantees.
3. Feed invalid, opaque, missing, stronger, and weaker contracts through every front end and compare
   classifications and stable witnesses.

## Open questions

- Does one shared vector corpus execute against all three high-level orchestrators, or do parallel
  tests merely happen to cover similar examples? The local mutation answered sensitivity of the
  runtime fixture only, not this parity question.

## Investigation Log

### Is high-level inverse orchestration single-sourced?

- Read: runtime calculus, ontology analyzer, workflow analyzer, project source links, and Stage 1
  authority topology.
- Found: normalization, solver, Roslyn bridge, and catalog are shared; status precedence and
  comparison orchestration are repeated.
- Conclusion: the kernel authority is shared, while parity remains an open R3 guard requirement.

### Does the runtime fixture detect removal of authored-to-derived implication?

- Read: `verification/issue-169/mutation-evidence.md`, bound to historical precursor
  `42b4ed7`/tree `2d0f3027`, not the final subject.
- Found: the focused ActionInverse suite exited 2 and the discriminating guarantee-restoration test
  failed; restored source then passed 30/30.
- Conclusion: killed on the precursor only. Exact-final and protected-path mutation remain
  Indeterminate, so the obligation remains Unproven rather than Verified.

## Stage 3 disposition

PF-8 confirmed that the R3 assignment is sound but “shared vectors” overstated existing evidence.
The neutral corpus is Proposed and is shared with the new precedence/witness obligation.
