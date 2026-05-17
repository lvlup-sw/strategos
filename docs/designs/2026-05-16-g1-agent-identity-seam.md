# G1-Strategos — Agent-Identity Seam via Wolverine Envelope Headers

**Date:** 2026-05-16
**Feature ID:** `g1-agent-identity-seam`
**Slice:** v2.7.0 Slice (D) — pins `Strategos.Identity.Abstractions 2.7.0-preview.1`
**Status:** Design — pending plan-review
**Tracking:** epic [#71](https://github.com/lvlup-sw/strategos/issues/71); supersedes sub-issues [#67](https://github.com/lvlup-sw/strategos/issues/67) / [#68](https://github.com/lvlup-sw/strategos/issues/68) / [#69](https://github.com/lvlup-sw/strategos/issues/69) (saga-emit work is no longer required)
**Consumer:** lvlup-sw/basileus PR #184 — to be re-cut as SPIFFE adapter

---

## 1. Problem Statement

Basileus's `IReviewEvent` audit trail carries an empty `AgentId` field. Sagas have no programmatic access to their own identity beyond the workflow `Guid`, and downstream gaps (G3 ProvenanceEnvelope, G5 SubagentSpawn OBO, G6 causal attribution) all require per-step agent identity. v2.7.0 Slice (D) closes this gap. The basileus G1 Phase 0 design (lvlup-sw/basileus `docs/designs/2026-05-13-g1-implementation-phase-0.md`) drafted a Strategos-side property-on-saga seam. This document supersedes that draft on the Strategos side after grounding in three official sources:

1. **Wolverine `docs/guide/messaging/header-propagation.md`** — first-class envelope header propagation across handler contexts, surviving outbox and all transports (Kafka / RabbitMQ / ASB). The published example is literally `x-on-behalf-of`.
2. **`Microsoft.Extensions.AsyncState` README + dotnet/extensions issue [#4623](https://github.com/dotnet/extensions/issues/4623), PR [#4852](https://github.com/dotnet/extensions/pull/4852)** — explicit thread-safety caveat: not designed for concurrent flows. Strategos's Fork/Join emit produces concurrent handler invocations against the same workflow.
3. **Roslyn SG cookbook** — generator packages may take a public dependency on a contract package; this is the cookbook-supported pattern for generator-emitted code that references domain types.

The combination drives the Strategos-side answer: **identity lives on the Wolverine envelope, not on the saga.**

## 2. Core problem (one sentence)

Attribute every outbound message produced inside a saga handler to a specific `(workflow-identity, phase-name)` agent-identity tuple, durably across the outbox, transports, and saga step transitions.

## 3. Chosen Approach

**Strategos owns the port abstractions** (`WorkflowIdentity`, `AgentIdentity`, `IAgentIdentityProvider`, `StrategosHeaders`, `IAgentIdentityAccessor`) in a new `Strategos.Identity.Abstractions` package. **Identity is stored as Wolverine envelope headers**, not as saga fields. Basileus provides the SPIFFE-shaped adapter implementing the ports and the Wolverine middleware that stamps headers. The generator emits one new piece of state per saga: a computed `string CurrentPhaseName` property exposed via an `IPhaseAwareSaga` marker interface — a single small change, not the four-issue scope of the original A1/A2/A3/A4 plan.

## 4. Technical Design — port / adapter architecture

Following the hexagonal convention (nrjohnstone/ports-adapters-examples, guiferreira, justifiedcode): the domain owns the ports, the adapter implements them. Strategos is the domain (durable workflow state machine); Basileus is the adapter (SPIFFE / SPIRE identity provider).

```text
┌─────────────────────────────────┐   ┌─────────────────────────────────┐
│  Strategos.Identity.Abstractions│   │  Basileus.Identity.Spiffe       │
│  (ports — owned by domain)      │←──│  (adapter — owned by infra)     │
│                                 │   │                                 │
│  WorkflowIdentity (record)      │   │  SpiffeAgentIdentityProvider    │
│  AgentIdentity (record)         │   │    : IAgentIdentityProvider     │
│  IAgentIdentityProvider         │   │  StrategosHeaderMiddleware      │
│  IAgentIdentityAccessor         │   │    (Wolverine middleware)       │
│  IPhaseAwareSaga                │   │                                 │
│  StrategosHeaders (constants)   │   │  Phase 0: HMAC-shaped impl      │
└─────────────────────────────────┘   │  Phase 1: SPIRE X.509-SVID swap │
         ↑                            └─────────────────────────────────┘
         │ public dep (Roslyn SG cookbook)
┌─────────────────────────────────┐
│  Strategos.Generators           │
│  emits :Saga, IPhaseAwareSaga   │
│  emits CurrentPhaseName         │
└─────────────────────────────────┘
```

Dependencies flow inward. Strategos.Identity.Abstractions has zero dependency on Basileus. Phase 1 (HMAC → SPIRE) is purely an adapter swap with **zero change to Strategos**.

## Requirements

### DR-1 — `Strategos.Identity.Abstractions` package

A new netstandard2.0 package shipping the ports plus header constants. Versioned at `2.7.0-preview.1` aligned with `Strategos.Generators 2.7.0-preview.1`. No dependency on `Microsoft.Extensions.AsyncState`. No dependency on any non-Strategos library beyond `Wolverine` for the accessor's envelope-reading impl (which lives in a sibling `Strategos.Identity` package if we choose to ship a default accessor; otherwise accessor is contract-only and consumers implement).

**Acceptance criteria:**
- `dotnet pack` produces `Strategos.Identity.Abstractions.2.7.0-preview.1.nupkg`
- Package has zero references to `Basileus.*`
- All public types XML-documented with `<summary>` blocks
- PublicAPI baseline tracked per [#51](https://github.com/lvlup-sw/strategos/issues/51) protocol

### DR-2 — `WorkflowIdentity` and `AgentIdentity` sealed records

```csharp
public sealed record WorkflowIdentity(string Value);
public sealed record AgentIdentity(string Value);
```

Opaque-string-valued. Strategos does not inspect the value. SPIFFE shape (`spiffe://td/workflow/<id>/step/<phase>`) is a basileus-adapter concern.

**Acceptance criteria:**
- Both records are `sealed` (INV-6)
- `Value` is `string` (header-serializable by construction)
- `Value` must be non-null non-empty — record constructor throws `ArgumentException` if violated
- Roundtrip: `new WorkflowIdentity("x").Value == "x"`

### DR-3 — `IAgentIdentityProvider` port

```csharp
public interface IAgentIdentityProvider
{
    AgentIdentity DeriveStepIdentity(WorkflowIdentity workflow, string phaseName);
    WorkflowIdentity ParseWorkflowHeader(string headerValue);
}
```

`DeriveStepIdentity` uses `phaseName` (not numeric step number) because the Strategos saga's authoritative step identifier is `Phase.ToString()` — workflows with forks / branches / failure handlers don't have stable numeric ordinals. This refines basileus G1 INV-8 (derivation from saga state) without duplicating state.

**Acceptance criteria:**
- Interface is `public`
- Both methods are pure (no side effects, deterministic given inputs)
- Null/empty inputs throw `ArgumentException` (boundary validation per axiom DIM-7)

### DR-4 — `StrategosHeaders` constants

```csharp
public static class StrategosHeaders
{
    public const string WorkflowIdentity = "x-strategos-workflow-identity";
    public const string AgentIdentity = "x-strategos-agent-identity";
}
```

Header keys are versioned by the package. Consumers MUST register `opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)` to enable cross-message propagation. `AgentIdentity` is per-message-derived; it is NOT propagated to outgoing messages (each handler stamps its own).

**Acceptance criteria:**
- Constants are `public const string`
- Names match `x-strategos-*` prefix
- README documents the required `PropagateIncomingHeaderToOutgoing` registration

### DR-5 — `IAgentIdentityAccessor` for in-handler reads

```csharp
public interface IAgentIdentityAccessor
{
    WorkflowIdentity? CurrentWorkflow { get; }
    AgentIdentity? CurrentAgent { get; }
}
```

Reads from `IMessageContext.Envelope.Headers`. Returns `null` outside an active message handler context (saga inspected from a Marten projection, a debugger, etc.). Strategos may ship a default implementation in a sibling `Strategos.Identity` package; basileus may also provide an alternate impl.

**Acceptance criteria:**
- Both properties return `null` when no envelope context is active (no throw)
- Both properties return parsed records when headers are present
- Implementations are NOT required to cache (envelope lifetime IS the cache)

### DR-6 — Generator emits `CurrentPhaseName` + `IPhaseAwareSaga`

The Roslyn generator (`Strategos.Generators` `SagaPropertiesEmitter` or a new `SagaPhaseAccessorEmitter`) emits:

```csharp
public partial class {Saga} : Saga, IPhaseAwareSaga
{
    // existing emit: Phase, WorkflowId, State, ...
    public string CurrentPhaseName => Phase.ToString();
}
```

This is the **only** generator emit change. No `InitializeIdentity` helper, no `CurrentAgentIdentity` property, no private mutable fields. The existing four basileus issues (#67/#68/#69) are descoped — `CurrentPhaseName` is one computed read-only property and one interface declaration.

**Acceptance criteria:**
- Snapshot test asserts `CurrentPhaseName` emit
- Snapshot test asserts `IPhaseAwareSaga` is in the saga's base list
- `CurrentPhaseName` is a get-only computed property (no setter, no backing field)
- Existing saga snapshots (Phronesis, review-loop, AgenticCoder, ContentPipeline, MultiModelRouter samples) regenerate successfully with the additive change

### DR-7 — Strategos generator emits NO other identity-related code

Confirms what's NOT in scope: no `CurrentAgentIdentity` property emit, no `InitializeIdentity(...)` helper, no `_workflowIdentity` / `_identityProvider` backing fields, no `InternalsVisibleTo` requirement on the generated assembly. Identity propagation happens via Wolverine envelope headers and the host-side middleware.

**Acceptance criteria:**
- A search of generated `*Saga.g.cs` finds no occurrence of `WorkflowIdentity`, `AgentIdentity`, `IAgentIdentityProvider`, or `InternalsVisibleTo`
- INV-1 audit passes — generated saga lowering is unchanged except for the `IPhaseAwareSaga` base-list entry and the `CurrentPhaseName` computed property
- INV-7 audit passes — no new mutable fields on any generated saga

### DR-8 — Error handling and edge cases

| Scenario | Behavior | Mechanical enforcement |
|---|---|---|
| Accessor read outside a Wolverine handler context | `CurrentWorkflow` / `CurrentAgent` return `null` | Default impl checks `IMessageContext` resolution; returns null on failure |
| Incoming envelope has no `x-strategos-workflow-identity` header | Middleware generates a new `WorkflowIdentity` keyed to `sagaId` | basileus middleware contract; Strategos provides no enforcement |
| `WorkflowIdentity` record constructed with null/empty value | `ArgumentException` at record construction | Sealed record positional constructor validates |
| Provider returns null from `DeriveStepIdentity` | `InvalidOperationException` thrown by middleware (basileus side) | basileus middleware contract |
| Saga handler emits a message with no Wolverine outbox context | Headers are still set; outgoing message carries them via Wolverine's standard envelope mechanism | Wolverine native |
| Header value contains non-UTF8 / control characters | Wolverine's transport layer is responsible for header encoding; values must be ASCII-safe | Strategos `WorkflowIdentity.Value` setter validates ASCII subset |

**Acceptance criteria:**
- Unit tests cover each row above
- `WorkflowIdentity` and `AgentIdentity` constructors throw `ArgumentException` on null, empty, or non-ASCII-safe input
- Accessor null-handling has a test case that runs WITHOUT a Wolverine message context

### DR-9 — Cross-repo coordination (basileus)

Basileus PR #184 is re-cut to:
- Reference `Strategos.Identity.Abstractions 2.7.0-preview.1`
- Drop `Basileus.Core.Contracts.Identity.{SpiffeId, WorkflowId, WorkflowIdentity, AgentIdentity, IAgentIdentityProvider}` — these become Strategos types or are re-shaped as adapter-internal concerns
- Add `Basileus.Identity.Spiffe.SpiffeAgentIdentityProvider : IAgentIdentityProvider`
- Add `Basileus.AgentHost.Middleware.StrategosHeaderMiddleware` — Wolverine middleware that:
  1. Reads `IMessageContext.Envelope.Headers[StrategosHeaders.WorkflowIdentity]` or generates a new one keyed to `sagaId`
  2. After Wolverine loads the saga (e.g., as a method parameter `IPhaseAwareSaga saga`), reads `saga.CurrentPhaseName`
  3. Calls `provider.DeriveStepIdentity(workflowId, saga.CurrentPhaseName)`
  4. Stamps `context.Envelope.Headers[StrategosHeaders.AgentIdentity] = agent.Value`
- Configure `opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)` in `UseWolverine`
- The basileus design doc `2026-05-13-g1-implementation-phase-0.md` is revised to reflect the envelope-header storage choice; the §4 derivation-site rationale (INV-8) is preserved unchanged

**Acceptance criteria:**
- basileus PR #184 builds against `Strategos.Identity.Abstractions 2.7.0-preview.1` published to nuget.org (or a local feed)
- An integration test in basileus verifies an outbound message has `x-strategos-workflow-identity` and `x-strategos-agent-identity` headers populated after handler completion
- basileus design doc revision is committed in the same PR

### DR-10 — Release: `Strategos.Identity.Abstractions 2.7.0-preview.1`

Per [#70](https://github.com/lvlup-sw/strategos/issues/70) protocol, but with revised scope:

- CHANGELOG `## [2.7.0-preview.1]` section describes:
  - New package `Strategos.Identity.Abstractions` debuts at 2.7.0-preview.1
  - Generator emits `CurrentPhaseName` + `IPhaseAwareSaga` on all sagas (additive; existing consumers unaffected unless they redefine the same identifier)
  - No breaking changes to existing 2.6.0 surface
- Bump `LevelUp.Strategos`, `LevelUp.Strategos.Generators` to 2.7.0-preview.1
- Tag `v2.7.0-preview.1`
- Comment on [#70](https://github.com/lvlup-sw/strategos/issues/70) with the nuget.org URL once published

**Acceptance criteria:**
- Three packages published: `LevelUp.Strategos.2.7.0-preview.1`, `LevelUp.Strategos.Generators.2.7.0-preview.1`, `LevelUp.Strategos.Identity.Abstractions.2.7.0-preview.1`
- All three resolvable from nuget.org
- basileus PR #184 successfully pins to this preview

## 6. Invariant audit (INV-1..INV-8)

| Invariant | Severity | Verdict | Notes |
|---|---|---|---|
| INV-1 workflow lowering via Wolverine+Marten | HIGH | ✓ | Uses Wolverine's native envelope-header primitive — strongest possible alignment |
| INV-2 ontology analyzer-only and self-contained | HIGH | N/A | Does not touch ontology |
| INV-3 MCP first-class | MEDIUM | N/A | Does not touch MCP |
| INV-4 concrete DSL nomenclature | MEDIUM | ✓ | `WorkflowIdentity`, `AgentIdentity`, `PhaseAwareSaga` — all concrete domain terms |
| INV-5 three-tiered validation, stable diagnostic IDs | HIGH | N/A | No new diagnostics required (no compile-time check available — runtime DI registration check is consumer's responsibility) |
| INV-6 sealed-by-default | HIGH | ✓ | Both records explicitly `sealed`; interfaces by their nature are extension points |
| INV-7 immutable record state | HIGH | ✓ | Zero new mutable fields on any saga; `CurrentPhaseName` is read-only computed; sealed records are immutable |
| INV-8 polyglot identity (`ClrType` / `SymbolKey`) | HIGH | N/A | Different identity concept (ontology descriptor identity, not agent identity) |

## 7. axiom dimensions audit (DIM-1..DIM-8)

| Dimension | Verdict | Notes |
|---|---|---|
| DIM-1 architecture | ✓ | Hexagonal port/adapter; SRP-clean package split |
| DIM-2 tests | ✓ | Per-DR acceptance criteria, snapshot tests, integration test on basileus side |
| DIM-3 surface design | ✓ | 6 types (2 records, 3 interfaces, 1 static constants class) — each load-bearing |
| DIM-4 concurrency | ✓ | Stateless ports; envelope headers are per-message immutable |
| DIM-5 performance | ✓ | Header read is O(1) dictionary lookup; no AsyncLocal overhead |
| DIM-6 coupling | ✓ | Strategos.Identity.Abstractions has zero Basileus reference; dependency arrow points correctly |
| DIM-7 errors | ✓ | DR-8 enumerates each edge case with mechanical enforcement |
| DIM-8 distillation | ✓ | No A1/A2/A3 emit work; only `CurrentPhaseName` + interface declaration added to generator |

## 8. Alternatives Considered

Phase 2 of `/exarchos:ideate` evaluated five options end-to-end. Summarized:

| Option | Shape | Why rejected (or adopted) |
|---|---|---|
| **A** — pure name-string emit | Generator emits `CurrentAgentIdentity` + `InitializeIdentity` referring to consumer-provided types | Strategos owns no port — Basileus owns both port and adapter. Anti-hexagonal. |
| **B** — A + empty `ISagaIdentityHost` marker | Same as A plus a Strategos-side empty marker interface | Marker doesn't load-bear; INV-4 friction with structural "Host" name; DIM-8 demerit. |
| **C** — Strategos-owned ports + saga-field emit | Strategos ships abstractions; generator emits `_workflowIdentity`/`_identityProvider` private fields + `InitializeIdentity` helper + `CurrentAgentIdentity` property | INV-7 friction (mutable transient fields); requires `InternalsVisibleTo` on consumer; doesn't survive outbox without explicit per-message stamping. |
| **D** — `Microsoft.Extensions.AsyncState` ambient context | Identity flows via `IAsyncContext<T>`, zero saga emit | dotnet/extensions PR #4852 + issue #4623 confirm AsyncState is **not designed for concurrent flows**; Fork/Join emit produces concurrent handler invocations against the same workflow. Doesn't survive outbox without explicit stamping. |
| **E** — Wolverine envelope-header propagation **(adopted)** | Identity stored on `IMessageContext.Envelope.Headers`; consumer registers `PropagateIncomingHeaderToOutgoing` | Uses Wolverine's first-class header-propagation primitive ([`docs/guide/messaging/header-propagation.md`](https://github.com/jasperfx/wolverine/blob/main/docs/guide/messaging/header-propagation.md), published example: `x-on-behalf-of` — identical to G5 OBO use case). Native outbox + transport survival. Zero mutable saga state. |

The decisive factor was Wolverine's published header-propagation primitive — it solves the harder problem (cross-outbox + cross-transport identity propagation) that C/D both punt on, AND uses the platform Strategos already lowers to per INV-1.

## 9. Integration Points

- **Wolverine** — header propagation policy (`opts.Policies.PropagateIncomingHeaderToOutgoing(StrategosHeaders.WorkflowIdentity)`) registered in consumer's `UseWolverine` block; middleware reads/stamps `IMessageContext.Envelope.Headers`
- **Marten** — outbox row contains the Wolverine envelope (with headers) — no schema change required
- **Basileus.AgentHost** — provides Wolverine middleware (`StrategosHeaderMiddleware`) and the SPIFFE-shaped `IAgentIdentityProvider` implementation
- **Strategos.Generators** — emits `: Saga, IPhaseAwareSaga` + `public string CurrentPhaseName => Phase.ToString()` on every generated saga partial
- **NuGet publishing** — new package `Strategos.Identity.Abstractions` ships alongside `Strategos.Generators` and `Strategos` at `2.7.0-preview.1`

## 10. What changed vs the basileus G1 Phase 0 draft

| | basileus draft (Option C in ideation) | This design (Option E — adopted) |
|---|---|---|
| Identity storage | private fields on saga (`_workflowIdentity`, `_identityProvider`) | Wolverine envelope headers |
| Saga reads identity | `this.CurrentAgentIdentity` property | injected `IAgentIdentityAccessor.CurrentAgent` |
| Generator A1 (CurrentAgentIdentity property) | required | **descoped** |
| Generator A2 (InitializeIdentity helper) | required | **descoped** |
| Generator A3 (compile test with stubs) | required | **descoped** |
| Generator A4 (release) | required | retained (scope changes to ship abstractions package) |
| New generator emit | property + helper + 2 mutable fields per saga | one computed property + one interface declaration |
| `InternalsVisibleTo` requirement on consumer | yes | **none** |
| Outbox / transport survival | requires explicit per-message stamping | **native via Wolverine** |
| Cross-message identity propagation | requires consumer code | **native via `PropagateIncomingHeaderToOutgoing`** |
| basileus INV-8 (derivation from saga state) | direct field read | middleware reads `saga.CurrentPhaseName` before stamping |
| Strategos INV-1 alignment | weakened (adds emit) | strengthened (uses Wolverine native primitive) |
| Strategos INV-7 alignment | violated transiently (mutable fields, mitigated) | preserved fully |

## 11. Out of scope (deferred)

- **G3 ProvenanceEnvelope** ([#61](https://github.com/lvlup-sw/strategos/issues/61)) — provenance envelope contracts build on top of this seam; the natural shape is additional `x-strategos-provenance-*` headers using the same Wolverine propagation policy
- **G5 SubagentSpawn OBO** ([#62](https://github.com/lvlup-sw/strategos/issues/62)) — token exchange uses agent identity but happens on the basileus side
- **G4 PartitionablePair** ([#60](https://github.com/lvlup-sw/strategos/issues/60)) — independent
- **Phase 1 SPIRE swap** — basileus-internal adapter change; zero Strategos work
- **`Strategos.Identity` default accessor implementation** — may ship in 2.7.0 GA; preview.1 ships abstractions only and basileus provides its own accessor impl

## 12. Testing Strategy

1. **Unit tests in `Strategos.Identity.Abstractions.Tests`** (new project)
   - Record construction: null/empty/non-ASCII rejection
   - `StrategosHeaders` constant values stable
2. **Snapshot tests in `Strategos.Generators.Tests`**
   - Every existing saga snapshot regenerates with `IPhaseAwareSaga` and `CurrentPhaseName` additions
   - New snapshot test asserts `CurrentPhaseName` exists and returns `Phase.ToString()`
   - Existing sample workflows (AgenticCoder, ContentPipeline, MultiModelRouter) still build
3. **Integration test (basileus side, in PR #184)**
   - End-to-end Wolverine handler test: incoming message → middleware stamps headers → handler emits cascading message → outgoing envelope contains both headers
   - Header value roundtrip: `WorkflowIdentity.Value` matches across in→out

## 13. Open Questions (resolve during plan phase, not blocking)

1. Whether to ship a default `IAgentIdentityAccessor` impl in `Strategos.Identity` (sibling package) in preview.1 or defer to GA
2. Whether `WorkflowIdentity.Value` ASCII validation is too strict (SPIFFE URIs are ASCII; basileus's HMAC scheme is base64-url which is ASCII; safe default but worth confirming)
3. Whether to also propagate `x-strategos-agent-identity` to outgoing messages (vs. each handler stamping its own — current design says each stamps its own; this matches OpenTelemetry `traceparent` semantics)

---

**Provenance:** Phase 1 understanding driven by [#71](https://github.com/lvlup-sw/strategos/issues/71) and the basileus G1 Phase 0 design. Phase 2 exploration evaluated four options (name-string emit, empty marker, port abstractions + saga emit, AsyncLocal ambient context) and discovered the Wolverine-canonical envelope-header pattern via official Wolverine docs `header-propagation.md`. Phase 3 (this document) commits to envelope-header storage with Strategos-owned ports.
