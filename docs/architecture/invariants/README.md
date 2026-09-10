# Strategos architectural invariants

One file per invariant. Each states the rule, the acceptance questions a design
or diff must answer, the repo-grounded checks that prove the rule holds today,
the overlap with the generic `axiom` dimensions, a severity guide and a worked
violation with its fix. [`deterministic-checks.md`](deterministic-checks.md)
collects the mechanical grep and structural patterns per invariant.

| Id | Formerly | Rule | Default severity |
|---|---|---|---|
| [U-1](U-1-workflow-durability-via-wolverine-marten.md) | INV-1 | Workflows lower into Wolverine + Marten via the Roslyn source generator; sagas are emitted, never hand-written | blocking |
| [U-2](U-2-ontology-analyzer-only-self-contained.md) | INV-2 | The ontology uses Roslyn analyzers (never generators) and takes no Wolverine or Marten dependency | blocking |
| [U-3](U-3-mcp-first-class-latest-spec.md) | INV-3 | MCP is first-class and tracks the current protocol revision (2026-07-28) | advisory |
| [U-4](U-4-concrete-dsl-nomenclature.md) | INV-4 | The workflow DSL uses concrete domain nomenclature, never graph-theory terms | advisory |
| [U-5](U-5-three-tiered-validation-stable-diagnostic-ids.md) | INV-5 | Three validation tiers with stable `AGWF*` / `AONT*` diagnostic ids | blocking |
| [U-6](U-6-sealed-by-default.md) | INV-6 | DSL and descriptor types are sealed by default | blocking |
| [U-7](U-7-immutable-record-state.md) | INV-7 | Immutable record state; step results never mutate their input | blocking |
| [U-8](U-8-polyglot-identity.md) | INV-8 | Polyglot identity: `ClrType` or `SymbolKey`, both first-class | blocking |

## Two representations, one catalog

- **This directory** is the prose catalog people read and cite.
- [`.exarchos/invariants.md`](../../../.exarchos/invariants.md) is the
  machine-readable twin (schema-version 3, user tier) that exarchos loads at
  design and review time through [`.exarchos.yml`](../../../.exarchos.yml). Its
  `references` point back here.

Change them together. `tests/Strategos.Architecture.Tests` asserts that every
`U-N` in the catalog has exactly one `U-N-*.md` here and vice versa, and that
every referenced path exists.

## Why the ids changed from INV-N to U-N on 2026-09-10

exarchos reserves the `INV-*` namespace for its own substrate catalog and rejects
`INV-*` ids in a user-tier catalog, so the catalog could not be registered under
the old names. The rules themselves did not change. Specs, CHANGELOG entries and
issues written before that date keep the `INV-N` spelling as history; read
`INV-N` as `U-N`.

## Where they are used

- `/exarchos:ideate` and `/exarchos:review` audit designs and diffs against the
  catalog (the `design-invariants` skill under `.agents/skills/` is the
  agent-facing wrapper and links here).
- The mechanical checks run as tests, so a stale deny-list fails loudly instead
  of returning zero hits (the failure mode that motivated tracking the catalog,
  lvlup-sw/strategos#178).
