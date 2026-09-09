# proof-authority-is-shared-across-runtime-and-analyzers

## Why this obligation exists

Runtime `ActionCalculus`, AONT216, and AGWF044 operate in different target frameworks. The neutral
Roslyn catalog/bridge/finite proof kernel is source-linked from `src/Shared/Analyzers/Proof`, including
`OntologyActionCatalog.cs`, so normalization and SymbolKey-only ownership do not become copied
authorities.

## Failure scenario and boundary

One analyzer uses a stale copied solver/catalog and resolves a decoy or proves a formula that runtime
rejects. All three isolated suites can remain green on different examples. High-level inverse
orchestration is still triplicated, so source linking alone cannot prove classification parity.

## Assigned proof

- Rung: R1, construction and generation for the neutral kernel authority.
- Existing artifacts: linked `Compile` items in analyzer projects and rooted catalog fail-closed
  cases. Missing: source/dependency ownership guard. Proposed R3/R4 backstop: common semantic vectors
  for deliberately repeated high-level orchestration.
- Current disposition: Unproven pending protected build/source-ownership kill and common vectors.

## Refutation attempts for Stage 3

1. Copy and alter the catalog in one analyzer; ownership guard must fail.
2. Resolve an unrelated `Define` overload; AONT216 decoy test at
   `AONT216CompensationTests.cs:179` must fail.
3. Reorder Invalid/Refuted/Opaque precedence in one front end; parity vectors must fail.

## Open questions

- Will a common corpus cover invalid, opaque, missing, identity, effective guarantee, semantic
  authority, exact frame, both implication directions, and deterministic witnesses?

## Investigation Log

### Is every inverse decision single-sourced?

- Read: Stage 1 authority topology and all three orchestrators.
- Found: lower kernel and catalog are shared/source-linked.
- Not found: one neutral orchestration for status/precedence/comparison/reason assembly.
- Conclusion: keep the narrow R1 claim and a separate parity guard; do not call all semantics shared.

## Stage 3 disposition

PF-7/PF-8 make artifact existence explicit. Runtime does not consume the Roslyn catalog, and
high-level status/frame/authority/reason assembly is not called single-sourced. The new
`inverse-failure-precedence-and-witnesses-are-stable` row owns all-adapter parity.
