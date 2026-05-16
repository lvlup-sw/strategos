---
title: "ADR: Exarchos ↔ Basileus Coordination Architecture"
---

# ADR: Exarchos ↔ Basileus Coordination Architecture

**Date:** 2026-04-18 (rev 2026-04-19 — synthesized with research findings)
**Status:** Accepted
**Supersedes:**
- `docs/designs/2026-02-19-remote-notification-bridge.md` (notification surface)
- the three-layer notification model in basileus#122
- the prior v1 of this ADR (2026-04-18), which specified a reinvented Basileus-hosted ontology tool surface; v2 reorients around the existing `Strategos.Ontology.MCP` tools

**Synthesis of:**
- `docs/designs/2026-04-18-exarchos-basileus-coordination.md` (design)
- `docs/research/2026-04-18-platform-agnostic-coordination.md` (research — notification surface)
- `docs/research/2026-04-18-strategos-ontology-gap-analysis.md` (research — ontology gaps)
- `docs/decisions/2026-04-18-strategic-framing-exarchos-basileus.md` (product boundaries)

**Issues (source of truth):**
basileus #112, #119, #120, #121, #122, #123, #124, #145, #147, #152, #156, **#167** (hybrid retrieval composition epic) ·
exarchos #1109, #1125 · strategos #16, #23, #37, #38, #39, #40, #41, #43, #44, **#47** (hybrid retrieval seams, 2.6.0)

---

## 1. Thesis: inverting the agent investigation flow

### 1.1 The bottom-up status quo

Every agent harness in production today — Claude Code, Cursor, Copilot, Codex, OpenCode — operates bottom-up: the user issues a prompt; the agent greps, reads, and follows imports; from loose code artifacts it reconstructs a mental model of the domain; it then plans and acts against that reconstructed model.

This has four structural costs:

1. **Cost scales with the codebase.** Each session pays O(codebase) investigation tokens before any useful work begins.
2. **Models diverge between agents.** Ten agents on the same code form ten slightly different models. Two coordinating agents (Exarchos local + Basileus remote) cannot assume a shared mental map.
3. **Refactor-fragile.** Rename a type, move a file — the grep patterns agents learned break. The model does not survive the territory's churn.
4. **Ephemeral.** The map dies with the session. The next agent repays the cost.

### 1.2 The top-down inversion this ADR builds rails for

An ontology-anchored flow flips all four:

`prompt → ontology_explore → ontology_validate(intent) → fabric_resolve(state) → plan against constraints → act`

- **Cost scales with the schema**, not the codebase. Schemas are small; codebases grow unboundedly.
- **One authoritative map.** Every agent — local subagent, remote Basileus agent, Exarchos coordinator — queries the same ontology graph and sees the same constraints, links, lifecycle states, and blast radii.
- **Refactor-stable surface.** Agent queries use declared ontology names rather than source strings, so the churn that breaks grep-pattern agents (moves, reformatting) doesn't silently invalidate the interface, and renames surface as build errors or rename deltas rather than quiet drift. Keeping the data behind that interface in sync with source truth at the latency the inversion requires is load-bearing work, not a sidebar — §1.3 (completeness) and §2.14 (ingestion cost/freshness SLOs) are how the ADR pays that bill.
- **Durable.** The `OntologicalRecord` (§2.5) is the cached top-down mental model — layered, event-sourced, persisted. The next session skips the discovery cost the previous one paid.

**This is the thesis the coordination architecture serves.** Two-channel MCP transport, the Ontology MCP endpoint, the OntologicalRecord lifecycle, cross-tier delegation, and the Strategos prerequisite refinements are all moves toward making top-down planning economically viable in production. Every decision below is evaluated against whether it advances or obstructs that inversion.

### 1.3 The load-bearing dependency: ontology completeness

Top-down planning is only as trustworthy as the ontology is complete. If Trading has 50 meaningful types and only 12 are registered in `Strategos.Ontology`, agents receive a confident-looking map with 38 blind spots — worse than the bottom-up alternative, because agents stop greping once the map claims to cover the territory.

Completeness is a governance problem, not a technology problem: ontology registration must become part of definition-of-done for new domain code. This ADR assumes completeness; the **source-repository ingestion pipeline** that makes it tractable is scoped to a separate ideation (`ingest-ontology-from-source`) and tracked as open question §9.1. This ADR ships the *rails*; that ideation ships the *guardrails*.

### 1.4 Constraints

The following non-negotiables shape every decision below:

| Constraint | Source | Implication |
|---|---|---|
| Convergence, not overlap | Strategic framing | Exarchos owns process; Basileus owns execution; same contracts, different scope |
| Graceful degradation | Strategic framing | Exarchos is fully functional with no Basileus connection at all |
| Same contracts, different scope | Strategic framing | Both products consume `Strategos.Contracts`; neither depends on the other |
| Two-hop MCP chain | platform-architecture §11.7 | Claude Code → Exarchos MCP Server → Basileus (MCP client role) |
| Local-first | distributed-sdlc-pipeline §12 | Operational mode `local` requires no Basileus; `remote` and `dual` enrich, never replace |
| Runtime-agnosticity | Exarchos `2026-03-09-platform-agnosticity.md` | No design may depend on Claude Code-specific features for correctness |
| Event-sourcing integrity | exarchos #1109 | Every coordination output reconstructible from the event stream |
| **Single ontology tool surface** | Gap analysis §1 | Basileus hosts Strategos's existing tools; it does not reinvent them |
| **Ontology completeness before reliance** | §1.3 | No `/ideate` phase may treat the ontology as authoritative for a domain until that domain's registration coverage gate passes |

---

## 2. Decisions

### 2.1 Two-channel MCP transport (unchanged)

Exarchos maintains two **independent** outbound MCP connections to Basileus:

```
Exarchos MCP Server process
├── MCP Client A → Basileus Workflow MCP Server  (/mcp/workflow)
│     Lifecycle: active during /delegate phases that dispatch remote work
│     Surface : workflow_subscribe (SSE in), sync command/respond/subscribe
│
└── MCP Client B → Basileus Ontology MCP Endpoint (/mcp/ontology)
      Lifecycle: always-on whenever Basileus is configured + reachable
      Surface : ontology_explore, ontology_query, ontology_validate,
                fabric_resolve, intent_register (write-restricted; see §2.2)
```

Independence enables the **Fabric-only integration tier** (Ontology connected, Workflow disconnected) — Exarchos enriches local orchestration with live ontology state without dispatching any work to Basileus.

### 2.2 Basileus Ontology MCP **Endpoint** (revised)

The v1 of this ADR proposed a new "Basileus Ontology MCP Server" hosting four hand-written tools. The research (gap analysis §2, §3) established that `Strategos.Ontology.MCP` already ships three production tools (`ontology_explore`, `ontology_query`, `ontology_action`) with deeper capability than v1 specified, and that Basileus already pulls the package (unwired). Reinventing would create tool-name collisions and duplicate contract surfaces.

**Revised design:** Basileus hosts a **mount point** (`/mcp/ontology`) that exposes Strategos's canonical tools plus three Basileus-specific additions. The mount is co-located in the AgentHost process and registered in the Aspire AppHost.

| Tool | Owner | Backed by | Read/Write | Purpose |
|---|---|---|:-:|---|
| `ontology_explore` | Strategos (reused) | `OntologyGraph` | R | Browse domains, object types, actions, links, interfaces, events, workflow chains, vector properties; BFS link traversal |
| `ontology_query` | Strategos (reused) | `IObjectSetProvider` + `IEventStreamProvider` | R | Object-set queries with filter, link traversal, interface narrowing, and **built-in semantic search** (semanticQuery, topK, minRelevance, distanceMetric) |
| `ontology_action` | Strategos (reused) — **not mounted on `/mcp/ontology`** | `IActionDispatcher` | W | Single-object or batch dispatch. Deliberately excluded from the Basileus-facing endpoint (§9.2 open question); writes go through Phronesis `ThinkStep → ActStep` |
| `ontology_validate` | **Strategos (new — §2.10.3)** | `IOntologyQuery.GetActionConstraintReport` + `EstimateBlastRadius` + `DetectPatternViolations` | R | Evaluate a `DesignIntent` against the ontology; return `ValidationVerdict` with hard/soft constraint evaluations, blast radius, and pattern violations |
| `fabric_resolve` | **Basileus (new)** | `IActionDispatcher.DispatchReadOnlyAsync()` — **§2.10.1** | R | Live domain state via the same path `ThinkStep` uses, guaranteed read-only at the interface level |
| `intent_register` | **Basileus (new)** | `IOntologicalRecordService` (new — §2.5) | W | Accept `ProcessLayer`, enrich with `DomainLayer`, persist the composite record to its Marten event stream |

**`Strategos.Ontology.MCP` activation.** Basileus will instantiate `OntologyExploreTool`, `OntologyQueryTool`, and (conditionally) `OntologyActionTool` via DI, upgraded to emit MCP 2025-11-25 `outputSchema` and `ToolAnnotations` per §2.11.

No parallel `OntologyMcpTools` class. The "thin facade" is wiring, not reimplementation.

### 2.3 Composite NotificationPipeline (unchanged — orthogonal)

[Retained verbatim from v1 §2.3 — tiers T0–T5, sink segregation `InlineSink` / `AsyncSink`, observable `degradedSinks`, piggyback envelope schema in Exarchos-side Zod (not in `Strategos.Contracts`), and elicitation lift. The notification architecture is independent of the ontology work and its design review in the agnostic-coordination research remains authoritative.]

### 2.4 Sideband daemon (`exarchos watch`) (unchanged — orthogonal)

[Retained verbatim from v1 §2.4 — `INotificationPublisher` boundary, Unix socket / JSONL transports, desktop / status-line / webhook transport sinks, resilience requirements, test seam.]

### 2.5 OntologicalRecord as Marten event-sourced aggregate (revised)

The v1 of this ADR claimed the `OntologicalRecord` would be "persisted in the data fabric as a `SemanticDocument`." The gap analysis (§Gap 6) established this is structurally unsound: `SemanticDocument.Metadata` is `ImmutableDictionary<string,string>` (no nested structure), `Content` is the embedded text (putting JSON there pollutes the embedding), and the record's state machine has no home in the fabric unit.

**Revised design:** the `OntologicalRecord` is a **Marten event-sourced aggregate**. The record's *summary* (design description, feature title, current status) is indexed into a `SemanticDocument` for semantic search; the *structured layers* live in the event stream as the authoritative source.

```typescript
interface OntologicalRecord {
  id: string; featureId: string;
  status: "proposed" | "validated" | "enriched" | "executing" | "completed" | "failed";
  createdAt: string; updatedAt: string;
  ontologyVersion: string;        // pins the graph version (§2.12)
  branch?: string;                // NEW — VCS branch the record is scoped to
  prNumber?: number;              // NEW — correlated GitHub PR number (null if no PR yet)
  processLayer: ProcessLayer;     // Authored by Exarchos at /plan
  domainLayer?: DomainLayer;      // Enriched by Basileus on intent.proposed
}

interface ProcessLayer {
  designRef: string; taskLedger: TaskSpec[]; qualityGates: QualityGateSpec[];
  dependencies: DependencySpec[]; delegationPolicy: DelegationPolicy;
  budgetConstraints?: BudgetSpec;
}

interface DomainLayer {
  affectedNodes: OntologyNodeRef[];
  validationVerdict: ValidationVerdict;            // §2.10.3
  blastRadius: BlastRadius;                         // §2.10.4
  patternViolations: PatternViolation[];           // §2.10.4
  existingPatterns: PatternRef[];
  domainStateSnapshot: DomainSnapshot;
}
```

**Wolverine saga** manages status transitions. **Marten stream** is keyed by `recordId`; events are `IntentProposed`, `IntentValidated`, `IntentEnriched`, `IntentExecuting`, `IntentCompleted`, `IntentFailed`. The `SemanticDocument` indexed for search carries only `Content` (design summary) and `Metadata["recordStream"] = recordId`; fabric queries resolving an ontology node can list records that referenced it by joining on `affectedNodes`.

**Lifecycle:** `/ideate` (queries ontology) → `/plan` (compiles `ProcessLayer`) → `intent.proposed` event → Basileus `IntentProposedHandler` enriches with `DomainLayer` (calling `ontology_validate` → `ValidationVerdict` + `BlastRadius`) → `intent.validated` (gate) → `intent.enriched` → `/delegate` (router reads policy + constraints) → tasks complete → `intent.completed`.

**Branch scoping.** Records are scoped to a branch (hand-authored work typically happens on feature branches). `Branch` captures the VCS ref; `PrNumber` correlates to the GitHub App's PR tracking (see source-ingestion design §4.9) once a PR is opened. A record created before a PR exists has `PrNumber = null`; the ingestion webhook handler backfills the field when the PR is opened on a matching branch. This makes `/ideate`-first-then-PR and PR-first-then-`/ideate` workflows both first-class.

**Validation gate.** `intent.validated` is a new explicit phase between `proposed` and `enriched` where `ValidationVerdict.passed = false` routes to a rejection path instead of proceeding to enrichment. This is the shift-left promise of the inversion (§1.2): rejected designs never reach `/delegate`. Validation is evaluated against the record's `branch`-scoped graph (main ⊕ branch-delta), not against main alone — so blast radius and constraint evaluation reflect the actual feature-branch state.

**PR lifecycle correlation** (from source-ingestion design §4.9). When a GitHub PR webhook fires on a branch that has an active record: `pull_request.opened` → backfills `PrNumber` if null; `pull_request.closed (merged=true)` → transitions status to `completed`; `pull_request.closed (merged=false)` → transitions to `failed`. Direct `intent.executing → intent.completed` transitions from Exarchos (task completion) still drive the record independently — the webhook and Exarchos paths both write to the same saga with deterministic merge on timestamps.

TypeSpec models for the record, layers, and lifecycle events live in `Strategos.Contracts` (basileus#152 + exarchos#1125). C# records are consumed by Basileus; Zod schemas are derived for Exarchos.

### 2.6 Hybrid delegation + cross-tier dependency resolution (unchanged)

[Retained verbatim from v1 §2.6 — `TaskClassification.target`, routing heuristics table, git-branch cross-tier dependency resolution.]

**Addition:** the router reads `domainLayer.blastRadius.scope` when deciding local vs. remote. `CrossDomain` or `Global` scope biases toward local (developer's machine, where the full repo is already resolved); `Local` scope permits remote.

### 2.7 Configuration consolidation (`.exarchos.yml`) (unchanged)

[Retained verbatim from v1 §2.7. `bridge-config.json` rejected; all Basileus-connection and notification config moves to `.exarchos.yml` under `basileus:`. Per-transport daemon configs in `~/.exarchos/notifications.toml`.]

### 2.8 Capability resolution (yaml ⊕ MCP handshake) (unchanged)

[Retained verbatim from v1 §2.8. Capability taxonomy (`realtimeChannel`, `elicitation`, `resourceSubscription`, `loggingNotifications`); MCP `initialize` handshake authoritative; single resolver merges yaml ⊕ handshake; pipeline constructed lazily after handshake.]

**Addition for §2.12:** the resolver also captures the ontology graph version hash returned on `initialize` — Exarchos uses it to invalidate cached schema views.

### 2.9 Skills bootstrap pull (capability-guarded) (unchanged)

[Retained verbatim from v1 §2.9.]

### 2.10 Strategos prerequisite refinements (NEW)

The ADR's `/mcp/ontology` surface depends on three Strategos extensions that must ship before Basileus can implement §2.2. These are tracked in the strategos repo as **must-land** blockers.

#### 2.10.1 `IActionDispatcher.DispatchReadOnlyAsync` (Gap 2)

Add a read-only dispatch path with interface-level guarantee:

```csharp
public interface IActionDispatcher
{
    Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default);
    Task<ActionResult> DispatchReadOnlyAsync(ActionContext context, object request, CancellationToken ct = default);
}
```

Default implementation rejects actions whose `Postconditions` list is non-empty. Ontology DSL gains `.ReadOnly()` marker on actions; source generator emits new AONT036 diagnostic. `fabric_resolve` calls `DispatchReadOnlyAsync` exclusively — the MCP `readOnlyHint: true` annotation (§2.11) is backed by a type-system guarantee, not a convention.

#### 2.10.2 Structured constraint feedback (Gap 3)

Extend `ActionResult` and promote `ConstraintEvaluation` into `Actions/`:

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

Non-breaking. Old callers inspecting only `IsSuccess`/`Error` keep working. The OTC paper's empirically-validated constraint feedback loop (+F1 in synthesis-step tasks; grounding doc §4.1) becomes available to every agent calling `ontology_action` or `fabric_resolve`.

#### 2.10.3 `ontology_validate` tool and `ValidationVerdict` record (Gap 3, 4)

New Strategos tool in `Strategos.Ontology.MCP`:

```csharp
public sealed class OntologyValidateTool
{
    public ValidationVerdict Validate(DesignIntent intent);
}

public sealed record DesignIntent(
    IReadOnlyList<OntologyNodeRef> AffectedNodes,
    IReadOnlyList<ProposedAction> Actions,
    IReadOnlyDictionary<string, object?>? KnownProperties);

public sealed record ValidationVerdict(
    bool Passed,
    IReadOnlyList<ConstraintEvaluation> HardViolations,
    IReadOnlyList<ConstraintEvaluation> SoftWarnings,
    BlastRadius BlastRadius,
    IReadOnlyList<PatternViolation> PatternViolations);
```

#### 2.10.4 Blast radius and pattern violation primitives (Gap 4)

New `IOntologyQuery` methods with serializable result records:

```csharp
BlastRadius EstimateBlastRadius(
    IReadOnlyList<OntologyNodeRef> touchedNodes,
    BlastRadiusOptions? options = null);

IReadOnlyList<PatternViolation> DetectPatternViolations(
    IReadOnlyList<OntologyNodeRef> affectedNodes,
    DesignIntent intent);

public sealed record BlastRadius(
    IReadOnlyList<OntologyNodeRef> DirectlyAffected,
    IReadOnlyList<OntologyNodeRef> TransitivelyAffected,
    IReadOnlyList<CrossDomainHop> CrossDomainHops,
    BlastRadiusScope Scope);    // { Local, Domain, CrossDomain, Global }
```

This is the **shift-left validation primitive**: pre-commit impact analysis. The router (§2.6) reads `BlastRadiusScope`; the `ontology_validate` gate (§2.5) refuses `intent.enriched` when pattern violations include `Severity.Error`.

### 2.11 MCP 2025-11-25 conformance (NEW — Gap 5)

Upgrade `OntologyToolDescriptor` and tool responses to current MCP spec:

```csharp
public sealed record OntologyToolDescriptor(
    string Name, string Title, string Description)
{
    public JsonElement? InputSchema { get; init; }
    public JsonElement? OutputSchema { get; init; }       // NEW
    public ToolAnnotations Annotations { get; init; }     // NEW
    public IReadOnlyDictionary<string, object?> Meta { get; init; } = ...;
}

public sealed record ToolAnnotations(
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);
```

**Annotation matrix (locked):**

| Tool | readOnly | destructive | idempotent | openWorld |
|---|:-:|:-:|:-:|:-:|
| `ontology_explore` | ✓ | | ✓ | |
| `ontology_query` | ✓ | | ✓ | |
| `ontology_validate` | ✓ | | ✓ | |
| `fabric_resolve` | ✓ | | ✓ | |
| `ontology_action` (non-`/mcp/ontology`) | | ✓ | | |
| `intent_register` | | ✓ | | ✓ |

Agents on Cursor / Copilot / Codex gate auto-approval on annotations. Without this work every ontology call would prompt the user.

### 2.12 Ontology graph versioning (NEW — Gap 7)

`OntologyGraph` gains a stable version hash computed at build time:

```csharp
public sealed class OntologyGraph
{
    // existing members...
    public string Version { get; }   // sha256(domains ⊕ objectTypes ⊕ links ⊕ actions ⊕ lifecycles)
}
```

Surfaced in:
- MCP `initialize` response capability parameters
- Every `ontology_explore` / `ontology_query` / `ontology_validate` / `fabric_resolve` response `_meta.ontologyVersion`
- `OntologicalRecord.ontologyVersion` at record creation

Exarchos compares cached version against fresh responses; mismatch invalidates its schema cache. On hot reload of a domain (future work), Strategos emits `notifications/resources/updated` for `ontology://{domain}/*` (Gap 8 — deferred to a follow-up issue).

### 2.13 Deferred refinements (captured, not scheduled)

These 🟡 findings from the gap analysis are acknowledged but not blocking:

- **Gap 9** — `.Guidance("...")` free-text advisories. Defer; LLMs parse `Description()` adequately today.
- **Gap 10** — Typed filter DSL replacing `RawFilterExpression`. Defer; ADR adds no new write paths exposing this.
- **Gap 11** — `IActionDispatchObserver` hook for `FabricQueryData` emission. **Land with §2.10.2**; use the same dispatch-completion seam to emit both constraint feedback and fabric-query telemetry.
- **Gap 12** — Case-role semantics on Action `Accepts<T>`. Defer unless router precision becomes bottlenecked.
- **Gap 13** — Instance-store metadata (N&R Fact DB analog). Defer; `IObjectSetProvider` mapping suffices.

### 2.14 Ingestion cost model

The source-repository ingestion pipeline that populates §1.3's completeness constraint is the most expensive component the ontology depends on, and its unit economics determine whether Basileus is viable as a multi-tenant SaaS product. Three grounding documents establish the cost envelope and the architectural choices that keep it viable:

- `docs/designs/2026-04-19-ingest-ontology-from-source.md` — the ingestion pipeline design
- `docs/research/2026-04-19-ontology-ingestion-cost-analysis.md` — per-push cost model
- `docs/research/2026-04-19-basileus-saas-workspace-efficiency.md` — SaaS unit-economics analysis; selects the **ephemeral Firecracker-hosted Roslyn worker** (M2c) as the primary architecture

The ADR adopts these SLOs as a coordination-architecture invariant — any ingestion implementation that doesn't meet them breaks the inversion thesis (§1.2) or the SaaS product (multi-tenant margin target ≥ 70%).

**Cost/latency SLOs (reference workload: 10 devs, 500k LOC .NET, ~200 pushes/day, 50 PRs/week):**

| Metric | SLO |
|---|---|
| Embedding API cost | < $0.05 per workspace per day (~$18/year) |
| p50 propagation latency (push → queryable) | 10 s |
| p95 propagation latency | 30 s |
| p99 under burst | 3 min (debouncer max-wait ceiling) |
| Cold-resume first-query (paused snapshot) | 5 s (Firecracker resume + diff-apply) |
| Cold-boot first-query (no snapshot — first tenant registration) | 60 s (initial `OpenSolutionAsync`) |
| Per-tenant worker CPU (ephemeral) | < 200 CPU-seconds/day |
| Per-tenant worker RAM (active ingest only, released at idle) | ~16 GB ephemeral |
| **Per-tenant COGS (steady state, Team tier)** | **≤ $15/month** |

**Cost controls (design §4.3.1, §4.8 + workspace-efficiency research §4):**

1. **Content-hash chunk cache** — `sha256(normalized_content, model_id, model_version)` keyed dedup. Without it, embedding cost is 4 orders of magnitude higher.
2. **Wolverine+Marten keyed debouncer** on `{install}:{repo}:{branch}`. Without it, a 10-push burst becomes 10 ingest jobs.
3. **Ephemeral Firecracker-hosted Roslyn worker with snapshot resume** — the Roslyn workspace is **not resident** in the Basileus service process. Per registered tenant, a Firecracker microVM holds a paused compilation snapshot. The debouncer resumes the worker (200–500 ms), the worker diff-applies the push and emits `OntologyDelta` events to Marten via the existing `IOntologySource` contract (strategos#37), and the worker returns to paused after a quiet period (default 5 min). This collapses per-tenant always-on RAM from 16 GB to ~0 while preserving semantic-binding features (`SymbolKey` correlation, IS-A, cross-project refs). Without it, SaaS gross margin falls below 20% at competitive price points; with it, gross margin at $99/mo Team tier is ~85% (research §6.1).

The mechanism and implementation details live in the ingestion design and workspace-efficiency research documents; the ADR records the ephemeral-worker architecture and the SLOs as commitments. A 2-sprint implementation spike (tracked on basileus#156) validates Firecracker snapshot resume latency, workspace-snapshot correctness across incremental applies, and vsock delta-streaming throughput before the final task breakdown is frozen.

---

## 3. Awareness × Coordination matrix (unchanged)

[Retained verbatim from v1 §3. Four integration tiers (Disconnected / Fabric-only / Full remote / Hybrid) × six awareness tiers (T0–T5), runtime × awareness table, coordination-tier table.]

---

## 4. `Strategos.Contracts` additions (expanded)

New TypeSpec models added to the canonical schema (basileus#152 / exarchos#1125). This is the full list after synthesis.

### 4.1 Ontological record lifecycle

```typespec
model IntentProposedData       { recordId, featureId, processLayer, delegationPolicy,
                                 ontologyVersion, branch?, prNumber? }                // branch + PR fields
model IntentValidatedData      { recordId, featureId, verdict: ValidationVerdict }    // NEW §2.5 gate
model IntentEnrichedData       { recordId, featureId, domainLayer: DomainLayer }
model IntentExecutingData      { recordId, featureId }
model IntentCompletedData      { recordId, featureId, tasksCompleted, tasksFailed, duration }
model IntentFailedData         { recordId, featureId, reason, failedAt }
model PrCorrelatedData         { recordId, prNumber, branch }                         // NEW — backfill event from webhook (§2.5)
```

### 4.2 Fabric query audit

```typespec
model FabricQueryData          { queryType, objectType?, resultCount, degraded, ontologyVersion }
enum  FabricQueryType          { ontologyQuery, designValidation, domainStateResolution, intentRegister }
```

### 4.3 Remote delegation

```typespec
model TaskDelegatedRemoteData  { taskId, featureId, target: "basileus", agentRole, reason, blastRadiusScope }
model CrossTierDependencyResolvedData
                               { dependentTaskId, resolvedByTaskId, resolvedByTier, branch, commitSha }
```

### 4.4 Validation contracts (NEW — from §2.10)

```typespec
model OntologyNodeRef          { domain, objectType, propertyName? }
model ConstraintEvaluation     { expression, description, actualValue, expectedValue,
                                 strength: ConstraintStrength, passed }
model ConstraintViolationReport{ actionName, hard: ConstraintEvaluation[],
                                 soft: ConstraintEvaluation[], suggestedCorrection? }
model BlastRadius              { directlyAffected: OntologyNodeRef[],
                                 transitivelyAffected: OntologyNodeRef[],
                                 crossDomainHops: CrossDomainHop[],
                                 scope: BlastRadiusScope }
enum  BlastRadiusScope         { Local, Domain, CrossDomain, Global }
model PatternViolation         { patternName, description, subject: OntologyNodeRef,
                                 severity: ViolationSeverity }
enum  ViolationSeverity        { Warning, Error }
model ValidationVerdict        { passed, hardViolations, softWarnings, blastRadius, patternViolations }
```

**Notification envelopes are still NOT added to `Strategos.Contracts`** (§2.3.4 from v1). They remain Exarchos-internal Zod types.

---

## 5. What this supersedes

| Prior artifact | Status | Reason |
|---|---|---|
| `docs/designs/2026-02-19-remote-notification-bridge.md` | Superseded for the notification surface | Three-layer model replaced by composite pipeline + sideband floor |
| basileus#122 three-layer notification model | Refactored | "Watcher teammate (Layer 3)" replaced by elicitation lift + sideband daemon; T1/T2 retained |
| Single `NotificationSink` interface (design §3.3) | Refactored | Split into segregated `InlineSink` / `AsyncSink` with composite pipeline |
| `bridge-config.json` (design §3.5) | Rejected | Consolidated into `.exarchos.yml` under `basileus:` |
| `hasRealtimeNotifications: bool` (design §3.3) | Refactored | Expanded to capability taxonomy + handshake-authoritative resolver |
| Watcher teammate / `exarchos_notify_wait` (#122 Layer 3) | Replaced | Sideband daemon serves the same goal at universal scope |
| **v1 §2.2 "Basileus Ontology MCP Server" with hand-written tools** | **Superseded** | v2 §2.2 mounts Strategos's existing tools at `/mcp/ontology`; no parallel reinvention |
| **v1 §2.5 "persisted as a `SemanticDocument`"** | **Superseded** | v2 §2.5 persists as a Marten event-sourced aggregate with summary-only fabric indexing |
| **v1 §4 event list (six models)** | **Expanded** | v2 §4 adds `IntentValidated`, validation contracts (`BlastRadius`, `PatternViolation`, `ValidationVerdict`, `ConstraintViolationReport`, `OntologyNodeRef`), and `ontologyVersion` threading |
| **v1's single `hasRealtimeNotifications` → capability taxonomy refactor** | Still valid, extended | §2.8 now also carries ontology graph version for cache invalidation |

The watcher teammate concept is dropped because (a) it was Claude-specific, (b) it cost ~$0.01/relay continuously, and (c) the sideband daemon delivers the same idle-session awareness for free on every runtime including Claude Code.

---

## 6. Implementation map (cross-repo issue board)

The issues below become the source of truth for execution. Each existing issue cites this ADR; new issues are filed where work has no current home.

### 6.1 Strategos (NEW — prerequisite work)

| # | Action | Scope |
|---|---|---|
| **NEW** `DispatchReadOnlyAsync` + `.ReadOnly()` DSL | **create** | §2.10.1 — interface extension, dispatcher default impl, DSL marker, AONT036 diagnostic. **Blocks basileus `fabric_resolve`.** |
| **NEW** Structured constraint feedback | **create** | §2.10.2 — promote `ConstraintEvaluation` into `Actions/`, extend `ActionResult` with `ConstraintViolationReport`, `IActionDispatchObserver` hook (folds in Gap 11). **Blocks `ontology_validate` usefulness.** |
| **NEW** `ontology_validate` tool + `ValidationVerdict` | **create** | §2.10.3 — `OntologyValidateTool`, `DesignIntent`, `ValidationVerdict`. Depends on the two above. |
| **NEW** Blast radius + pattern violation primitives | **create** | §2.10.4 — `IOntologyQuery.EstimateBlastRadius`, `DetectPatternViolations`, `BlastRadius`, `PatternViolation`. |
| **NEW** MCP 2025-11-25 conformance | **create** | §2.11 — `OntologyToolDescriptor` upgrade (`outputSchema`, `ToolAnnotations`, title), annotation matrix. |
| **NEW** Ontology graph versioning | **create** | §2.12 — `OntologyGraph.Version` hash, `_meta.ontologyVersion` threading. |
| strategos#16 (Ontology) | **link** | Parent epic; track all six new issues under it. |
| strategos#23 | **leave** | Tangential; no changes required. |

**Release target:** Strategos 2.5.0 as the coordination floor. Basileus §6.2 "Basileus Ontology MCP Endpoint" work starts only after 2.5.0 ships.

**Strategos 2.6.0 (hybrid retrieval seams — [strategos#47](https://github.com/lvlup-sw/strategos/issues/47); scope from `docs/designs/2026-04-19-retrieval-composition-for-ontology-mcp.md` §4):**

| # | Action | Scope |
|---|---|---|
| strategos#47 `IKeywordSearchProvider` + `RankFusion.Reciprocal` + `HybridQueryOptions` | **create** | Design §4 (DR-1/2/3) — parent issue covering all three seams; TypeSpec ships via basileus#152 + exarchos#1125 pipeline |

**Release target:** Strategos 2.6.0 cut post-2.5.0. Basileus hybrid-composition epic (basileus#167) blocks on 2.6.0.

### 6.2 Basileus

| # | Action | Scope after this ADR |
|---|---|---|
| #119 Streaming Sync Engine | **update** | Cite §2.1 MCP Client A; correct the cross-repo reference (was `lvlup-sw/basileus#66`, should be exarchos#66); supersede the 2026-02-19 design for the notification surface only |
| #120 Remote Event Types | **update** | Cite §4 — full list including `IntentValidated`, validation contracts, `ontologyVersion` threading |
| #121 Task Router | **update** | Cite §2.6 + read `domainLayer.blastRadius.scope` for local-vs-remote bias |
| #122 Notification Delivery Layer | **major refactor** | Replace 3-layer model with composite pipeline §2.3 + sideband daemon §2.4 + tier matrix §3. Drop watcher teammate. Move `exarchos watch` work to exarchos-side issue |
| #123 Cross-Session Coordination | **update** | Cite §2.6 + reframe in terms of cross-tier dependency resolution events |
| #124 Agentic Coder Dispatch | **update** | Cite §2.6 hybrid delegation + §2.5 ontological record handoff |
| #145 Data fabric ingestion | **link** | Fabric ingestion of the *record summary* (not full record) — see §2.5 |
| #147 Phronesis Code Review | **update** | Cite §2.2 — Ontology MCP endpoint gives the agent its enrichment surface; "trigger surface" resolves via `intent_register` + `task.completed` events |
| #152 Strategos.Contracts companion package | **update** | Cite §4 full list (record lifecycle + fabric query + remote delegation + validation contracts) |
| **NEW** Epic: Basileus Ontology MCP **Endpoint** | **create** | §2.2 — wire `OntologyExploreTool` / `OntologyQueryTool` / `OntologyValidateTool` (from Strategos 2.5) + `fabric_resolve` + `intent_register` (Basileus-new). Blocked by §6.1. |
| **NEW** Ontological Record aggregate + lifecycle | **create** | §2.5 — Marten event stream, Wolverine saga, status state machine, `SemanticDocument` summary indexer, `IOntologicalRecordService`. Depends on #152. |
| **NEW** `IntentProposedHandler` + validation gate | **create** | §2.5 — Wolverine handler; calls `ontology_validate`, emits `intent.validated`, routes to `intent.enriched` or rejection. |
| **NEW** Ontology completeness CI gate | **create** | §1.3 — build-time check: public domain types in `Basileus.{Trading,StyleEngine,Knowledge}` must appear in `OntologyGraph.ObjectTypes`. Failure blocks merge. *Design in separate ideation `ingest-ontology-from-source`.* |
| #167 Epic: Hybrid retrieval composition | **create** | `docs/designs/2026-04-19-retrieval-composition-for-ontology-mcp.md` §11 — `PostgresTsVectorKeywordSearchProvider`, `CohereReranker` (Azure AI Foundry MaaS), `BoundedGraphExpander` (1-hop), `IOntologyVersionedCache<TKey, TValue>` + `MemoryOntologyVersionedCache`, `AzureAiSearchKeywordSearchProvider` (fallback, feature-gated), pipeline orchestration with BM25-saturation early-exit, MCP tool parameter + `_meta` envelope enrichment, qrel-set measurement harness, OTel metrics + burn alerts. **Blocked by Strategos 2.6.0 (strategos#47).** Resolves ADR §9.8 + §9.9. |
| #112 Cross-Tier Event Bridge | **leave** | Orthogonal — ControlPlane↔AgentHost SSE durability, not Exarchos↔Basileus |

### 6.3 Exarchos

| # | Action | Scope after this ADR |
|---|---|---|
| #1109 v2.8–v3.0 cross-cutting | **update** | Cite §2.8 — basileus-forward constraint means MCP handshake-authoritative capability resolution AND ontology-version-aware cache invalidation |
| #1125 Strategos.Contracts pipeline | **update** | Cite §4 full model list for contract pipeline emission |
| **NEW** Epic: NotificationPipeline composite refactor | **create** | §2.3 |
| **NEW** Epic: `exarchos watch` sideband daemon | **create** | §2.4 |
| **NEW** ElicitationSink + escalation lift | **create** | §2.3.5 |
| **NEW** Capability resolver (yaml ⊕ handshake) | **create** | §2.8 + §2.12 ontology version capture |
| **NEW** PiggybackSink + `_notifications` envelope | **create** | §2.3.4 |
| **NEW** `.exarchos.yml` consolidation (`basileus:` section) | **create** | §2.7 |
| **NEW** Skills bootstrap pull (capability-guarded) | **create** | §2.9 |
| **NEW** Fabric-action surface in `exarchos_sync` | **create** | Actions: `query_fabric` (→ `ontology_query` + `fabric_resolve`), `validate_design` (→ `ontology_validate`), `register_intent` (→ `intent_register`), `get_record` (→ record stream read). Proxy through MCP Client B with graceful-degradation responses. |
| **NEW** Ontology cache + version invalidation | **create** | §2.12 — Exarchos-side cache of `ontology_explore` results, invalidated on `_meta.ontologyVersion` mismatch. |
| **NEW** Hybrid retrieval parameter pass-through in `exarchos_sync` | **update** | Retrieval design §11 — extend `query_fabric` proxy wiring to pass through `precision`, `followLinks`, `linkDepth`, `chunkLevel`, `provenance` parameters when Basileus reports `ontology_query` hybrid capability in the MCP `initialize` response. Graceful degradation: if the upstream response lacks `_meta.hybrid`, call stays semantic-only. |

### 6.4 Strategos.Contracts

No new issues needed. The package itself is owned by basileus#152 / exarchos#1125; the new TypeSpec models in §4 ship through that pipeline. `ontology-to-tools` and `ontological-semantics` reference docs in the strategos repo are updated to reflect the new `ValidationVerdict` / `BlastRadius` contracts.

---

## 7. Consequences

### Positive

- **Top-down planning is economically viable — quantitatively.** Per-session discovery cost drops from O(codebase) to O(schema). Every `/ideate` starts with authoritative schema, not archaeology. Steady-state embedding cost for the reference workload (10 devs, 500k LOC, 50 PRs/week): **< $0.05 per workspace per day** (~$18/year) after the chunk cache warms. Propagation latency from push to `ontology_query`: **p50 10s, p95 30s, p99 3 min under burst** — sits comfortably inside the "agents query the ontology freely" latency regime.
- **Coordination via shared map, not shared guesses.** Exarchos local and Basileus remote agents query the same ontology and operate against the same `OntologicalRecord`. "Same contracts, different scope" becomes runtime-enforced, not aspirational.
- **Validation shifts left.** `intent.validated` gate rejects designs before `/delegate` — blast radius, constraint violations, and pattern violations surface pre-commit, not post-merge.
- **Refactor-stable agent memory.** The agent's map is phrased against declared ontology names, not source strings — so the churn that wrecks grep-based recall surfaces as build errors, rename deltas, or a version-hash mismatch rather than silent drift.
- **Generic-runtime parity.** (Unchanged from v1.) OpenCode / generic MCP clients get the same proactive-awareness floor as Claude Code.
- **Single source of truth for MCP tool surfaces.** Strategos owns ontology tools; Basileus owns record management; no duplicate `ontology_query`.
- **Observable degradation.** (Unchanged from v1.) `degradedSinks` visible in next tool response.
- **Fabric-only tier is real.** Independent Ontology channel works without Workflow channel.
- **Cost-optimized for SaaS by design.** Three composed mechanisms carry the unit economics: (1) content-hash chunk cache turns embedding from ~$1,625/yr naive into ~$1–$11/yr; (2) paused-Firecracker snapshot resume turns per-ingest latency from 2–5 minutes cold-open into ~500 ms resume + diff-apply; (3) ephemeral-worker lifecycle turns per-tenant always-on RAM from 16 GB into ~200 CPU-seconds/day active-only. Steady-state per-tenant COGS at the Team tier lands at ~$15/mo against a $99/mo price point (≈85% gross margin). See §2.14 and the SaaS-efficiency research for the full model.

### Negative / costs

- **Strategos release coupling.** Basileus cannot ship its §2.2 endpoint until Strategos 2.5.0 lands §2.10/§2.11/§2.12. This is a coordination-floor dependency — six new strategos issues gate the Basileus epic.
- **Completeness risk.** Top-down confidence is as good as the ontology is complete. Incomplete registration yields silent blind spots — worse than bottom-up archaeology. The `ingest-ontology-from-source` ideation (§9.1) is the load-bearing mitigation; until it lands, the ontology-completeness CI gate (§6.2 new) enforces coverage by build failure.
- **Lifecycle complexity.** (Unchanged from v1.) Pipeline lifecycle bound to MCP handshake; now also bound to ontology graph version.
- **New process to install** (`exarchos watch`). (Unchanged from v1.)

### Neutral

- **`exarchos_sync subscribe` now carries two responsibilities** (event streaming + escalation UX). (Unchanged from v1.)
- **`ontology_action` write path deliberately excluded from `/mcp/ontology`.** Writes continue to flow through Phronesis. Exarchos can still invoke actions via Workflow-channel delegation that ultimately calls the dispatcher — the exclusion is about the Basileus-facing tool surface, not about denying Exarchos write capability.

---

## 8. Decision summary: what changed from v1

| Area | v1 decision | v2 decision | Why |
|---|---|---|---|
| §1 framing | "Runtime coordination surface" | **Top-down inversion thesis** | Research surfaced the underlying thesis; naming it explicitly aligns every decision |
| §2.2 tool surface | 4 new hand-written tools on a new server | 6 tools on a mount; 3 reuse Strategos; 1 deliberately excluded; 2 new | Gap 1 — reinvention would collide with shipped Strategos tools |
| §2.2 read-only semantics | Implicit | `DispatchReadOnlyAsync` with interface guarantee | Gap 2 — ADR referenced a method that doesn't exist |
| §2.2 validation feedback | Implicit | `ConstraintViolationReport` on `ActionResult` + `ValidationVerdict` on `ontology_validate` | Gap 3 — OTC paper's empirical F1 gain |
| §2.2 blast radius | "Pattern violations" mentioned; no primitive | `EstimateBlastRadius` + `DetectPatternViolations` as first-class Strategos queries | Gap 4 — shift-left validation needs the primitive |
| §2.5 persistence | "Persisted as a `SemanticDocument`" | Marten event stream; summary indexed in `SemanticDocument` | Gap 6 — `SemanticDocument` shape doesn't fit |
| §2.5 lifecycle | `proposed → enriched → completed` | `proposed → **validated** → enriched → executing → completed/failed` | New validation gate is the shift-left mechanism |
| §2.8 capability resolver | Handshake-authoritative for MCP capabilities | Also captures `ontologyVersion` | Gap 7 — cache invalidation requires version signal |
| §2.11 MCP conformance | Silent | Explicit `outputSchema` + `ToolAnnotations` + annotation matrix | Gap 5 — Cursor/Copilot/Codex auto-approval gating |
| §1.3 ontology completeness | Not mentioned | Explicit constraint; CI gate; separate ideation to design ingestion | Research surfaced this as the load-bearing unnamed risk |
| §6 implementation map | Basileus + Exarchos sections | Strategos (new) + Basileus + Exarchos; Strategos 2.5.0 as coordination floor | Basileus work blocked on Strategos refinements |

---

## 9. Open questions

These remain open and tracked on the relevant issues:

1. **Ontology completeness: how do we populate the ontology from source repositories?** This ADR assumes domain types are registered in `Strategos.Ontology` but offers no mechanism beyond "write the DSL by hand." For the top-down inversion (§1.2) to be trustworthy, coverage must scale as codebases grow. Tracked in a **separate ideation workflow** (`ingest-ontology-from-source`) that will design:
   - Static analysis to identify domain types lacking ontology registration
   - Scaffolding generation for DSL entries from existing C# types
   - Incremental ingestion as a CI step
   - Drift detection between source truth and ontology truth
   - Interaction with the §6.2 ontology-completeness CI gate
2. **Should `ontology_action` be exposed on `/mcp/ontology` at all?** Writing to the domain goes through Phronesis `ThinkStep → ActStep`. Exposing `ontology_action` over MCP to Exarchos creates a second write path that bypasses orchestration. Recommendation (this ADR): keep it out of `/mcp/ontology` until a concrete use case emerges. Revisit in v3.
3. **Sideband daemon: opt-in or mandatory on non-Claude runtimes?** (Unchanged from v1.) Recommendation: opt-in but auto-prompt.
4. **Reciprocal Basileus-hosted webhook endpoint?** (Unchanged from v1.)
5. **Cryptographic signing of ontological records.** (Unchanged from v1.) Does the piggyback envelope need a verifiable digest? Now additionally: does the `OntologicalRecord.id` → ontology-version binding need a signature so Exarchos can verify it was validated against a known-good schema?
6. **Runtime detection for sideband daemon.** (Unchanged from v1.)
7. **Ontology-version skew during active workflow.** If `OntologicalRecord` is created at `ontologyVersion = A` and the graph updates to `B` mid-workflow, does `/delegate` reject the record, re-validate, or proceed? Recommendation: re-validate on transition from `enriched → executing`; re-validation failure routes to `failed` with reason = `ontology-drift`.
8. **~~Hybrid retrieval composition for `ontology_query(semanticQuery=...)`~~ — RESOLVED by `docs/designs/2026-04-19-retrieval-composition-for-ontology-mcp.md`.** The design commits to Framing A (external-agent MCP parity), Layering C (Strategos 2.6.0 thin seams: `IKeywordSearchProvider` + `RankFusion.Reciprocal` + `HybridQueryOptions`; Basileus supplies Azure-specific implementations), Shape 2/3 optimization, and Approach 3 (fixed parameter-driven pipeline with one BM25-saturation early-exit). Composition: (a) Postgres `tsvector` inline with pgvector, Azure AI Search as feature-gated fallback; (b) RRF `k=60` with `sparseTopK`/`denseTopK=50` by default; (c) Cohere Rerank v3.5 via Azure AI Foundry serverless MaaS, opt-in via `precision` parameter (default `true`); (d) 1-hop `BoundedGraphExpander` via `OntologyGraph.TraverseLinks`, context-only (not re-ranked). Gated ship by measurement harness on a 75-query qrel set (Shape 2 nDCG@10 ≥ 0.80 goal 0.86; Shape 3 Recall@10 ≥ 0.85; p95 < 400 ms; Cohere cost ≤ $7/workspace/month).
9. **~~Ontology-versioned retrieval cache abstraction~~ — RESOLVED by the same design.** `IOntologyVersionedCache<TKey, TValue>` lives in `Basileus.AgentHost.Abstractions/DataFabric/Retrieval/`; default `MemoryOntologyVersionedCache<TKey, TValue>` backed by `MemoryCache` with bounded LRU in `Basileus.Infrastructure`. Caches both composed-graph and RRF fused-result surfaces keyed on `(workspace, branch, ontologyVersion, queryHash, paramHash)`. Stale-guard on version mismatch (miss even if key present) is the invariant. Invalidation signal: `_meta.ontologyVersion` mismatch at the MCP boundary drives `InvalidateStale(currentOntologyVersion)`; `notifications/resources/updated` (Gap 8) remains deferred as a subscription-model future enhancement.

---

## 10. Related

- Strategic Framing: Exarchos × Basileus × Strategos
- Coordination design (input)
- Platform-agnostic coordination research (input)
- Strategos ontology gap analysis (input, this rev)
- Source ingestion design — §1.3 completeness mechanism; §2.14 cost model implementation
- Ontology ingestion cost analysis — grounding for §2.14 SLOs
- Data shape → query performance and relevance — grounding for ingest-design §4.4 chunking/metadata/index choices; surfaces the compositional work (hybrid retrieval) resolved by the follow-on design
- Retrieval composition for the Ontology MCP Endpoint — resolves ADR open questions §9.8 and §9.9; commits Strategos 2.6.0 seams + Basileus Azure-native implementations of hybrid BM25+vector+RRF+rerank+bounded-graph-expansion pipeline
- [Platform Architecture §11.7 Channel integration](/strategos/reference/platform-architecture/#117-claude-code-channel-integration)
- Distributed SDLC Pipeline §12 Basileus integration
- Data Fabric & Ontology Context
- Ingestion Concept
- Strategos ontology theoretical grounding (N&R)
- Strategos ontology-to-tools grounding (Zhou et al.)
- MCP spec: [schema](https://modelcontextprotocol.io/specification/2025-11-25/schema), [tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools), [tool annotations](https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/), [logging](https://modelcontextprotocol.io/specification/2025-11-25/server/utilities/logging), [elicitation](https://modelcontextprotocol.io/specification/2025-11-25/client/elicitation), [progress](https://modelcontextprotocol.io/specification/2025-11-25/basic/utilities/progress)
- [MCP client capability matrix](https://mcp-availability.com/)
- [Claude Code Channels documentation](https://code.claude.com/docs/en/channels)
- Claude Code WONTFIX precedents: [#3174](https://github.com/anthropics/claude-code/issues/3174), [#7252](https://github.com/anthropics/claude-code/issues/7252), [#7108](https://github.com/anthropics/claude-code/issues/7108)
- Sideband precedent: [`zudsniper/mcp-notifications`](https://github.com/zudsniper/mcp-notifications)
