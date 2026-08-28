# recur-diagnostic-trigger-reachable

Open class **R7**. Guard candidate **G-R7**. Completing AGWF035 (R1) does not add a catalog-wide reachability rule.

## What led here

AGWF022 fired on fork-path and loop-body confidence that was already lowered (correctness-core DR-6). Four `Deferred` parity entries and two emission blocks were wrong. The near-miss retarget (instance-named + configure-lambda fork-path) is unauthorable: `IForkPathBuilder` has no overload that takes both. Spec: retargeting there “would leave a declared control with no trigger, the same INV-5 violation.” #187 retargeted at approval-preceding-step confidence, which is authorable (`DeclaredButInertTests`).

AGWF035 over-reach-only is the same operator: diagnostic present, cannot see the filed under-reach shape (#184/#185). #179’s `SourceTexts.WorkflowWithTerminalBranch` is the bool-discriminator CS8510 shape consumed only as parse/Mermaid — a fixture that looks like coverage (R3).

## Surfaces at 324768f

- `AgwfCatalogParityTests` — catalog ↔ `WorkflowDiagnostics` identity (id, severity, title, message). Does not fire any diagnostic.
- `AgwfSingleSourceTests` — no production `AGWF0xx` literals.
- `DeclaredButInert_ApprovalPrecedingStepConfidence_ReportsAgwf022` and `_LowersNoConfidenceGate` — the one pinned live target.
- AGWF035 under-reach tests inject `WithoutSuccessor` and call `Report` directly. They do not prove a public DSL source that the **generator** rejects.
- No catalog field names a kill fixture path.

## Failure

Contributors suppress or trust an id that never fires, or that fires on a legal shape and blocks emission. INV-5 (three-tiered validation, stable ids) is then a catalog of inert or false controls. Who observes it: the next author who writes the filed shape and gets a green build (#184), or who cannot compile a legal workflow (false AGWF022).

## Expensive to find again

- Identity parity going green after adding an Error id is the reassuring shape. Reachability is a different claim.
- A `WithoutSuccessor` unit test will be offered as the AGWF035 kill for this guard. It is not: G-R7 requires `RunGenerator` / import emit.

## Open questions (with stakes)

- Which current Error ids lack a public-path kill? If several, G-R7’s first implementation fails on HEAD and needs a dated exception list. Stakes: shipping the closure test without the inventory either blocks the tree or gets waived and decays.

### Investigation Log

#### Do existing catalog tests check trigger reachability?

- Read: `AgwfCatalogParityTests.cs:38–77`; existing-proof P16–P21; recurrence seed R7.
- Found: identity and metadata strings. `DeclaredButInertTests` is per-id for AGWF022. Deferred-entry parity requires the cited diagnostic to fire for those rows only.
- Conclusion: no catalog-wide rule. G-R7 is not a duplicate of parity.
