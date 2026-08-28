# claim-clr-free-xor-polymorphic — Docs name the CLR-free XOR polymorphic limit and first-class descriptor path

Lens: 6 Claim Derivation
Disposition: obligation
Inventory claims: 36, 37, 59, 74, 75, 137, 138, 139, 140, 141, 142
Confidence: medium — this wave's delivery is prose; the type limit is pre-existing

## Ledger

| | |
|---|---|
| **Claim** | Docs name `ObjectTypeFromDescriptor` / `ApplyDelta` as the first-class CLR-free path; fluent `Object<T>` / `Interface<T>` stays CLR-generic. They record the CLR-free ⊕ polymorphic limit: a SymbolKey-only interface fan-out is not expressible (cited from `RationaleCorpusParityTests`). An `InterfaceDescriptor` carries a CLR `Type`; a CLR-free descriptor has `ClrType == null` and cannot also be a polymorphic interface target. This is a type-system limit, not a missing fluent API. |
| **Scope** | `docs/src/content/docs/guide/ontology/polyglot-descriptors.md` and CHANGELOG Residue; the cited parity test as the bound. |
| **Consequence** | Authors invent a fluent `Object(symbolKey)` + interface twin that the type system cannot express, or treat fluent generics as a CLR-free path. |
| **Proof rung** | Human judgment |
| **Proof artifact** | Review that the guide pages state the limit the types already enforce, and that the citation still matches `RationaleCorpusParityTests`. The type-system limit itself is already rung 2 and is not this wave's new mechanism. |
| **Why not cheaper** | These guide pages are not generated from the type system. A substring test that the quote appears can pass on a comment (survey's INV-3 grep shape). The cited parity test proves the bound, not that the new prose is accurate or complete. |
| **Failure signal** | Nothing. |
| **Rollback** | Revert the doc edits. No runtime change claimed. |
| **Lenses** | 6 Claim Derivation (claims 36–37 / 59 / 137–142). |

**Open questions:**

- Untracked `docs/2026-06-16-edge-*` files are out of scope (claim 7). Do not treat junction-table leftovers as a second source of this limit.

## Evidence

Plan T6 (claims 36–37), CHANGELOG (`CHANGELOG.md:197–199`, claim 59), commit `c366147` (claims 74–75), polyglot-descriptors.md (claims 137–142). Existing-proof S7: ontology guide edits are rung 6 prose, not proofs. The cited `RationaleCorpusParityTests` string is the bound, not a new test this wave adds.
