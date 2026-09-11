# The workflow-definition kernel, v1

**Status:** frozen in `LevelUp.Strategos.Contracts` **0.13.0**.
**Issues:** [#193](https://github.com/lvlup-sw/strategos/issues/193) (the kernel),
[#209](https://github.com/lvlup-sw/strategos/issues/209) (stage a, this document),
[#204](https://github.com/lvlup-sw/strategos/issues/204) (the first machine-checked consumer).

The kernel is the language-neutral surface every consumer of the Exarchos semantic workflow
plane shares. This document records the two things the schema itself cannot say: which
reserved names map onto which existing construct, and which slots Strategos carries without
proving.

## The governing rule

**Add slots. Do not add a parallel vocabulary.**

Every field the kernel names either already existed on `WorkflowDefinitionV1` under another
name, or is a slot that the authority lattice (#165) and the typed predicate fragment (#168)
already typed. Two vocabularies for the same concept is the drift the kernel exists to
prevent, so the kernel extends the delivered types rather than sitting beside them.

Two consequences follow, and both are load-bearing:

- A step's `completion` is an `ActionGuaranteeV1` — the same predicate vocabulary the action
  calculus uses. It is not a second predicate language that happens to look similar.
- A step's `authority` is an `AuthorityRequirementV1` — a lattice **coordinate**. It is not a
  list of capability names. Two coordinates compare without resolving either against a
  lattice; two name lists do not.

## Reserved composition-combinator kinds

The composition-combinator names are reserved **structurally**. They add **no enum tokens** to
any wire schema, and a test pins that: `KernelSlotSchemaTests.ReservedCombinatorKinds_AddNoInertEnumTokens`.

An enum token with no implementation is not a harmless placeholder. Strategos emits strict
converters, so a producer that emits a reserved token produces a document every consumer
rejects. The name is reserved by being written down here and by the construct below already
carrying the meaning.

| Reserved kind | The construct that already carries it | Where |
|---|---|---|
| `sequence` | `transitions[]` — a directed edge from one step to the next | `WorkflowDefinitionV1.transitions` |
| `parallel` | `forkPoints[]` with `joinStepId` — concurrent fan-out and its join | `WorkflowDefinitionV1.forkPoints` |
| `choice` | `branchPoints[]` — conditional fan-out over `BranchPathDefinition` | `WorkflowDefinitionV1.branchPoints` |
| `compensation` | `CompensationConfiguration` on a step's configuration, with the typed `inverseAction` added in 0.12.0 | `StepConfigurationDefinition.compensation` |
| `host-continuation` | the `StepRuntime` federation slot on the shared step common | `StepCommon.runtime` |

The kernel's `edges[] { from, to, kind }` therefore stays a **projection** of `transitions[]`
and `forkPoints[].joinStepId`, not a new collection. Referential integrity across those edges
is declared as an authoring rule and emitted (see
[#219](https://github.com/lvlup-sw/strategos/issues/219)), not hand-written by each consumer.

## Carried, not proved

`WorkflowAuthorityV1` — the kernel's `authority { invariants, goals, assumptions,
delegatedDecisions, escalationBoundaries }` block — is **serialized and checked by nothing** in
3.0.

No analyzer reads it. No emitter lowers it. No runtime path evaluates it. A consumer that acts
on this block acts on unproved data, and this paragraph is the only warning it gets.

The block exists anyway, because the alternative is worse: each consumer invents its own
authority vocabulary now, and the plane converges them later across four repositories. One
shared vocabulary carried from birth costs a slot. Four private ones cost a migration.

The same status applies to the per-step `completion` and `authority` slots, and to the
`inputs` / `outputs` typed-contract references. Strategos carries all four and proves none of
them in 3.0.

### Why a coordinate is an ordered array, not a map

`AuthorityRequirementV1.coordinates` and `AuthorityDescriptorV1.coordinates` are arrays of
`AuthorityCoordinateV1 { axis, level }`, ordered ordinally by `axis`. The CLR side is an
`ImmutableDictionary<string, string>`, so a map was the obvious projection. It is the wrong one.

A JSON object has no canonical member order. Two producers serializing the same coordinate can
emit two byte sequences, and therefore two different `contentHash` values for one definition —
which defeats the digest that #204 compares for skew. An array sorted by `axis` has exactly one
serialization. `OntologyGraphHasher` already sorts every collection before hashing, for the
same reason.

### Why statements are a model, not a string

Each member of the frame is a list of `WorkflowAuthorityStatementV1 { id?, statement }` rather
than a list of strings. The frame is unproved today, so the fields a *proved* statement will
need — a provenance reference, a check id, a severity — are not yet known.

Adding a field to that model later is additive. Replacing `string[]` with a model later is a
**narrowing**, which pre-1.0 costs an allowlist entry and post-1.0 costs a V2 root. Designing
a slot that forces a future narrowing is exactly the error #209 set out to avoid, so the
model wrapper is the cheaper freeze.

## What the kernel deliberately leaves out

- **`CompiledPlan` and `Capsule`.** These stay out until an Exarchos `prepare` consumer
  exists. An Exarchos-owned schema can reference the kernel's `$id`. Contract-first cuts both
  ways: a contract nobody consumes is churn, not discipline.
- **A type system for `inputs` / `outputs`.** `TypedContractRefV1` carries the `$id` of a
  schema that already has an identity. Strategos never resolves the reference. Whether it
  resolves, and to what, is the resolving consumer's concern. The slot is frozen; the type
  system is not.
- **CLR type handles, under any name.** `AcceptsType` and `ReturnsType` stay unprojected
  (LB-2, INV-8). `KernelSlotSchemaTests` asserts their absence rather than trusting the
  authoring convention.

## The proof manifest

`ProofCatalogV1` is the portable manifest [#204](https://github.com/lvlup-sw/strategos/issues/204)
emits from one assembly and reads from a referencing one, so a binding whose declarations live
in a referenced package can still be machine-checked. It is frozen here, with the types it
composes, so #204 is emission, consumption and proof with no further Contracts change.

Its fail-closed rules — an unknown `schemaVersion`, a duplicate identity, an opaque contract,
an incomplete lattice — are the **reader's** contract, not the schema's. JSON Schema cannot
express them and does not try. #204 owns the diagnostics that enforce them.

`contentHash` on both `WorkflowDefinitionV1` and `ProofCatalogV1` is a lowercase-hex SHA-256
over structural fields, with prose excluded, on the rules `OntologyGraphHasher` already
applies. Documentation churn must not change the hash, because the hash exists to say when
the shape changed.

## Versioning

Every 0.13.0 addition is optional. A 0.12.0 document parses against 0.13.0 unchanged, the
two-arm schema diff reports 91 non-breaking changes, and **no allowlist entry is needed**. A
narrowing in this slice would have been a design error rather than an allowlist row.
