# Strategos.Contracts — Cross-Product Schema Substrate (0.2.0)

**Date:** 2026-05-24
**Status:** Design — pending plan
**Owner:** `LevelUp.Strategos.Contracts` companion package
**Milestone:** Strategos 2.8.0 — Cross-product schema substrate (slice A)
**Tracking:** #36 (events), #50 (workflow IR), #98 (invariant schema, fast-follow)
**Supersedes:** `docs/designs/2026-04-18-typespec-contracts-pipeline.md` (events-only; predates the workflow-IR dual-representation finding and the #98 family)
**Invariants applied:** `/strategos-design-invariants` (INV-1, INV-5, INV-6, INV-7, INV-8) · `/axiom:design` (DIM-1, DIM-3, DIM-5, DIM-6, DIM-8)

## Problem

Three vocabularies that cross the Strategos ↔ Basileus ↔ Exarchos boundary are each maintained twice, in two languages, with hand-written sync points that drift (a DIM-3 divergent-instances risk):

1. **SDLC events** — 77 Zod types in Exarchos (`event-store/schemas.ts`) and 20+ C# `ISdlcEvent` records in Basileus, bridged by a hand-written `ExarchosEventDto`.
2. **Workflow IR** — the serialized shape of a workflow, which Exarchos's Workflow Builder SDK (exarchos v3.x, #1258) needs to validate against.
3. **Invariant catalog** — Exarchos ships hand-written Zod for `CheckNode`/`Enforcement`/`InvariantEntry`, explicitly authored "in the shape this contract will emit" (#98 seam doc).

This release stands up `LevelUp.Strategos.Contracts`: TypeSpec is the single canonical source; the build emits JSON Schema (language-neutral, embedded as NuGet content) and C# records (compiled into the DLL). Basileus consumes the DLL; Exarchos derives Zod from the JSON Schema. The package **debuts at 0.2.0** with all three families — no 0.1.0 ships.

## Dependency direction (DIM-1, INV fixed by issue)

```
            Strategos.Contracts  ◄── Strategos (core DSL)
                  ▲     ▲
                  │     │
            Exarchos   Basileus
        (derives Zod)  (refs the DLL)
```

Neither consumer depends on the other. The contract never depends on a consumer's release cadence. `Strategos.Contracts` versions in lockstep with `Strategos` (same repo, same pipeline). This is a one-way fan-in: any cycle here is a design defect.

## The load-bearing finding: the workflow IR has two shapes, not one

Issue #50 specifies "18 generated records 1:1 with `src/Strategos/Definitions/*.cs`, delete the hand-authored files." **This is not achievable as written**, and the design hinges on why. The hand-authored builder IR plays a role TypeSpec-generated POCOs structurally cannot:

| In `src/Strategos/Definitions/` today | Why it can't be a generated wire record |
|---|---|
| `WorkflowDefinition<TState> where TState : class, IWorkflowState` | TypeSpec emits **concrete** types. The generic root type cannot be generated at all. |
| `StepDefinition.StepType : System.Type` | A live CLR handle the Roslyn SG reads to emit saga classes (**INV-1**). JSON carries a string moniker, not a `Type`. |
| `StepDefinition.LambdaDelegate : Delegate` | A compiled function. It **cannot serialize** — a lambda-bearing workflow has no faithful JSON body. |
| `Create()` / `With*()` + `ArgumentException.ThrowIfNullOrWhiteSpace` | **INV-5 tier-1** builder-runtime validation living *on* the records. Deleting them deletes a validation tier. |

So one record cannot be both "the workflow you build and run" (generic, CLR-typed, behavioral) and "the workflow you serialize and share" (a flat, language-neutral document). They are genuinely two shapes. Conflating them is what makes #50's acceptance criteria infeasible. Events (#36) and invariant models (#98) have no such tension — they are pure data and generate cleanly. **The dual-representation problem is isolated to the workflow-IR leg.**

## Solution — Approach B: dual representation + test-pinned projection

Keep the behavioral builder IR exactly as-is. Add a *separate* generated wire schema. Bridge them with one hand-authored projection whose fidelity is pinned mechanically by the #53 fixture corpus.

```
  Builder DSL  (UNTOUCHED — hand-authored, generic)
  Workflow<TState>.StartWith<A>().Then<B>()...
        │ produces
        ▼
  WorkflowDefinition<TState>          ── Role 1: in-memory build product
   • generic · System.Type · Delegate    • SG reads StepType (INV-1) ✔
   • Create/With*/validation (INV-5) ✔    • runtime executes lambdas ✔
        │
        │  ToContract()   ◄── NEW projection (hand-authored, fixture-pinned)
        ▼
  WorkflowDefinitionV1                ── Role 2: serializable wire document
   • POCO · stepType: string moniker      • GENERATED from TypeSpec
   • lambda → marker, never code          • → JSON Schema + C# record + Zod
   • schemaVersion: "1.0" literal          • the artifact #36/#50/#53/#98 ship
```

`WorkflowDefinitionV1` is the **single source of truth for the wire contract**; both consumers derive from it. `WorkflowDefinition<TState>` remains the single source of truth for *execution*. The projection is the one seam between them, and it is the only place that can drift — so the whole design rests on making that drift mechanically detectable, not a matter of discipline (see Resilience).

## Load-bearing decision LB-1 — Lambda steps: the wire contract never carries code

A step defined via `CreateFromLambda` holds a `Delegate` that cannot serialize. The projection maps it to a `delegate`-kind step (one of #50's discriminated kinds: `skill | handler | gate | delegate | approval`) carrying its `stepName`/`instanceName` and a `lambda: true` marker — **structure preserved, body dropped, and the loss made visible in the data** rather than silently elided.

This is not a workaround; it is the contract's semantic boundary. The wire IR describes workflow *structure* (for validation, visualization, cross-product schema conformance, breaking-change diffing) — it is not an execution-reconstruction format. Dropping the body is therefore *correct* for the contract's purpose, not lossy relative to its purpose.

Critically, this unifies with #98's INV-4 requirement that `CheckNode` "cannot express an embedded-executable leaf." The governing principle across **all three** families becomes one sentence: **the contract is declarative-only; it never serializes executable code.** A reader who internalizes that rule predicts the shape of every family. That is the DIM-8 (prose/conceptual-integrity) payoff of stating it once and applying it everywhere.

## Load-bearing decision LB-2 — Identity & round-trip: export-only, language-neutral moniker

`StepDefinition.StepType : System.Type` projects to `stepType : string`. The moniker is the **simple type name** (`StepType.Name`), not the assembly-qualified name. Rationale (DIM-3): the contract is consumed by TypeScript; an assembly-qualified .NET path leaks runtime specifics into a language-neutral schema and couples Exarchos to Strategos's assembly layout. The simple name is sufficient for every milestone consumer (Zod validation, fixture corpus, visualization, schema diff) and aligns with how the builder already derives phase identity from `StepName`/`InstanceName`.

The projection is **one-way (`ToContract()`) for 0.2.0.** Rehydration (`FromContract()` → a runnable `WorkflowDefinition<TState>`) is explicitly **deferred**, because it is genuinely harder and **not needed by this milestone's consumers** — none execute Strategos workflows *from* JSON; they validate, visualize, and diff the JSON. A future bidirectional V-next would require: (a) a moniker→`System.Type` resolver registry (the simple-name moniker is intentionally resolver-friendly), and (b) a lambda re-binding story (markers cannot recover code). The design **reserves** that space — the moniker scheme and the `lambda` marker are forward-compatible with it — without paying for it now. INV-8 note: the moniker is identity-by-name, parallel to the ontology's `SymbolKey`/`ClrType` polyglot stance; the wire layer commits to neither CLR reflection nor a single language.

## The three schema families (model breadth)

Scope confirmed: **core envelope + new ADR §4 families + in-repo consolidation.** Absorbing the full 77 Exarchos + 20 Basileus type inventory is left to the consumer-migration issues (basileus#152, exarchos#1125), which reference this package as source-of-truth.

**Family 1 — SDLC events (#36).** `SdlcEventEnvelope` (`streamId`, `sequence: int32`, `timestamp`, `type` discriminator, optional correlation/causation/agent fields, `source: exarchos | basileus`, `schemaVersion`, `data`). Forward-compatible: unknown `type` values are logged, never rejected. New ADR §4 families: ontological-record lifecycle (`IntentProposedData`, `IntentEnrichedData`, `IntentCompletedData`, `OntologicalRecordData`, `RecordStatus`, `DelegationPolicy`), fabric-query audit (`FabricQueryData`, `FabricQueryType`), remote delegation (`TaskDelegatedRemoteData`, `CrossTierDependencyResolvedData`). `NotificationEnvelope`/`FormattedNotification` are **excluded** (ADR §2.3.4) — enforced by a regression test, not a comment.

**Family 2 — Workflow IR (#50).** `WorkflowDefinitionV1` + the discriminated `StepDefinition` and the structural sub-definitions, as the *wire* schema (per LB-1/LB-2). `schemaVersion: "1.0"` literal at the IR root; future minors additive-only; breaking changes require V2. `StepDefinition.runtime: "exarchos" | "strategos" | "remote"` reserved (optional, default `"exarchos"`) for v3.3.0 federation — forward-compat slot, no behavior now.

**Family 3 — Invariant catalog (#98, fast-follow).** `CheckNode` (recursive combinator tree: `grep`/`structural`/`heuristic` leaves + `all-of`/`any-of`/`not`/`scope` arms, **no executable member** per LB-1), `Enforcement` (discriminated on `mode`: `check` → `CheckNode` | `audit` → prompt string), `InvariantEntry` v3 (v2 fields + additive optional v3 fields). Uses `@encodedName` to preserve **kebab-case wire names** so the generated decoder validates Exarchos's YAML frontmatter verbatim. Exercises emitter recursion + discriminated unions + wire-name encoding — broadening pipeline coverage without touching the migration surface.

## Build pipeline & C# emission (DIM-6, INV-6, INV-7)

```
src/Strategos.Contracts/
  Strategos.Contracts.csproj
  Events/     *.tsp        Workflow/   *.tsp        Diagnostics/  *.tsp   ← canonical
  tspconfig.yaml          (emitters: @typespec/json-schema + C#)
  schemas/                (emitted JSON Schema — embedded NuGet content)
  Generated/              (emitted C# records — compiled into the DLL)
```

**C# emitter: TypeSpec native, NJsonSchema fallback (decision-gated).** The generated records are consumer-facing contracts and must satisfy INV-6 (`sealed record`) and INV-7 (`{ get; init; }`, collections as `IReadOnlyList<T>`). The native TypeSpec C# emitter is preferred; **the plan must include a spike task that proves it can emit sealed/init-only/`IReadOnlyList` records before committing.** If it cannot hit that shape, fall back to NJsonSchema-from-JSON-Schema with a template tuned to the same shape. This is a first-class decision gate, not an afterthought — a generated record that defaults to mutable `class` with `{ get; set; }` would silently violate INV-6/INV-7 the moment it shipped.

**Spike-known issues** (from `spikes/typespec-contracts/`, all already mitigated there): `$ref` needs dereferencing (`@apidevtools/json-schema-ref-parser`) before Zod generation; `int64`→`string` avoided by using `int32` for `sequence`; `model` reserved word handled via `@encodedName`.

## Hygiene & migration (DIM-5, the #50 swap correction)

The workflow-IR leg does **not** delete `src/Strategos/Definitions/*.cs` — they are the builder, never a wire contract, and (per the finding) cannot be generated. Instead:

- The generated `WorkflowDefinitionV1` and the projection are **net-new** alongside the untouched builder IR.
- The single-source guarantee is **on the wire contract**: TypeSpec is the only authored source for `WorkflowDefinitionV1`; CI's codegen-guard fails if `Generated/*.cs` or `schemas/*.json` are hand-edited (DIM-6: generated output is emitter-owned).
- **#50's acceptance criteria must be amended** (recorded here; GitHub edit pending the user's go-ahead): replace "delete `Definitions/*.cs`, grep zero hits" with "the wire contract is single-sourced from TypeSpec; projection-to-builder-IR fidelity is gated by the #53 round-trip fixtures." The consumer-facing contract is still 100% single-sourced — only the false premise that the builder *is* the wire contract is corrected.

## Resilience — the equivalence gate (DIM-7, no hand-wavy mitigation)

The projection is the design's only drift surface, so its correctness is enforced mechanically, never by convention:

1. **#53 fixture export** runs ≥100 builder cases through `ToContract()` and writes JSON. Every fixture **must validate against the generated JSON Schema** — a schema/projection disagreement fails CI.
2. **Cross-product round-trip**: an emitted fixture must parse against Exarchos's generated Zod; an Exarchos-emitted IR must validate against our schema (coordinated with exarchos#1247).
3. **Schema versioning (INV-5 analogue)**: `schemaVersion` literal at each root; additive-only minors; breaking change ⇒ major bump. Breaking-change detection via JSON Schema structural diff in CI.
4. **C# round-trip**: generated event records validate against existing Basileus `ISdlcEvent` shapes (validation, not migration — migration is basileus#152).

If the projection and the schema ever disagree, the build is red before either consumer sees it.

## Out of scope

- T4 cross-runtime dispatch / saga compensation across event stores (deferred to a later milestone).
- `FromContract()` rehydration to a runnable workflow (deferred; space reserved per LB-2).
- Full 77 Exarchos + 20 Basileus type absorption (consumer-migration issues).
- Consumer-side codegen swaps (basileus#152, exarchos#1125, exarchos#1247).

## Invariant conformance summary

| Invariant | Posture |
|---|---|
| **INV-1** (SG → Wolverine+Marten) | Builder IR + `StepType: System.Type` untouched; SG input contract preserved. ✔ |
| **INV-5** (stable diagnostic IDs) | Tier-1 validation stays on builder records; `schemaVersion`+additive-minors is the cross-product analogue. ✔ |
| **INV-6** (sealed-by-default) | Decision-gated: emitter must produce `sealed record` (spike-verified). ✔ |
| **INV-7** (immutable state) | Emitter must produce `{ get; init; }` + `IReadOnlyList`; gated. ✔ |
| **INV-8** (polyglot identity) | Wire moniker is identity-by-name, not CLR-reflection-bound. ✔ |

## Related

- `docs/designs/2026-04-18-typespec-contracts-pipeline.md` (superseded prior art)
- `docs/designs/2026-04-18-strategic-framing-exarchos-basileus.md` (product boundaries)
- Spike: `spikes/typespec-contracts/`
- exarchos#1258 (Workflow Builder SDK epic), exarchos#1247 (cross-product round-trip), basileus#152 (Basileus consumption)
