# claim-handauthoredcontract-ingest-assignment — Who assigns HandAuthoredContract?

Lens: 6 Claim Derivation
Disposition: open question
Inventory claims: (depends on 34, 57, 130, 133)
Confidence: n/a — unsettled

## Open question

Is `DescriptorSource.HandAuthoredContract` assigned by any in-repo or out-of-repo producer (TypeSpec ingest, JSON contract ingest, fluent builder, merge)?

**Stakes.** Claim 57 says AONT205 retargets "so TypeSpec / JSON contract-authored actions survive graph merge." That "so" is true only if those ingest paths tag `HandAuthoredContract` (or otherwise avoid `Ingested` + intent). Survey backbone §6: `HandAuthoredContract = 2` has no production assignment found; `MergeTwo` still writes `Source = HandAuthored`; unwidened `== HandAuthored` branches remain at `OntologyGraphBuilder.cs:330/:409/:566`. Existing-proof P36/P37 construct the enum in tests. Compile-time AONT205 descriptor has no `ReportDiagnostic` site.

If no producer assigns `2`, the additive enum is an inert public member, AONT205 retarget does not change who TypeSpec/JSON ingest fails, and claim 57's consequence is unsupported. The retarget obligation (`claim-aont205-ingested-only`) can still be true of the runtime predicate and false of the ingest pipeline.

If merge collapses `2` → `HandAuthored` (P37:87), "survives graph merge" can be true of the action list and false of provenance.

## What would settle it

A trace from each ingest entry point to the `Source =` assignment. If every path writes `Ingested` or `HandAuthored` only, promote a finding that nothing supports claim 57's "so" clause. If a path writes `2`, attach that path to `claim-aont205-ingested-only` and close this question.

This file is not an obligation. It is the validation gate those claims did not pass.
