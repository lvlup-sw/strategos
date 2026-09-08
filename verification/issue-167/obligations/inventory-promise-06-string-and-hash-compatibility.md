# Promise obligation 06 — legacy string authoring and graph hash

- **Historical Stage 2 status:** candidate-supported (not evidence-bound)
- **Historical Stage 2 subject:** working tree over base `45c86a63a9437abd920240b4dc95b235c0f72d37`; final binding was pending.
- **Promise sources:** issue #167 acceptance criteria 3–4; `IC-CHANGELOG-004`, `IC-MIGRATION-001`/`007`, `IC-HASH-001`/`002`.

This file preserves the Stage 2 inventory record. Present-tense delivery notes below describe that
historical snapshot; see `../ledger.md` and `../final-evidence.md` for current posture.

## Obligation

Existing `.BoundToWorkflow("name")` calls must compile and construct the typed immutable reference
without trimming/case normalization. Replacing the old descriptor string property with
`WorkflowBindingReference(sameId)` must write the identical length-prefixed bytes at the same
canonical position; a changed identifier must still change the graph hash.

The cheapest sufficient proof is **R4, a before/after serialization oracle**. Compiling the overload
(R2) does not establish byte identity, and comparing two new implementations would not establish
compatibility with the base revision.

## Delivery evidence

- Both `ActionBuilder.cs:40-49` and `ActionBuilderOfT.cs:68-76` retain the string overload and
  immediately construct `WorkflowBindingReference`; that constructor preserves the supplied
  nonblank string exactly (`WorkflowBindingReference.cs:4-14`).
- `OntologyGraphHasher.cs:238-253` writes `BoundWorkflow?.WorkflowId` between the same binding-type
  and tool-routing fields occupied by the legacy workflow-name slot.
- `ActionBuilderTests.cs:54-93` and `ActionBuilderOfTTests.cs:100-134` cover the old overload, typed
  overload, null, and whitespace behavior.
- `OntologyGraphVersionTests.cs:288-338` proves changed workflow IDs change the version and compares
  the typed fixture to the independently executed base-revision oracle
  `17f5da54ed8eeb9aa31e2796c6f5dce69d07cbf36d6c289564f8a5f641fc4a00`.

The broader #168 action-contract canonicalization intentionally rolls action-bearing graph hashes
once. This obligation is only the wrapper-only comparison with all other bytes fixed, as the
migration and graph-versioning docs now state.
