# Design: Strategos 2.5.0 — Polyglot Descriptors + IOntologySource + Drift Diagnostics

**Date:** 2026-05-10
**Status:** Draft (ideate output)
**Workflow:** `ontology-2-5-0-polyglot-ingestion` (feature, ideate phase)
**Scope:** Strategos-only carve-out of the cross-repo ingestion design
**Parent ADR:** `../../../basileus/docs/adrs/ontological-data-fabric.md` §8.2 (Strategos additions), §9 (merge semantics), §15.7 (polyglot descriptor contract)
**Prior design (cross-repo):** `docs/reference/2026-04-19-ingest-ontology-from-source.md`
**Prior design (already-shipped 2.5.0):** `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`
**Closes:** strategos#37, strategos#48, strategos#43
**Milestone:** Ontology 2.5.0 — Coordination Floor

---

## 1. Context and thesis

PR #59 (slices B+C) shipped the dispatch + validation halves of v2.5.0 on 2026-05-09. Three issues remain on the milestone before the Coordination Floor is closed: #37 (`IOntologySource` + provenance), #48 (polyglot descriptor schema), #43 (AONT200-series drift diagnostics). This document specifies them as a single coherent slice — they share a graph-merge surface, a descriptor-shape evolution, and a test fixture matrix.

The load-bearing constraint shaping every decision: **`Actions`, `Events`, `Lifecycle`, `InterfaceActionMappings`, `ExternalLinkExtensionPoints` are hand-authored only** (basileus ADR §9.2). The mechanical ingester is forbidden to contribute to these fields. AONT205 enforces this at graph-merge time. This invariant guarantees that the polyglot work does not need to thread `SymbolKey?` through the merged 2.5.0 dispatch and validation surfaces — those paths only operate on descriptors that carry hand-authored actions, which by invariant carry a non-null `ClrType`.

The Strategos-side surface is therefore three concrete things: (1) the descriptor schema becomes polyglot-capable, (2) ingestion arrives via a new extension point with field-level provenance, and (3) drift between hand-authored intent and ingested mechanical schema surfaces as named diagnostics at merge time.

## 2. Scope and non-goals

### In scope (Strategos 2.5.0)

- Polyglot descriptor schema on `ObjectTypeDescriptor`, `LinkDescriptor`, `PropertyDescriptor` (#48)
- `AONT037` source-generator diagnostic for `ClrType != null OR SymbolKey != null` invariant (#48)
- `IOntologySource` interface + `OntologyDelta` event vocabulary + `AddSource<T>()` DI extension (#37)
- Runtime `IOntologyBuilder.ObjectTypeFromDescriptor` / `ApplyDelta` (#37)
- `DescriptorSource` enum (`HandAuthored | Ingested`) field-level provenance on descriptors (#37)
- `AONT201` through `AONT208` graph-freeze diagnostics (#43, expanded from #43's original 6 codes per ADR §9.3)
- `AONT041` retarget from `ClrType`-keyed to `(DomainName, Name)`-keyed link-participant check
- `OntologyValidateTool.FindObjectType` retarget to descriptor-name resolution (PR #59 deferred follow-up)

### Out of scope

- `MartenOntologySource` implementation (basileus owns; consumes `IOntologySource`)
- `RoslynSourceAnalyzer`, `TypescriptLanguageFrontend`, SCIP wiring (basileus §7)
- Marten event-stream replay, ingestion service orchestration (basileus §8.3–§8.13)
- `ChunkContentHashCache`, embedding pipeline (basileus §8.5–§8.6)
- Branch-hand stream and four-input fold (`MergeFour`) — Strategos 2.5.0 ships the two-input fold (`MergeTwo`: hand + ingested); four-input arrives with branch-stream support in a later release
- Coverage gate / `IOntologyCoverageProvider` extensions (Strategos.Ontology.Query already has this; no work)
- `Strategos.Contracts` TypeSpec emit for the new types (separate concern, tracked alongside `2026-04-18-typespec-contracts-pipeline.md`)
- Multi-registered CLR types in structural links (#32) — explicitly deferred awaiting concrete use case
- README + VitePress documentation updates (#23) — synthesizes after this slice merges

### Cadence

Two PRs, sequenced:

- **PR-A: Schema + ingest** — DR-1, DR-2, DR-3, DR-4, DR-5, DR-6, DR-9, DR-10
- **PR-B: Drift diagnostics** — DR-7, DR-8

PR-A is shippable in isolation: it unblocks basileus to start implementing `MartenOntologySource` against a stable contract. PR-B follows once PR-A merges, since AONT201–AONT208 require descriptors with mixed provenance to test against.

## 3. Architecture overview

The polyglot descriptor schema is a field-level evolution of the existing `ObjectTypeDescriptor` record — `ClrType` becomes nullable, joined by `SymbolKey`, `SymbolFqn`, and `LanguageId`. The lattice rule from basileus ADR §9.2 governs which origin wins per field:

```
ObjectTypeDescriptor (polyglot)
  ├── ClrType:      Type?       — hand-main wins; null iff descriptor is purely-ingested
  ├── SymbolKey:    string?     — ingested wins (SCIP moniker is mechanical truth)
  ├── SymbolFqn:    string?     — ingested wins
  ├── LanguageId:   string      — hand-main wins; default "dotnet" for hand-authored
  ├── Source:       DescriptorSource  — record-level provenance: HandAuthored | Ingested
  ├── Properties:   Set<PropertyDescriptor>  — per-name union; each carries Source
  ├── Links:        Set<LinkDescriptor>      — per-name union; each carries Source
  ├── Actions:      ImmutableArray<ActionDescriptor>  — HAND-ONLY (AONT205)
  ├── Events:       ImmutableArray<EventDescriptor>   — HAND-ONLY (AONT205)
  └── Lifecycle:    LifecycleDescriptor?     — HAND-ONLY (AONT205)

OntologyBuilder pipeline:
  IOntologySource.LoadAsync     →  OntologyDelta[]
                                       ↓
                                 IOntologyBuilder.ApplyDelta
                                       ↓
                                 Two-input fold (MergeTwo, ADR §9.1)
                                       ↓
                                 Graph-freeze with AONT200-series checks
                                       ↓
                                 OntologyGraph (consumed by PR #59 surfaces)
```

Runtime paths shipped in PR #59 (`IActionDispatcher.*`, `EstimateBlastRadius`, `DetectPatternViolations`, `GetActionConstraintReport`) key descriptors by `(DomainName, Name)` and observe `descriptor.Actions` for dispatch decisions. Because `Actions` is hand-only, those paths only see descriptors with `ClrType != null` — no polyglot composition cost at runtime.

## 4. Design requirements

### DR-1: Polyglot descriptor schema

**Statement:** `ObjectTypeDescriptor`, `LinkDescriptor`, and `PropertyDescriptor` accept polyglot identity. `ClrType` becomes nullable. `SymbolKey`, `SymbolFqn`, and `LanguageId` are added with documented semantics per basileus ADR §8.2.5.

**Shape:**

```csharp
public sealed record ObjectTypeDescriptor
{
    public required string Name { get; init; }
    public required string DomainName { get; init; }

    public Type? ClrType { get; init; }
    public string? SymbolKey { get; init; }
    public string? SymbolFqn { get; init; }
    public string LanguageId { get; init; } = "dotnet";

    public DescriptorSource Source { get; init; } = DescriptorSource.HandAuthored;
    public string? SourceId { get; init; }
    public DateTimeOffset? IngestedAt { get; init; }

    // ... existing properties unchanged
}

public enum DescriptorSource { HandAuthored, Ingested }
```

`LinkDescriptor.TargetType`/`ParentType` and any `PropertyDescriptor` `Type` references receive the same treatment: optional CLR-type, optional symbol-key, exclusive.

**Acceptance criteria:**
- `ObjectTypeDescriptor.ClrType` is nullable; default constructor of new field set throws `InvalidOperationException` if `ClrType` and `SymbolKey` are both null at construction.
- `LinkDescriptor.TargetTypeName` already exists (PR #59); `LinkDescriptor.TargetSymbolKey` is added as a parallel optional field.
- `PropertyDescriptor` reference-property fields accept `string? SymbolKey` alongside existing `Type? ReferenceType`.
- Existing `DomainOntology` subclasses across `Strategos.Ontology.Tests`, `Strategos.Ontology.MCP.Tests`, and the trading sample compile and run unchanged with `LanguageId = "dotnet"` default.
- 666 existing tests in `Strategos.Ontology.Tests` continue to pass; 121 in `Strategos.Ontology.MCP.Tests` continue to pass.

### DR-2: AONT037 source-generator diagnostic

**Statement:** A new source-generator diagnostic fires when a hand-authored `DomainOntology.Define()` produces an `ObjectTypeDescriptor` with both `ClrType == null` and `SymbolKey == null`. Severity Error.

**Trigger:** `OntologyDefinitionAnalyzer` walks `Define()` method bodies; for each `obj.ObjectType<T>()` or `obj.ObjectType(name, ...)` call, verify the resulting descriptor satisfies the invariant. The hand-authored DSL always carries `T` (so `ClrType` is implied non-null) — AONT037 catches the case where the descriptor-by-name overload is used without supplying a `SymbolKey`.

**Acceptance criteria:**
- Diagnostic registered in `OntologyDefinitionAnalyzer` with identifier `AONT037`, severity Error.
- Positive test: `Define()` calling `obj.ObjectType("Foo", domainName: "Trading")` without `SymbolKey` produces AONT037 with a fix-suggestion message naming both options.
- Negative test: `Define()` calling `obj.ObjectType<TradeOrder>()` does not produce AONT037.
- Diagnostic documentation in `docs/reference/ontology-diagnostics.md` (or equivalent) updated.

### DR-3: `IOntologySource` extension point

**Statement:** `Strategos.Ontology.IOntologySource` is a new public interface enabling ontology graph contributions from sources beyond hand-authored `DomainOntology.Define()`. Registered via DI: `options.AddSource<MartenOntologySource>()`.

**Shape:**

```csharp
namespace Strategos.Ontology;

public interface IOntologySource
{
    /// <summary>Stable identifier; tags provenance and conflict diagnostics.</summary>
    string SourceId { get; }

    /// <summary>Replays the source's full state as a stream of deltas. Called once at startup.</summary>
    IAsyncEnumerable<OntologyDelta> LoadAsync(CancellationToken ct);

    /// <summary>Subscribes to incremental updates. Empty for static sources.</summary>
    IAsyncEnumerable<OntologyDelta> SubscribeAsync(CancellationToken ct);
}
```

`OntologyGraphBuilder` drains `LoadAsync` from each registered source at construction. `SubscribeAsync` is a v2.5.0 surface — Strategos ships the consumer but does not wire live invalidation in this slice; consumers may complete the async enumerable immediately. Live invalidation lands when basileus exercises the surface.

**Acceptance criteria:**
- `IOntologySource` defined in `Strategos.Ontology` namespace, public.
- `OntologyBuilderOptions.AddSource<T>()` extension method registers `T : IOntologySource` as transient.
- `OntologyGraphBuilder.Build()` drains all registered sources' `LoadAsync` before returning the graph.
- A `TestOntologySource` test fixture exists in `Strategos.Ontology.Tests.TestInfrastructure`.
- An integration test exercises two sources contributing to the same `ObjectType` with non-overlapping fields; both contributions appear in the composed graph with correct `DescriptorSource`.

### DR-4: `OntologyDelta` event vocabulary

**Statement:** `OntologyDelta` is a sealed abstract record with eight concrete variants covering object-type, property, and link granularity. Per basileus ADR §8.2.2.

**Variants:**

```csharp
public abstract record OntologyDelta
{
    public required string SourceId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public sealed record AddObjectType(ObjectTypeDescriptor Descriptor) : OntologyDelta;
    public sealed record UpdateObjectType(ObjectTypeDescriptor Descriptor) : OntologyDelta;
    public sealed record RemoveObjectType(string DomainName, string TypeName) : OntologyDelta;
    public sealed record AddProperty(string DomainName, string TypeName, PropertyDescriptor Descriptor) : OntologyDelta;
    public sealed record RenameProperty(string DomainName, string TypeName, string FromName, string ToName) : OntologyDelta;
    public sealed record RemoveProperty(string DomainName, string TypeName, string PropertyName) : OntologyDelta;
    public sealed record AddLink(string DomainName, string SourceTypeName, LinkDescriptor Descriptor) : OntologyDelta;
    public sealed record RemoveLink(string DomainName, string SourceTypeName, string LinkName) : OntologyDelta;
}
```

Mechanical ingester is forbidden from constructing `Add`/`Update` deltas whose descriptors contain `Actions`, `Events`, or `Lifecycle` — this is the AONT205 invariant. Validation occurs at delta-apply time, not at delta-construction time.

**Acceptance criteria:**
- All eight variants defined as sealed records under `OntologyDelta`.
- `Strategos.Ontology.Tests.OntologyDeltaTests` exercises each variant's construction + round-trip equality.
- `RenameProperty` is a single delta, not `RemoveProperty + AddProperty` — preserves rename identity through the matcher.

### DR-5: Runtime `IOntologyBuilder` API

**Statement:** `IOntologyBuilder` exposes two new methods enabling delta application without the expression-tree DSL.

**Shape:**

```csharp
public interface IOntologyBuilder
{
    // ... existing expression-tree DSL methods unchanged

    /// <summary>Adds an ObjectType from a fully-specified descriptor. Bypasses the expression-tree DSL.</summary>
    void ObjectTypeFromDescriptor(ObjectTypeDescriptor descriptor);

    /// <summary>Applies an OntologyDelta against the current builder state.</summary>
    void ApplyDelta(OntologyDelta delta);
}
```

The DSL path remains the supported entry point for `DomainOntology.Define()` subclasses. `ObjectTypeFromDescriptor` is the mechanism `IOntologySource` contributions reach the graph — necessary because ingested types may only be known by `SymbolKey`, with no loaded CLR type.

**Acceptance criteria:**
- Both methods are non-virtual on the public interface; existing implementations get them via default interface methods where backward compat matters.
- `ApplyDelta` dispatches by delta variant; unknown variants throw `NotSupportedException`.
- Each delta variant has a positive test exercising the apply path.
- Adding an ObjectType with `Source = Ingested` and a non-empty `Actions` array throws `OntologyCompositionException` with message naming AONT205.

### DR-6: Field-level provenance metadata

**Statement:** Each descriptor type carries a `DescriptorSource Source { get; init; }` property. `PropertyDescriptor` and `LinkDescriptor` carry per-field provenance, not per-parent — a single `ObjectTypeDescriptor` can have hand-authored `Properties` and ingested-added `Properties`, each tagged separately.

**Merge semantics (two-input fold, MergeTwo):**

```csharp
ObjectTypeDescriptor MergeTwo(ObjectTypeDescriptor hand, ObjectTypeDescriptor ingested)
{
    return new ObjectTypeDescriptor
    {
        Name = hand.Name,                          // mismatch is AONT006
        DomainName = hand.DomainName,
        ClrType = hand.ClrType ?? ingested.ClrType,        // hand wins, fallback to ingested
        SymbolKey = ingested.SymbolKey ?? hand.SymbolKey,  // ingested wins (SCIP authoritative)
        SymbolFqn = ingested.SymbolFqn ?? hand.SymbolFqn,
        LanguageId = hand.LanguageId,
        Source = DescriptorSource.HandAuthored,             // record-level: hand wins on composition
        Properties = MergeProperties(hand.Properties, ingested.Properties),
        Links = MergeLinks(hand.Links, ingested.Links),
        Actions = hand.Actions,                             // INTENT — hand only
        Events = hand.Events,                               // INTENT — hand only
        Lifecycle = hand.Lifecycle,                         // INTENT — hand only
    };
}
```

Per-property merge: hand wins on conflict, ingested-only properties added with `Source = Ingested`. Same for links.

**Acceptance criteria:**
- `DescriptorSource` enum present; default value `HandAuthored`.
- `PropertyDescriptor.Source` and `LinkDescriptor.Source` properties exist, default `HandAuthored`.
- `MergeTwo` implementation in `OntologyGraphBuilder` follows the lattice rule from basileus ADR §9.1.
- Test matrix: hand-only, ingested-only, hand-overrides-ingested, ingested-adds-to-hand, conflict-resolves-to-hand, identity-fields-from-ingested.
- Field-level provenance accessible via `ObjectTypeDescriptor.GetPropertyProvenance(name)` helper.

### DR-7: AONT200-series graph-freeze diagnostics

**Statement:** Eight new diagnostics fire at `OntologyGraphBuilder.Build()` based on observable conditions in the composed graph. Per basileus ADR §9.3.

**Catalog:**

| ID | Trigger | Severity |
|---|---|---|
| AONT201 | Hand-declared property does not exist on ingested descriptor | Error |
| AONT202 | Hand-declared property type mismatches ingested | Warning |
| AONT203 | Ingested-only property missing from hand `Define()` when `[DomainEntity(Strict = true)]` | Warning |
| AONT204 | Ingested type not referenced by any hand-authored `Define()` | Info |
| AONT205 | Mechanical ingester contributed to intent-only field (`Actions`/`Events`/`Lifecycle`) | Error |
| AONT206 | Hand-declared property also ingested mechanically — opt-in cleanup hint | Info (off by default) |
| AONT207 | Branch-hand vs main-hand property conflict — **deferred** (requires four-input fold) | — |
| AONT208 | `LanguageId` disagreement between origins | Error |

AONT207 is named here for vocabulary alignment with basileus ADR but is not implemented in this slice (four-input fold is out of scope per §2). The diagnostic registration is present; the trigger is unreachable until branch-hand stream support lands.

**Acceptance criteria:**
- All eight diagnostics registered in `OntologyDefinitionAnalyzer` and `OntologyGraphBuilder` per applicability (compile-time vs graph-freeze).
- AONT201–AONT206, AONT208 each have positive and negative tests; AONT207 has a registration test only with a `[Skip("requires four-input fold")]` placeholder.
- AONT201's exception/diagnostic message names the offending property + suggests `Pass-6b rename matcher may have missed this` when context allows.
- AONT203 fires only when the `[DomainEntity(Strict = true)]` opt-in is set.
- AONT206 is gated by MSBuild property `OntologyEnableHygieneHints` (default false).

### DR-8: AONT041 retarget

**Statement:** `AONT041 MultiRegisteredTypeInLink` currently keys its multi-registration check by `ClrType`. With polyglot descriptors, this produces false negatives when a multi-registered type is participated in a link via `SymbolKey`-only descriptor. Retarget to `(DomainName, Name)` keying.

**Acceptance criteria:**
- AONT041 lookup uses descriptor-name + domain-name composite key.
- Regression test: hand-authored multi-registration of `TradeOrder` as `"orders"` + `"open_orders"` participating in a link still trips AONT041 (existing behavior preserved).
- New test: ingested descriptor with `SymbolKey="scip-typescript ./mod#User"` registered twice (e.g., once per workspace) participating in a hand-authored link trips AONT041.
- `OntologyValidateTool.FindObjectType` retargets to descriptor-name resolution (closes PR #59 deferred follow-up: "domain-agnostic; cross-domain name collisions could silently resolve").

### DR-9: Test fixture infrastructure

**Statement:** `Strategos.Ontology.Tests` gains a `TestOntologySource` for fast synthetic deltas and one Roslyn-based integration test verifying real `SymbolKey.ToString()` round-trip.

**Test infrastructure:**

```csharp
namespace Strategos.Ontology.Tests.TestInfrastructure;

public sealed class TestOntologySource : IOntologySource
{
    public required string SourceId { get; init; }
    public required ImmutableArray<OntologyDelta> Deltas { get; init; }

    public async IAsyncEnumerable<OntologyDelta> LoadAsync([EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var delta in Deltas) { ct.ThrowIfCancellationRequested(); yield return delta; }
    }

    public async IAsyncEnumerable<OntologyDelta> SubscribeAsync([EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield(); yield break;
    }
}
```

**Integration test:** One new test using `Microsoft.CodeAnalysis.CSharp` to compile a small `.cs` snippet, extract an `INamedTypeSymbol`, serialize via `SymbolKey.Create(symbol).ToString()`, and validate that an `ObjectTypeDescriptor` built from that `SymbolKey` round-trips through the runtime builder + graph-freeze without losing identity.

**Acceptance criteria:**
- `TestOntologySource` exists in test infrastructure; unit/merge/diagnostic tests use it.
- `Strategos.Ontology.Tests.csproj` adds `Microsoft.CodeAnalysis.CSharp` as a test-only dependency (PrivateAssets="all").
- One Roslyn integration test passes; it does not run when `SkipRoslynIntegrationTests` MSBuild property is set (CI escape hatch).
- Test count delta: ~80 new tests across PR-A + PR-B (synthetic) + 1 Roslyn integration test.

### DR-10: Error handling, failure modes, edge cases

**Statement (mandatory per ideate skill):** The slice must specify behavior under three failure modes: (i) source raises during `LoadAsync`, (ii) delta application fails the AONT205 invariant, (iii) graph-freeze produces an error-severity diagnostic.

**Failure mode 1 — Source raises during `LoadAsync`:**
`OntologyGraphBuilder.Build()` propagates the exception with `SourceId` in the message. No partial graph is produced. Other sources do not run.

**Failure mode 2 — Delta apply violates AONT205:**
`IOntologyBuilder.ApplyDelta` throws `OntologyCompositionException` with diagnostic identifier `AONT205`, the offending field name, and `SourceId`. The builder is poisoned (no further deltas accepted on that builder instance); `Build()` will throw.

**Failure mode 3 — Error-severity diagnostic during graph-freeze:**
`OntologyGraphBuilder.Build()` aggregates all Error-severity diagnostics (AONT201, AONT205, AONT208) and throws `OntologyCompositionException` with all findings in `Diagnostics: ImmutableArray<OntologyDiagnostic>`. Warnings (AONT202, AONT203) and Info (AONT204, AONT206) are logged via `ILogger<OntologyGraphBuilder>` and surfaced on the returned graph as `OntologyGraph.NonFatalDiagnostics`.

**Acceptance criteria:**
- Test: `TestOntologySource` that yields-and-throws produces an exception whose message contains the `SourceId`.
- Test: applying an ingested `Add` delta with `Actions.Count > 0` throws `OntologyCompositionException` with `Diagnostic.Id == "AONT205"`.
- Test: a graph with one AONT201 + one AONT202 + one AONT204 throws on Build(); thrown exception's `Diagnostics` contains the AONT201 only; the AONT202 and AONT204 are accessible via the exception's `NonFatalDiagnostics` property for telemetry purposes.
- Logging at `LogLevel.Warning` for AONT202/AONT203 with structured properties (`DiagnosticId`, `DomainName`, `TypeName`, `PropertyName`).

## 5. Open questions

1. **Should `PropertyDescriptor.Source` be on the descriptor itself or on a parallel `ObjectTypeDescriptor.PropertyProvenance: ImmutableDictionary<string, DescriptorSource>` map?** Recommendation: on the descriptor (simpler, no parallel-map drift risk). Open for review challenge.

2. **`OntologyGraph.NonFatalDiagnostics` shape — `ImmutableArray<OntologyDiagnostic>` or richer?** Suggest matching `ConstraintViolationReport`'s shape from PR #59 for consistency.

3. **How does `IOntologyVersionedCache` (`OntologyGraph.Version` from #44) compose with `SubscribeAsync` for live invalidation in v2.6.0?** Out of scope here but the design should not foreclose it. Current proposal: `OntologyGraphBuilder.RebuildAsync` consumes `SubscribeAsync` deltas and bumps the version hash; cache consumers invalidate via existing `_meta.ontologyVersion` envelope.

## 6. Risks and trade-offs

| Risk | Mitigation |
|---|---|
| Field-level provenance on `PropertyDescriptor`/`LinkDescriptor` increases descriptor record size; many call sites construct these in tests | Default `Source = HandAuthored` preserves existing test ergonomics; `with { Source = Ingested }` is the explicit opt-in for new ingested paths |
| AONT201 firing rate may be high when basileus first runs against an existing trading ontology that has drifted | Acceptable; AONT201 is the load-bearing diagnostic for the inversion thesis and is expected to fire on first integration — that is the value it delivers. Document in basileus rollout runbook |
| Two-PR cadence (PR-A → PR-B) leaves a window where ingested types can be added to the graph but drift diagnostics are silent | PR-A explicitly logs `LogLevel.Information` events on every ingested-with-mismatch case so the diagnostic gap is observable. PR-B converts those to graph-freeze errors |
| `Microsoft.CodeAnalysis.CSharp` test dependency adds ~5MB to test restore | Acceptable; gated behind PrivateAssets and the `SkipRoslynIntegrationTests` MSBuild property |

## 7. Cross-references

- **basileus ADR:** `../../../basileus/docs/adrs/ontological-data-fabric.md` — §8.2 (Strategos additions), §9 (merge semantics), §15.7 (polyglot descriptor contract in Strategos.Contracts)
- **Prior cross-repo design:** `docs/reference/2026-04-19-ingest-ontology-from-source.md`
- **Prior 2.5.0 dispatch+validation design:** `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`
- **PR #59 deferred follow-ups (this slice closes):** `OntologyValidateTool.FindObjectType` cross-domain resolution
- **#31 multi-registration design:** `docs/designs/2026-04-08-ontology-descriptor-name-dispatch.md` (AONT041 origins)
- **GitHub issues:** strategos#37, strategos#48, strategos#43
- **Deferred to follow-up:** strategos#32 (multi-registered link participants, awaits use case), strategos#23 (docs)

## 8. axiom:design dimensional summary

This design was constrained by `axiom:design` DIM-1..DIM-8 during ideation. Notable applications:

- **DIM-1 Topology:** Every shared resource has a single source-of-truth path. `OntologyGraphBuilder` is the single composition root for source merging. No silent fallbacks: missing `IOntologySource` is not an error; failing `IOntologySource.LoadAsync` is a startup error.
- **DIM-2 Observability:** All diagnostics carry structured identifiers (AONT201–AONT208, AONT037). Non-fatal diagnostics surface via `ILogger` + `OntologyGraph.NonFatalDiagnostics`. No silent catches.
- **DIM-3 Contracts:** Changes to PR #59's already-shipped surface (`IOntologyQuery.*`, `IActionDispatcher.*`) are zero. The descriptor schema evolution is opt-in via nullable fields with documented defaults; existing hand-authored ontologies compile unchanged.
- **DIM-4 Test Fidelity:** `TestOntologySource` matches production source wiring (same `IOntologySource` interface); the one Roslyn integration test guards against `SymbolKey.ToString()` divergence between Strategos test fixtures and basileus production ingester.
- **DIM-5 Hygiene:** Two-PR cadence avoids dead code in PR-A (no diagnostic-shape placeholders). AONT207 is named for vocabulary alignment but explicitly unreachable until four-input fold ships.
- **DIM-6 Architecture:** Polyglot composition does not ripple through merged 2.5.0 surfaces; dispatch + validation paths remain descriptor-name-keyed. Single responsibility per module preserved.
- **DIM-7 Resilience:** Graph-freeze is bounded by `MaxExpansionDegree` (existing from PR #59). `LoadAsync` exceptions are loud, not silent. No new caches in this slice.
- **DIM-8 Prose:** Diagnostic messages name specific identifiers (property names, type names, source IDs), not generic categories.

No paired invariants skill detected. Run `/axiom:scaffold-invariants` to create one.
