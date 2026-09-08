# packed-consumer-loads-and-enforces-typed-compensation-warning-free

## Why this obligation exists

Project-reference tests can pass while a nupkg omits the analyzer or a dependency. The initial #169
probe omitted typed compensation; a later exact consumer found generated CS8604 warnings. The final
script uses local Strategos packages, an empty per-run cache and exclusive local source mapping for
Strategos IDs, all warnings as errors, a legal typed program, and exclusive AGWF041/AGWF044 negatives.

## Failure scenario and boundary

The package is present but analyzer code is not loaded, legal generated source warns/fails, or restore
network failure gets reported as product success. The legal probe's CLR bodies are no-ops, so this
obligation proves packaging/analyzer enforcement, not actual inverse effects.

## Assigned proof

- Rung: R5, production-path integration.
- Artifact: `scripts/verify-generator-consumer-build.sh` against exact nupkg SHA-256s, with exit 0
  Pass, exit 2 product Fail, and exit 3 Indeterminate.
- Current disposition: Unproven. Exact-final local execution verified complete package closure,
  source/restored-byte equality, stale-artifact exclusion, a legal 0-warning/0-error consumer, and
  exclusive AGWF041/AGWF044 negatives. Protected binding remains absent.

## Refutation attempts for Stage 3

1. Remove generator/analyzer from nupkg.
2. Disable AGWF044 while leaving AGWF041 active.
3. Reintroduce a CS86xx warning.
4. Force restore failure and require exit 3, never 0 or product exit 2.
5. Seed a stale package with the same ID/version and require fresh source/restored-byte provenance to
   reject or bypass it.

## Open questions

- Does the protected wrapper preserve the three-way result instead of tolerating exit 3?

## Investigation Log

### Can the package probe prove executable inverse restoration?

- Read: embedded consumer ontology/state/step bodies and Stage 1 wildcard.
- Found: declared ontology transitions are meaningful; `ProbeStep.ExecuteAsync` returns unrelated
  `FlowState` unchanged.
- Conclusion: it proves package/analyzer declaration enforcement only; the trust boundary is explicit.

## Stage 3 disposition

PF-12 corrected the historical stale-evidence narrative without promoting state. COV-05 created a
separate packed ontology-analyzer/AONT216 obligation. Exact-final local package execution now covers
both rows; their protected executions remain absent.
