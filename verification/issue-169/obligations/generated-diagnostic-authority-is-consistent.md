# generated-diagnostic-authority-is-consistent

## Why this obligation exists

AGWF044/045 cross a TypeSpec closed enum, per-entry literal models, generated C# enum/constants,
catalog JSON, standalone/bundled schemas, generated Markdown, and live Roslyn descriptors. A closed
consumer enum makes additive diagnostic codes a real compatibility boundary.

## Failure scenario and boundary

The analyzer emits AGWF045 while the schema/enum rejects it, or catalog severity/message differs from
the live descriptor. Updating generated files by hand can hide a stale TypeSpec authority.

## Assigned proof

- Rung: R1, construction and generation for derived representations.
- Artifacts: `scripts/contracts-codegen.sh`; `AgwfCodeEnumTests`, `AgwfCatalogEmitterTests`,
  `AgwfCatalogSchemaTests`, and `AgwfMarkdownTests` for representations derived from TypeSpec.
- Current disposition: Unproven. Exact-final TypeSpec regeneration was byte-identical, all 191
  Contracts tests passed locally, and the three AGWF authority roots agreed at 39/39/39; protected
  binding is absent.

## Refutation attempts for Stage 3

1. Remove an enum member but retain its entry.
2. Alter one live descriptor severity/message.
3. Make codegen a no-op and require its self-test to report Indeterminate/fail.

## Open questions

None.

## Investigation Log

No unresolved question. AONT216 has a different runtime/source-analyzer topology; its semantic parity
is covered by the shared-proof and bidirectional-equivalence obligations rather than this AGWF codegen
claim.

## Stage 3 disposition

PF-2 split live `WorkflowDiagnostics` parity into `live-agwf-descriptors-match-generated-catalog` at
R4. This R1 file no longer claims generation controls that hand-authored root.
