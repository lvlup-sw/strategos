# rooted-descriptor-catalog-proof-nonvacuous — Rooted descriptor catalog proof is nonvacuous

| | |
|---|---|
| **Claim** | Inline `ActionDescriptor` values participate in #167 resolution only when owned by `DomainOntology.Define -> ObjectTypeFromDescriptor -> Actions`, and deleting that supported inventory path makes a proof fixture fail instead of yielding zero bound actions and zero diagnostics. |
| **Scope** | `OntologyActionCatalog.InventoryDirectDescriptorActions`, direct descriptor ownership, SymbolKey-only ontologies, and workflow occurrence resolution. |
| **Consequence** | Supported descriptor-authored ontologies silently receive no build-time workflow proof; the positive test remains green because the analyzer has nothing to analyze. |
| **Proof rung** | Rung 4 — contract and component tests. |
| **Proof artifact** | A real-generator rooted descriptor fixture with an intentionally illegal seam or missing occurrence that must emit an exact binding diagnostic, paired with the current legal and unrooted controls. Mutation-removing `InventoryDirectDescriptorActions` must kill the negative fixture. |
| **Why not cheaper** | Rung 1 does not own hand-authored Roslyn syntax. Rung 2 accepts both rooted and unrooted descriptor construction. Rung 3 can recognize ancestry but does not establish catalog-to-workflow resolution and proof emission. |
| **Failure signal** | Nothing at build time if rooted inventory disappears; runtime still registers the descriptor and executes the workflow without the promised static proof. |
| **Rollback** | Author the actions through the supported `builder.Object(..., obj => obj.Action(...))` fluent path, or revert descriptor support until it is guarded. Merely retaining the positive empty-diagnostic test does not reverse the exposure. |
| **Lenses** | False-green shapes: vacuous positive, skipped-as-pass, cannot-fail. |

**Confidence:** High.

**Current proof posture:** Indeterminate, locally supported at exact-current `98fabb4`. The suite covers legal rooted, unrooted, subject-mismatch, conditional, and escaped controls. `InlineObjectTypeDescriptorBoundAction_WithMissingOccurrence_ReportsAgwf040` uses a valid rooted direct descriptor as the bound action while leaving a configured occurrence unresolved, requiring the exact AGWF040 diagnostic. Removing or selectively skipping rooted inventory now kills the negative instead of producing a vacuous green. The affected suite passed 35/35 and the complete generator portfolio passed 1,761/1,761. Protected execution and review remain pending.

**Open questions:**

- None.

### Investigation Log

#### Would removal of rooted descriptor support make the positive proof red?

- Read: `OntologyActionCatalog.Build`, `InventoryDirectDescriptorActions`, `InlineDescriptorSource`, the legal rooted, subject-mismatch, unrooted, and rooted-missing-occurrence tests.
- Historical finding: the legal positive could remain green if rooted inventory disappeared wholesale; subject mismatch proved ownership but not bound-action-to-occurrence participation.
- Current finding: the valid-rooted-bound-action fixture requires catalog resolution to reach a missing occurrence and emit AGWF040, closing that selective-vacuity route; it passed in the exact-current `98fabb4` portfolio.
- Not found: protected execution or completed review bound to `98fabb4`.
- Conclusion: the proof design is nonvacuous in committed, exact-current locally exercised code; formal status remains `Indeterminate` pending protected execution and review.
