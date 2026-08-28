# recur-enum-ordinal-frozen

Open class **R5**. Guard candidate **G-R5**. This diff appends `HandAuthoredContract = 2` and does not move `Ingested`. That is an instance fix.

## What led here

#183: generated `{Workflow}Phase` reorder from document-order `StepNames` is a data migration under Newtonsoft `EnumStorage` ordinal. Closed by docs (`phase-persistence.md` + CHANGELOG warning). #163: original proposal `HandAuthoredContract = 1`, `Ingested = 2` would have moved `Ingested`. This wave refuses that. #156.3 positional `DiagnosticForkCount_{i}` is the same class and **out of wave**.

Recurrence seed said “no fixture that fails if someone writes `Ingested = 2`.” That sentence is stale relative to this revision: `DescriptorSourceTests` now asserts the three ordinals. Those tests are a **one-enum snapshot**, not a class-level append-only guard. Phase remains docs-only.

## Surfaces at 324768f

- `src/Strategos.Ontology/Descriptors/DescriptorSource.cs:8–10, 46–63` — remarks say numeric values are the public contract and new members append. `HandAuthored = 0`, `Ingested = 1`, `HandAuthoredContract = 2`.
- `src/Strategos.Ontology.Tests/Descriptors/DescriptorSourceTests.cs:17–33` — asserts those three integers.
- PublicAPI.Unshipped lists the member name, not the ordinal (existing-proof P39).
- AONT205 retarget tests (`AONT205Tests`, `HandAuthoredContractMergeTests`) prove semantics of the new value, not freeze of the old ones.
- `builder-api-stability` / `public-api-drift.yml` track seven workflow builder interfaces, not this enum.

## Failure

Stored integers remap. A Newtonsoft host reads the wrong `{Workflow}Phase`. A consumer that persisted `DescriptorSource` as `1` meaning Ingested would read a different member if anyone inserted at 1. Who observes it: a migrated database or a wire payload, after deploy, not the author.

## Expensive to find again

- Remarks on the enum look like a guard and are a comment.
- PublicAPI green after a reorder (names unchanged, values moved).
- Counting #156.3 would change the “twice vs three times” proof-system finding; this wave must not invent that obligation.

## Open questions (with stakes)

- Is there a consumer already persisting `DescriptorSource` as an integer? If yes, even additive `= 2` is a compatibility event for anyone who serialized a closed two-value assumption (e.g. “any non-zero is ingested”). Stakes: G-R5’s seed map is necessary but not sufficient for out-of-repo readers.

### Investigation Log

#### Does a test already freeze Ingested == 1?

- Read: `DescriptorSourceTests.cs:24–33`.
- Found: `Ingested == 1` and `HandAuthoredContract == 2` at this revision.
- Conclusion: the recurrence-seed “missing fixture” line is wrong for DescriptorSource. It remains true for the **class** (Phase, any future public enum). G-R5 reuses the snapshot and adds the insert-at-1 kill from the #163 body.
