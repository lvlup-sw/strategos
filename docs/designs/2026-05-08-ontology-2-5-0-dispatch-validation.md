# Design: Ontology 2.5.0 — Dispatch Guarantees + Validation Surface

**Date:** 2026-05-08
**Status:** Draft (ideate output)
**Workflow:** `ontology-2-5-0-dispatch-validation` (feature, ideate phase)
**Milestone:** Ontology 2.5.0 — Coordination Floor (Slices B + C)
**Closes:** strategos#39, strategos#38, strategos#42, strategos#41
**Partially closes:** strategos#33 (Findings 1, 2, 4 — Finding 3 deferred to #32)
**Predecessor:** Slice A (#40 + #44, merged 2026-05-07 via #49 + #55)
**Parent ADR:** `lvlup-sw/basileus:docs/adrs/ontological-data-fabric.md` §§12.1–12.4
**Related:** strategos#37 (Slice D — follows this unit); strategos#48 (polyglot descriptor — Slice D prereq)

---

## 1. Context and thesis

Slice A ([#49], [#55]) landed the MCP descriptor and `OntologyGraph.Version` surface. External clients (Cursor, Copilot, Codex) can now negotiate the spec, and `_meta.ontologyVersion` flows on every response. What Slice A advertises about the tools, however, is not yet enforceable in the dispatch path: any dispatch can mutate, any failure returns free-text `Error`, and there is no observer hook for fabric telemetry. Symmetrically, the validation surface that Basileus's `intent.validated` lifecycle gate depends on (`ontology_validate`) does not exist, and `IOntologyQuery` lacks the blast-radius and pattern-violation primitives that tool would compose.

This design closes the dispatch + validation halves of the v2.5.0 coordination floor in a single feature branch. The two halves share a vocabulary — `ConstraintEvaluation`, the violation-report records, `ActionContext` — and they share a single downstream consumer (Basileus). Pairing them amortizes one dispatcher refactor across both interface guarantees and lands the shape that `OntologyValidateTool.outputSchema` must embed in the same change-set that produces it.

The unit also folds three of the four MEDIUM findings from #33 (the 2.4.1 quality-review follow-ups). Findings 1 and 4 sit on the same `OntologyGraphBuilder` / source-generator surface this unit already touches; Finding 2 is a small `PgVectorObjectSetProvider` correctness fix. Finding 3 is deferred — its issue body recommends batching with #32's Option Y relaxation.

**Non-goals.** No `IOntologySource` ingestion path (Slice D, #37). No polyglot descriptor (Slice D prereq, #48). No hot-reload notifications. No new MCP tools beyond `ontology_validate`. No OpenTelemetry adoption for `IActionDispatchObserver` — observer is inline-sync per Approach 1; OTel layering is left as a follow-up if Basileus's tracing requirements pin a concrete shape.

## 2. Scope

**In scope.**

- `IActionDispatcher.DispatchReadOnlyAsync` — added via C# default interface implementation; guards on `ActionDescriptor.IsReadOnly == true`.
- `ActionDescriptor.IsReadOnly` — new flag, populated by source generator when `.ReadOnly()` is declared.
- `ObjectBuilder.Action(...).ReadOnly()` — DSL marker.
- `AONT036` — source-generator diagnostic: `.ReadOnly()` action must not declare `.Modifies()` / `.CreatesLinked()` / `.EmitsEvent()`.
- `ConstraintEvaluation` — promoted from `Strategos.Ontology.Query` to `Strategos.Ontology.Actions`; type-forwarder re-export at the old location for source compat.
- `ActionResult.Violations : ConstraintViolationReport?` — new optional field; non-breaking.
- `ConstraintViolationReport(ActionName, Hard, Soft, SuggestedCorrection)` — new record in `Strategos.Ontology.Actions`.
- `IActionDispatchObserver` — fan-out hook called inline after every dispatch (read-only and mutating); try/catch isolation.
- `IOntologyQuery.EstimateBlastRadius` + `BlastRadius` + `BlastRadiusOptions` + `BlastRadiusScope` — graph expansion primitive on existing query types.
- `IOntologyQuery.DetectPatternViolations` + `PatternViolation` + `ViolationSeverity` — 4-pattern v1 set (see §4.7).
- `OntologyValidateTool` + `ValidationVerdict` + `DesignIntent` + `ProposedAction` + `CoverageReport` — registered alongside existing tools in `OntologyToolDiscovery.Discover()`.
- `_meta.ontologyVersion` and MCP annotations on `ontology_validate` (`readOnlyHint=true`, `idempotentHint=true`, `destructiveHint=false`, `openWorldHint=false`).
- **#33-F1** — Extend AONT041 to reject explicit-name link-participating types in `OntologyGraphBuilder.ValidateMultiRegisteredTypesNotInLinks`.
- **#33-F2** — `PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload` throws when graph is present and type is unregistered.
- **#33-F4** — `DiscoverWorkflowChains` keys cross-domain lookup by `(DomainName, Name)`; workflow metadata carries domain.

**Out of scope.**

- `IOntologySource` extension point (#37) — Slice D.
- Polyglot descriptor / `SymbolKey` / AONT037 (#48) — Slice D prereq.
- AONT041 simple-name collision (#33 Finding 3) — deferred to #32.
- `IOntologyCoverageProvider` implementation — interface plus null-coalescing wiring only; concrete provider lands when an analyzer/coverage pass is built.
- `BlastRadiusOptions` extensibility hooks beyond a `MaxExpansionDegree` cap — additional knobs accumulate as real consumers exercise the surface.
- Cryptographic signing of any of these contracts.

---

## 3. Architecture overview

```text
┌────────────────────────────────────────────────────────────────────┐
│  Strategos.Ontology (existing)                                     │
│    Actions/                                                        │
│      ActionResult              (UPGRADED — gains Violations?)      │
│      ConstraintEvaluation      (PROMOTED here from Query/)         │
│      ConstraintViolationReport (NEW)                               │
│      IActionDispatcher         (UPGRADED — DispatchReadOnlyAsync)  │
│      IActionDispatchObserver   (NEW — inline fan-out)              │
│      ActionDescriptor          (UPGRADED — IsReadOnly bool)        │
│                                                                    │
│    Query/                                                          │
│      IOntologyQuery            (UPGRADED — Estimate*, Detect*)     │
│      BlastRadius               (NEW)                               │
│      BlastRadiusOptions        (NEW)                               │
│      BlastRadiusScope          (NEW — Local/Domain/CrossDomain/    │
│                                       Global)                      │
│      PatternViolation          (NEW)                               │
│      ViolationSeverity         (NEW — Warning/Error)               │
│      ConstraintEvaluation      (TYPE-FORWARDER → Actions/)         │
│                                                                    │
│    OntologyGraphBuilder        (UPGRADED — F1, F4 fixes)           │
│                                                                    │
│  Strategos.Ontology.Generators                                     │
│    ObjectBuilder.Action(...).ReadOnly()  (NEW — DSL marker)        │
│    AONT036                              (NEW — diagnostic)         │
│                                                                    │
│  Strategos.Ontology.MCP (existing)                                 │
│    OntologyValidateTool        (NEW)                               │
│    ValidationVerdict           (NEW)                               │
│    DesignIntent                (NEW)                               │
│    ProposedAction              (NEW)                               │
│    CoverageReport              (NEW)                               │
│    OntologyToolDiscovery.Discover()  (UPGRADED — registers tool)   │
│                                                                    │
│  Strategos.Ontology.Npgsql (existing)                              │
│    PgVectorObjectSetProvider   (UPGRADED — F2 strict graph check)  │
└────────────────────────────────────────────────────────────────────┘

Consumer flow (basileus):
  fabric_resolve  ──> IActionDispatcher.DispatchReadOnlyAsync(...)
                          │
                          ├── guard: ActionDescriptor.IsReadOnly
                          ├── on success: ActionResult { IsSuccess=true }
                          └── on failure: ActionResult { Violations=… }
                          │
                          └── IActionDispatchObserver.OnDispatchedAsync(…)

  intent.validated ──> OntologyValidateTool.Validate(DesignIntent)
                          │
                          ├── IOntologyQuery.EstimateBlastRadius
                          ├── IOntologyQuery.DetectPatternViolations
                          ├── ConstraintEvaluation hard/soft buckets
                          └── ValidationVerdict  ← _meta.ontologyVersion
```

The two consumer flows share `ConstraintEvaluation` (the unit of constraint judgement) and `ConstraintViolationReport` (the dispatch-time package) as their joint vocabulary. `ValidationVerdict` is the validation-time superset that adds `BlastRadius` and `PatternViolation`.

---

## 4. Decisions

### 4.1 `ConstraintEvaluation` promotion (#38 §2)

Move the record from `Strategos.Ontology.Query` to `Strategos.Ontology.Actions`. The Actions namespace is the canonical home because both `ActionResult.Violations` and `IOntologyQuery.GetActionConstraintReport` consume it, and the Query namespace was an accident of original implementation order. Re-export via `[assembly: TypeForwardedTo(typeof(ConstraintEvaluation))]` from the Query namespace so existing referenced consumers compile unchanged. No public API removed.

### 4.2 `ActionResult.Violations` + `ConstraintViolationReport` (#38 §1)

```csharp
public sealed record ActionResult(
    bool IsSuccess,
    object? Result = null,
    string? Error = null,
    ConstraintViolationReport? Violations = null);

public sealed record ConstraintViolationReport(
    string ActionName,
    IReadOnlyList<ConstraintEvaluation> Hard,
    IReadOnlyList<ConstraintEvaluation> Soft,
    string? SuggestedCorrection);
```

Non-breaking by record-with-init. Callers that destructure positionally on the four-arg shape will need to add `_` for the new field; we accept this as a permitted source-level break for v2.5.0 (we are pre-1.0 in this milestone). Reference dispatcher implementation (`OntologyActionDispatcher`) populates `Violations` whenever `GetActionConstraintReport` yields hard violations. `SuggestedCorrection` is opportunistic — populated only when the dispatcher's introspection produces a stable, single suggestion (e.g., precondition-property-missing names the property).

### 4.3 `IActionDispatcher.DispatchReadOnlyAsync` via default interface implementation (#39)

```csharp
public interface IActionDispatcher
{
    Task<ActionResult> DispatchAsync(
        ActionContext context, object request, CancellationToken ct = default);

    Task<ActionResult> DispatchReadOnlyAsync(
        ActionContext context, object request, CancellationToken ct = default)
    {
        if (context.ActionDescriptor.IsReadOnly is not true)
        {
            return Task.FromResult(new ActionResult(
                IsSuccess: false,
                Error: $"Action '{context.ActionDescriptor.Name}' is not read-only.",
                Violations: null));
        }
        return DispatchAsync(context, request, ct);
    }
}
```

C# default interface implementation (DIM) was chosen over an abstract base class because it requires no migration for existing implementations and it composes cleanly with how dispatchers are constructed today (via DI registration of concrete classes that implement `IActionDispatcher` directly). DIM is supported on net8.0+, which is already the project floor.

The default impl is intentionally minimal — concrete dispatchers may override it for additional checks, but the core invariant ("read-only path rejects unless the descriptor is read-only") lives at the interface so it cannot be silently overridden away.

### 4.4 `.ReadOnly()` DSL marker, AONT036, `ActionDescriptor.IsReadOnly` (#39 §§2-4)

DSL:

```csharp
obj.Action("GetPosition")
    .ReadOnly()
    .Returns<Position>();
```

`.ReadOnly()` returns the same `ActionBuilder` to keep call-chain ergonomics. The builder records a `bool _isReadOnly` flag that flows into the generated `ActionDescriptor.IsReadOnly = true` initializer.

`AONT036` (source-generator diagnostic): fires when `.ReadOnly()` is followed in the chain by any of `.Modifies(...)`, `.CreatesLinked(...)`, or `.EmitsEvent<T>(...)`. The diagnostic message names the conflicting call so authors can resolve quickly. Implementation: extend the existing chain-walk in `Strategos.Ontology.Generators.OntologyDslAnalyzer` to track a per-action `seenReadOnly` flag and emit at the conflict site.

`ActionDescriptor.IsReadOnly` is surfaced through the existing ontology-query API (it is part of the descriptor that `IOntologyQuery.GetActionDescriptor` already returns).

### 4.5 `IActionDispatchObserver` (#38 §3)

```csharp
public interface IActionDispatchObserver
{
    Task OnDispatchedAsync(
        ActionContext context, ActionResult result, CancellationToken ct);
}
```

Multiple observers supported via DI (`services.AddSingleton<IActionDispatchObserver, MyObserver>()`); the default dispatcher's fan-out gathers `IEnumerable<IActionDispatchObserver>` from DI and invokes each in order with try/catch isolation. Observer exceptions are swallowed and logged; they never fail the dispatch.

The observer is invoked **after** both `DispatchAsync` and `DispatchReadOnlyAsync` (the DIM in §4.3 falls through to `DispatchAsync`, so the observation point lives there). The reference implementation captures a `Stopwatch` around the inner dispatch and is purely synchronous-shaped (`Task.WhenAll` over the observers). OpenTelemetry layering is explicitly *not* in this unit — observers can be implemented over `ActivitySource` externally if a consumer wants that.

### 4.6 `IOntologyQuery.EstimateBlastRadius` + `BlastRadius` (#42 §§1-3)

```csharp
public sealed record BlastRadius(
    IReadOnlyList<OntologyNodeRef> DirectlyAffected,
    IReadOnlyList<OntologyNodeRef> TransitivelyAffected,
    IReadOnlyList<CrossDomainHop> CrossDomainHops,
    BlastRadiusScope Scope);

public enum BlastRadiusScope { Local, Domain, CrossDomain, Global }

public sealed record BlastRadiusOptions(int MaxExpansionDegree = 16);
```

Algorithm — BFS over four edge types from the seed set:

1. Seed = `touchedNodes`.
2. Expand via `GetDerivationChain` (existing).
3. Expand via `TracePostconditions` (existing).
4. Expand via `GetIncomingCrossDomainLinks` (existing) — populates `CrossDomainHops`.
5. Track a `HashSet<OntologyNodeRef>` of seen nodes for cycle detection.
6. Cap expansion depth at `BlastRadiusOptions.MaxExpansionDegree` (default 16).

Scope classification:
- `Local`: all affected nodes share one domain, no cross-domain hops.
- `Domain`: multiple object types in one domain, no cross-domain hops.
- `CrossDomain`: ≥1 cross-domain hop.
- `Global`: cross-domain hops touch ≥4 distinct domains (configurable later; static threshold for v1).

Determinism: same `OntologyGraph` + same `touchedNodes` (set-equal) yields identical `BlastRadius` (lists ordered by `(DomainName, NodeName)`).

### 4.7 `IOntologyQuery.DetectPatternViolations` + 4-pattern v1 set (#42 §§1-3)

```csharp
public sealed record PatternViolation(
    string PatternName, string Description,
    OntologyNodeRef Subject, ViolationSeverity Severity);

public enum ViolationSeverity { Warning, Error }
```

V1 patterns:

| Pattern | Severity | Detection |
|---|---|---|
| `Computed.Write` | Error | A `ProposedAction` writes to a property whose `IsComputed == true`. (Mirrors AONT023 build-time check; runtime defense-in-depth.) |
| `Link.MissingExtensionPoint` | Error | A `ProposedAction` creates a link whose source object type lacks a matching `ExtensionPoint` on the target. |
| `Action.PreconditionPropertyMissing` | Error | A `ProposedAction.ActionName` resolves to an `ActionDescriptor` whose preconditions reference a property name not present on `AcceptsType`. |
| `Lifecycle.UnreachableInitial` | Warning | Object type declares `Lifecycle.Initial` state but no incoming transition can produce it; flagged at descriptor-resolve time. |

The detection surface is intentionally extensible — a future v2 pattern set lands as a new `IPatternDetector` registry; for v1 we hard-code the four. Tests cover each pattern positively and negatively.

### 4.8 `OntologyValidateTool` + `ValidationVerdict` + `DesignIntent` (#41 §§1-2)

```csharp
public sealed record DesignIntent(
    IReadOnlyList<OntologyNodeRef> AffectedNodes,
    IReadOnlyList<ProposedAction> Actions,
    IReadOnlyDictionary<string, object?>? KnownProperties);

public sealed record ProposedAction(
    string ActionName,
    OntologyNodeRef Subject,
    IReadOnlyDictionary<string, object?>? Arguments);

public sealed record ValidationVerdict(
    bool Passed,
    IReadOnlyList<ConstraintEvaluation> HardViolations,
    IReadOnlyList<ConstraintEvaluation> SoftWarnings,
    BlastRadius BlastRadius,
    IReadOnlyList<PatternViolation> PatternViolations,
    CoverageReport? Coverage);

public sealed class OntologyValidateTool
{
    public ValidationVerdict Validate(DesignIntent intent);
}
```

`Passed = HardViolations.Count == 0 && PatternViolations.All(p => p.Severity == Warning)` (any pattern violation at `Error` severity also fails). `Coverage` is populated only if `IOntologyCoverageProvider` is registered in DI; otherwise null.

Tool registration in `OntologyToolDiscovery.Discover()` adds:

- `Name = "ontology_validate"`, `Title = "Validate design intent"`.
- `OutputSchema` derived from `ValidationVerdict` via the same `System.Text.Json.Schema` flow Slice A introduced for the other tools.
- `Annotations { ReadOnlyHint=true, IdempotentHint=true, DestructiveHint=false, OpenWorldHint=false }`.
- Response carries `_meta.ontologyVersion` like every other tool.

### 4.9 #33 Finding 1 — AONT041 link-target name extension

In `OntologyGraphBuilder.ValidateMultiRegisteredTypesNotInLinks`, after the existing multi-registration check, add a pass: for every `LinkDescriptor`, if `link.TargetType` is registered with `descriptor.Name != descriptor.ClrType.Name`, throw `OntologyCompositionException` with diagnostic id `AONT041` and message naming both the link site and the explicit-name registration. Tests cover the bug reproduction in #33 (the TradeOrder/`open_orders` case) and a positive control (link-target name matches CLR simple name).

### 4.10 #33 Finding 2 — `PgVectorObjectSetProvider` strict graph check

In `ResolveTableNameForDefaultOverload<T>`, when `graph is not null` and `TryGetValue` returns false, throw `InvalidOperationException` with the diagnostic from #33's "Fix" snippet. Graph-null fallback (test/direct-instantiation case) preserved unchanged.

### 4.11 #33 Finding 4 — `DiscoverWorkflowChains` domain-keyed lookup

`WorkflowMetadataBuilder` carries `DomainName` alongside the workflow chain entry. `DiscoverWorkflowChains` keys its lookup `Dictionary<(string DomainName, string Name), ObjectTypeDescriptor>`. First-wins `GroupBy` removed. Tests cover the cross-domain workflow-metadata case introduced by C1.

---

## 5. Migration & compatibility

- **`ActionResult` shape change:** record-with-init init means callers that construct `ActionResult` positionally with the four-arg shape compile with a fifth-arg `null`. Most callers use `new ActionResult(IsSuccess: true, Result: x)` named-arg style and are unaffected.
- **`ConstraintEvaluation` namespace move:** `[TypeForwardedTo]` re-export from `Strategos.Ontology.Query` namespace ensures binary-compatible reads from existing assemblies. Source-level `using Strategos.Ontology.Query;` remains valid.
- **`IActionDispatcher` interface change:** DIM means existing implementers compile and work. New implementers can override.
- **`IOntologyQuery` interface change:** the two new methods (`EstimateBlastRadius`, `DetectPatternViolations`) are added with default interface implementations that throw `NotSupportedException` if the concrete query type does not provide them. The reference impl (`OntologyGraphQuery`) provides both. Test doubles will need to implement them or inherit from a `OntologyQueryBase` (added if needed).
- **#33 Finding 1 diagnostic:** AONT041 extension fires at composition time on graphs that violate the new check. Existing user code that registered a link-participating type with an explicit non-default name now fails the build. Migration: rename to default or remove the explicit name. We accept this as an intentional tightening — silent cross-table drift is a worse failure mode than a compile error.

## 6. Test plan

- **Unit (Strategos.Ontology.Tests):** `ActionResult` construction, `ConstraintViolationReport` shape, `EstimateBlastRadius` deterministic output across known seed graphs (Local/Domain/CrossDomain/Global classification), `DetectPatternViolations` per-pattern positive/negative, AONT041 extension fires on the #33-1 reproduction.
- **Unit (Strategos.Ontology.MCP.Tests):** `OntologyValidateTool.Validate` happy path, hard-fail path, soft-warning path, empty-`AffectedNodes` path; output schema generation matches `ValidationVerdict` shape; `_meta.ontologyVersion` stamping.
- **Unit (Strategos.Ontology.Generators.Tests):** AONT036 fires on `.ReadOnly().Modifies()`, `.ReadOnly().CreatesLinked()`, `.ReadOnly().EmitsEvent()`; does not fire on `.ReadOnly()` alone.
- **Integration (Strategos.Ontology.Tests):** `IActionDispatcher.DispatchReadOnlyAsync` rejects non-readonly action; succeeds on read-only action; `IActionDispatchObserver` invoked after success and after failure; observer exceptions do not fail dispatch.
- **Regression (Strategos.Ontology.Npgsql.Tests):** F2 — `PgVectorObjectSetProvider` throws on graph-present + unregistered type; preserves test-mode graph-null fallback.
- **Regression (Strategos.Ontology.Tests):** F4 — `DiscoverWorkflowChains` resolves workflow metadata to the correct domain when the same simple name exists in multiple domains.

## 7. Composite acceptance criteria

- All seven deliverables (§2 in-scope) compile, ship, and have ≥1 test per acceptance criterion called out in their parent issue.
- `outputSchema` for `ontology_validate` matches `ValidationVerdict` exactly (round-trip JSON deserialize).
- Existing dispatchers and `IOntologyQuery` consumers compile without changes.
- `ConstraintEvaluation` references via `Strategos.Ontology.Query` continue to resolve at source-level.
- `_meta.ontologyVersion` present on every `OntologyValidateTool` response.
- Build succeeds with zero new analyzer warnings introduced by this unit (AONT036 must not fire on existing samples).
- Slice A's existing tests still pass (no regression in MCP descriptor / version / `_meta` envelope).

## 8. Out of scope (explicit)

- `IOntologySource` ingestion (#37) — Slice D.
- Polyglot `SymbolKey` / `LanguageId` / AONT037 (#48) — Slice D prereq.
- `IOntologyCoverageProvider` concrete implementation — only the interface plus null-coalescing wiring lands here.
- Cross-runtime dispatch / saga compensation (deferred milestone).
- AONT041 simple-name collision (#33 Finding 3) — deferred to #32.
- OpenTelemetry adoption for `IActionDispatchObserver`.

## 9. References

- ADR — `lvlup-sw/basileus:docs/adrs/ontological-data-fabric.md` §§12.1 (DispatchReadOnlyAsync), 12.2 (structured constraint feedback), 12.3 (ontology_validate), 12.4 (blast radius + patterns).
- Predecessor design — `docs/designs/2026-04-19-mcp-surface-conformance.md` (Slice A).
- Issues — strategos#39, #38, #42, #41, #33.
- Gap analysis — `lvlup-sw/basileus:docs/research/2026-04-18-strategos-ontology-gap-analysis.md` Gaps 2, 3, 4, 11.
- Slice A merge commits — bdd0fa0 (#55, closes #40), eab19c2 (#49, closes #44).

---

## 10. Implementation deltas (post-implementation erratum, 2026-05-08)

The following deviations from §§3, 4.1, 4.2, 4.5, 4.7, 4.8 were applied during implementation. The originating sections above are kept as written for design-review history; this section is the binding record of what shipped.

### 10.1 `DesignIntent` and friends live in `Strategos.Ontology.Query`, not `MCP`

Original §3 architecture box and §4.8 placed `DesignIntent`, `ProposedAction`, `CoverageReport`, and `IOntologyCoverageProvider` in `Strategos.Ontology.MCP`. Implementation moved them to `Strategos.Ontology.Query` because `IOntologyQuery.DetectPatternViolations(IReadOnlyList<OntologyNodeRef>, DesignIntent)` (§4.7) takes `DesignIntent` as a parameter — and `IOntologyQuery` lives in `Strategos.Ontology` core, which does not (and must not) reference `Strategos.Ontology.MCP`. Co-locating the records with `IOntologyQuery` avoids a cycle.

`ValidationVerdict` and `OntologyValidateTool` remain in `Strategos.Ontology.MCP` since they are MCP-tool surface.

### 10.2 `ConstraintEvaluation` source compat uses `using` directives, not `[TypeForwardedTo]`

§4.1 specified `[assembly: TypeForwardedTo(typeof(ConstraintEvaluation))]` from the old `Strategos.Ontology.Query` namespace. That attribute only forwards across assemblies; intra-assembly namespace moves rely on standard C# resolution. Implementation moved the type to `Strategos.Ontology.Actions` and updated touched consumers (`ActionConstraintReport.cs`, `OntologyQueryService.cs`) to add `using Strategos.Ontology.Actions;`. `Query/ConstraintEvaluation.cs` is retained as a comment-only stub explaining the substitution.

### 10.3 No "OntologyActionDispatcher"; cross-cutting concerns ship as opt-in decorators

§4.2 and §4.5 referred to a "reference dispatcher implementation (`OntologyActionDispatcher`)" that would populate `ActionResult.Violations` and fan out to `IActionDispatchObserver`. Strategos does not ship a concrete dispatcher (each consumer registers their own via `OntologyOptions.UseActionDispatcher<T>()`). The cross-cutting behavior was instead delivered as two per-concern decorators in `Strategos.Ontology.Actions`:

- `ConstraintReportingActionDispatcher` — wraps any `IActionDispatcher`; reads `IOntologyQuery.GetActionConstraintReport` after dispatch and attaches `ConstraintViolationReport` to `ActionResult.Violations`.
- `ObservableActionDispatcher` — wraps any `IActionDispatcher`; fans out to `IEnumerable<IActionDispatchObserver>` with try/catch isolation per observer.

Consumers opt in via `OntologyOptions.AddConstraintReporting()` (order 25, closer to inner) and `OntologyOptions.AddDispatchObservation()` (order 75, closer to caller). Both are non-breaking — without these calls, the user's dispatcher is registered as-is. The pattern matches the bifrost decorator idiom (`Bifrost.Resilience.ResilientOrchestrator`, etc.).

`ConstraintReportingActionDispatcher` uses an internal `Lazy<IOntologyQuery>` factory to break a DI cycle (the registered `IOntologyQuery` factory itself resolves `IActionDispatcher`).

### 10.4 `Link.MissingExtensionPoint` resolves target via postcondition `TargetTypeName`

§4.7 says the detector flags "creating a link to an object type without a matching ExtensionPoint." The original implementation resolved the target via the source type's `Links` collection, which silently skipped when the user declared `.CreatesLinked<TTarget>("X")` without a sibling `.HasOne<TTarget>("X")` declaration on the source. Fix:

- `ActionPostcondition` gains `TargetTypeName` (set by `ActionBuilderOfT.CreatesLinked<TTarget>` from `typeof(TTarget).Name`).
- The detector falls back to `post.TargetTypeName` when `ot.Links` lookup fails, allowing target resolution without a sibling link descriptor.
- `OntologyGraphHasher` includes the new `TargetTypeName` field so `OntologyGraph.Version` reflects this dimension.
- Regression test: `DetectPatternViolations_CreatesLinkedWithoutSiblingHasOne_StillFlagsMissingExtensionPoint`.

### 10.5 Optimistic constraint evaluation when `KnownProperties` is null

`OntologyQueryService.GetActionConstraintReport` evaluates expression-based preconditions (e.g., `.Requires(o => o.Quantity > 0)`) against `DesignIntent.KnownProperties`. When `KnownProperties` is null or omits the referenced property, the evaluator returns `IsSatisfied=true` (optimistic) rather than failing closed. Link-existence preconditions (`.RequiresLink(name)`) remain deterministic and fail closed when the link is absent.

Consumers seeking deterministic verdicts on property predicates must populate `KnownProperties` at intent construction. v2.5.0 preserves the optimistic behavior; tightening to fail-closed on missing properties is deferred to a follow-up after the Basileus integration pins the contract.

### 10.6 `ActionContext.ActionDescriptor` optional init property

§4.3's snippet for `IActionDispatcher.DispatchReadOnlyAsync` referenced `context.ActionDescriptor.IsReadOnly`. The original `ActionContext` record carried `(Domain, ObjectType, ObjectId, ActionName, Options?)` — no descriptor. Implementation added `ActionDescriptor? ActionDescriptor { get; init; }` (default null) as a non-breaking record extension. The DIM uses `context.ActionDescriptor?.IsReadOnly is true` and `context.ActionName` (not `context.ActionDescriptor.Name`) so a null descriptor produces a controlled failure rather than a NullReferenceException.

### 10.7 `WorkflowMetadataBuilder.InDomain(string)` (new public DSL surface)

`#33` Finding 4 (§4.11) keys `DiscoverWorkflowChains` lookup by `(DomainName, Name)`. The implementation added a new fluent setter `WorkflowMetadataBuilder.InDomain(string domainName)` so workflow metadata can carry the qualifying domain. This is a new public DSL surface; it is documented inline and exercised by `DiscoverWorkflowChainsDomainKeyedTests`.
