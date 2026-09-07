# Promise obligation 11 — compilation-local proof boundary

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** `IC-COMMENT-006`, current migration/workflow/action-calculus docs, claim seed 11.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

#167 must not imply that referenced binaries or runtime `IOntologySource` contributions are proved.
The supported v2.13 contract is compilation-local: bound action, target workflow, and leaf actions
must be visible to one generator invocation, with JSON workflows provided as `AdditionalFiles`.
Portable cross-assembly catalogs must remain explicit unresolved product work rather than a silent
success path.

The cheapest sufficient proof is **R3 structural boundary analysis plus public documentation**.
This obligation describes what the compiler can observe, not an execution outcome.

## Delivery evidence

- `OntologyActionCatalog.Build` enumerates `compilation.SyntaxTrees` and source-visible
  `DomainOntology.Define` roots (`OntologyActionCatalog.cs:42-198`); it does not inspect metadata
  assemblies or execute runtime sources.
- Unrooted direct descriptors cannot satisfy leaf resolution; `OntologyActionCatalog.cs:200-229`
  retains an unrooted workflow binding only as invalid so it fails closed.
- `WorkflowBindingProofAnalyzer.cs:15-16` explicitly calls the proof compilation-local.
- The action-calculus reference at `:378+`, migration guide at `:357-369`, and workflow API at
  `:172-178` all state the source/AdditionalFiles boundary and absence of runtime re-proof.
- GitHub issue #204 exists as the explicit portable-catalog follow-up, and the docs link it.
- `OntologyActionCatalogFailClosedTests` covers unrooted descriptors, inline rooted descriptors,
  factored helpers, foreign builder objects, dynamic domain names, and SymbolKey-only rooted
  descriptor ownership.

The unqualified original issue sentence “a non-existent workflow fails the build” is therefore
scoped only inside this explicitly documented v2.13 support boundary, not for an action hidden in
a referenced assembly.
