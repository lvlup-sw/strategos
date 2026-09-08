# Promise-against-delivery ledger — issue #167

## Provenance state

- Base/HEAD during this lens: `45c86a63a9437abd920240b4dc95b235c0f72d37`.
- Last observed product-tree fingerprint:
  `794f9331f5f63ce76e0125834bb0a7e32541428a9de0dcc29a12659c8140f45f`.
- Fingerprint command:

  ```bash
  (git diff --binary -- . ':(exclude)verification'; \
    git ls-files -z --others --exclude-standard -- . ':(exclude)verification' \
      | sort -z | xargs -0 -r sha256sum) | sha256sum
  ```

This is a deliberately preserved **historical Stage 2** fingerprint: audit-driven product and
proof-harness edits continued after the inventory. It identifies that observation, not a release
subject. Every `candidate-supported` status below is only the historical logical promise/delivery
classification. Stage 3 later bound a complete local portfolio and artifact digests to immutable
commit `4110b512`. The proof-harness, rooted-descriptor, emitter-boundary, and completed-event naming
repairs were subsequently committed at `595a949`; `6851fe7` serialized the local-feed pack
invocation, and current final subject `98fabb4` closes the workflow-name nonblank mismatch. Current
evidence binding and formal state are in
`../final-evidence.md` and `../ledger.md`.

## Ledger

| ID | Obligation | Cheapest sufficient rung | Status | Final-binding work |
|---|---|---:|---|---|
| PROM-01 | Exact ordinal C#/JSON workflow resolution; 0/many => AGWF039 | R3 | candidate-supported | Re-run generator proof suite |
| PROM-02 | Entry/exit/subject/frame/authority workflow refinement | R4 | candidate-supported | Re-run shared and topology vectors |
| PROM-03 | Internal seams and topology fail-closed behavior | R4 | candidate-supported | Re-run closure/topology matrix |
| PROM-04 | Occurrence identity across builder/extractor/wire/import/proof | R4 | candidate-supported | Re-run core/contracts/generator tests |
| PROM-05 | Fork footprint/noninterference and joint-tail guarantee | R4 + mutation | candidate-supported | Repeat or bind mutation kill |
| PROM-06 | Legacy string call plus wrapper-only graph-hash bytes | R4 | candidate-supported | Re-run ontology hash oracle |
| PROM-07 | Runtime result invariants and shared refinement classifications | R4 | candidate-supported | Re-run both vector interpreters |
| PROM-08 | Contracts 0.11 schema/model/validation/legacy omission | R4 | candidate-supported | Regenerate and run Contracts suite |
| PROM-09 | Fresh packed consumer legal build and illegal AGWF041 build | R5 | unproven | Record nupkg SHA-256 and probe logs |
| PROM-10 | Public API/codegen/diagnostic/projection authorities | R3/R4 | candidate-supported | Run API and codegen drift gates |
| PROM-11 | Explicit compilation-local boundary; portable catalog => #204 | R3 | candidate-supported | Confirm docs/issue link at final commit |
| PROM-12 | #169 seam adoption, downstream issues, PR/review/merge process | R4/external | unproven | #169 consumer + issue/PR evidence |

## Execution notes

Two passing observations predate the ongoing audit edits and therefore are not final provenance:

```text
dotnet build src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj \
  --configuration Release --no-restore --disable-build-servers -m:1 -v minimal
=> Build succeeded, 0 warnings, 0 errors (fingerprint 64a0eca0...).

dotnet test --project src/Strategos.Generators.Tests/Strategos.Generators.Tests.csproj \
  --configuration Release --no-build --no-restore
=> 1700 passed, 0 failed, 0 skipped (fingerprint 7265dfa5...).
```

A later historical full-suite attempt observed the shared `ParserTestHelper` while it was intentionally being
strengthened and returned 1604 passed / 100 failed from fixture-compilation errors. The root agent
identified that state as transient before the run. It is neither positive evidence nor a product
refutation. It was superseded first by the historical exact-4110 portfolio and then by the later
evidence recorded in `../final-evidence.md`.

## Actionable gaps

1. Bind the exact-current local portfolio to protected PR CI and a completed-diff review.
2. Run the future Contracts tag workflow against its own tag commit and bind the release-package
   bytes; exact-current candidate digests cannot substitute for that future event.
3. Track implementation/test results in the existing Basileus #493 and Exarchos #1893 issues.
4. Keep PROM-12 open through #169; #167 correctly rejects `Compensate<T>` as AGWF042 but cannot
   prove that the future consumer reuses the occurrence mapping.
