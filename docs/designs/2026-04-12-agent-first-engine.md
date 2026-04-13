# Agent-First Engine Framework

**Date:** 2026-04-12
**Depends on:** v1 MCP Agent Server (`docs/designs/2026-04-11-mcp-agent-server.md`), v2 scope (issue #35)
**Workflow:** `agent-first-engine` (exarchos)
**Scope:** Elevate agentic development from an optional dev tool (v1) to a first-class engine capability. External agents (Claude Code, bespoke harnesses) become co-designers that can reason about game design quality — not just observe and interact, but interrogate the engine's design space, run experiments, and evaluate outcomes against declared design intent.

---

## 1. Context

### 1.1 Where v1/v2 leave off

v1 ships a **dev co-pilot**: Claude Code attaches to a running Valkyrie window, inspects the UI via a Game DOM, issues clicks/commands, and observes events. v2 (issue #35) expands to **gameplay research**: headless execution, expanded tactical vocabulary, event streaming, screenshots, command replay.

Both treat the agent as an *operator* — it can see and touch the game, but it doesn't understand the game's *design*. An agent running v2 can move units and observe outcomes, but it can't answer: "Is WedgeFormation dominated by BoxFormation in open terrain?" or "Does SliceCorner have a counter-play gap?" without the developer manually encoding that knowledge into prompts.

### 1.2 The gap

The missing layer is **design-level semantics**: machine-readable descriptions of what the engine's tactical abstractions *are*, how they relate, what constraints they carry, and what the designer *intended*. This is an ontology — a typed, linked, constraint-annotated graph of the engine's design concepts.

### 1.3 The key insight: Strategos.Ontology already provides this

The lvlup-sw Strategos library (`../strategos`) provides a production-grade ontological layer:

- **Fluent builder** (`DomainOntology.Define(IOntologyBuilder)`) for declaring object types, properties, links, actions, events, lifecycles, interfaces, and constraints
- **Immutable runtime graph** (`OntologyGraph`) frozen at startup, indexed for fast lookup
- **Query algebra** (`ObjectSet<T>`, `IOntologyQuery`) for traversal, filtering, constraint evaluation, and derivation chain analysis
- **MCP tool surface** (`ontology_explore`, `ontology_query`, `ontology_action`) for agent interaction — already built
- **Design-intent metadata**: preconditions (hard/soft), postconditions, lifecycle state machines with transitions, link cardinality, interface polymorphism

Building a parallel "Design Registry" in Valkyrie would be a DIM-5 HIGH violation (divergent implementation of the same behavior) and a DIM-6 concern (two metadata registries with no reconciliation). Instead, Valkyrie integrates Strategos.Ontology as its design-knowledge layer.

### 1.4 The impedance mismatch (and why it's solvable)

Strategos.Ontology was designed for Wolverine/Marten-backed persistence (async, document store, event sourcing). Valkyrie is a synchronous, single-threaded, zero-alloc game loop. The mismatch is real but bounded:

| Strategos assumption | Valkyrie reality | Bridge |
|---|---|---|
| `IObjectSetProvider.ExecuteAsync<T>()` | In-memory component stores | Provider reads from `IFrameStateProvider` snapshot on worker thread (already async) |
| `IActionDispatcher.DispatchAsync()` | `TacticalCommandQueue.Publish<T>()` | Dispatcher enqueues to `CommandIntakeQueue` (already cross-thread) |
| `IEventStreamProvider.QueryAsync()` | `TacticalEventStore` ring buffer | Provider reads from `FrameState.RecentEvents` snapshot |
| Persistent document store | Transient in-memory state | Provider implements `IObjectSetProvider` without persistence — objects live in the game loop, queryable via frame snapshots |

The v1 MCP architecture already solves the async/sync bridge: MCP tool handlers run on ASP.NET Core worker threads and interact with the game loop via `IFrameStateProvider` (reads) and `CommandIntakeQueue` (writes). Strategos providers sit on the same side as tool handlers — no new bridging pattern needed.

### 1.5 Strategos extensions required

Strategos.Ontology's provider interfaces are abstract (they're interfaces, not Wolverine/Marten-coupled), but some capabilities may need extension to serve the game-loop use case well:

1. **In-memory `ObjectSetExpression` evaluator.** Strategos.Ontology.Npgsql translates expression trees to SQL. Valkyrie needs an evaluator that runs `Where`, `TraverseLink`, `OfInterface` against in-memory collections. This may warrant a `Strategos.Ontology.InMemory` package (useful beyond Valkyrie — test harnesses, embedded scenarios).

2. **Snapshot-scoped query semantics.** Strategos assumes queries see the latest persistent state. Valkyrie's queries see a point-in-time frame snapshot. The provider contract may need a `QueryContext` that carries a snapshot version/timestamp, or it may be sufficient for the Valkyrie provider to capture the snapshot at query start (consistent-read semantics via `IFrameStateProvider.Current`).

3. **Fire-and-forget action dispatch.** Strategos' `IActionDispatcher.DispatchAsync` returns `ActionResult`. Valkyrie's v1 command pipeline is fire-and-forget (enqueue, return `{ accepted: true }`). The dispatcher implementation returns an accepted `ActionResult` immediately; v2's result-blocking variant (issue #35 item #10) maps to a future Strategos extension where dispatch can optionally await completion.

4. **Non-persistent `IObjectSetWriter`.** Strategos' `IObjectSetWriter.StoreAsync<T>()` assumes durable writes. Valkyrie's writer would mutate in-memory game state (e.g., spawn a unit, change a formation parameter). The write semantics are "apply to current simulation" not "persist to database." This may need a documented contract clarification in Strategos.

These extensions are independently valuable and can be developed in Strategos in parallel with Valkyrie's integration work. Valkyrie defines the contract it needs; Strategos evolves to support it.

---

## 2. Architecture

Three layers, each independently valuable, each amplifying the others:

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 3: Semantic Telemetry                                │
│  Causal event chains, outcome metrics, aggregation          │
│  "What actually happened, and why"                          │
├─────────────────────────────────────────────────────────────┤
│  Layer 2: Programmable Sandbox                              │
│  Scenario authoring, headless execution, parameter sweeps   │
│  "Test whether a design hypothesis holds"                   │
├─────────────────────────────────────────────────────────────┤
│  Layer 1: Strategos.Ontology Integration                    │
│  Design-intent metadata, relationships, constraints         │
│  "What should be true about the game's design"              │
├─────────────────────────────────────────────────────────────┤
│  v1/v2: MCP Agent Server                                    │
│  Game DOM, commands, events, tools                          │
│  "See and touch the running game"                           │
└─────────────────────────────────────────────────────────────┘
```

**Why this order matters:** Layer 1 (ontology) provides the *questions* — "WedgeFormation declares it's countered by BoxFormation; is that true?" Layer 2 (sandbox) provides the *answers* — "run 100 simulations and measure." Layer 3 (telemetry) provides the *explanations* — "WedgeFormation lost because coherence dropped at tick 47 when the point unit was flanked." Without Layer 1, agents have power but no direction. Without Layer 2, agents have questions but no laboratory. Without Layer 3, agents have outcomes but no understanding.

### 2.1 Assembly layout and dependency direction

```
Strategos.Ontology                    (NuGet — no Wolverine/Marten dependency)
    ▲
    │
Valkyrie.TacticalAI.Core             (NO ontology dependency — stays clean)
    ▲
    │
Valkyrie.TacticalAI.MonoGame          (existing)
    ▲
    │
Valkyrie.TacticalAI.MonoGame.Agent    (references Core + MonoGame + Strategos.Ontology)
    ├─ TacticalOntology : DomainOntology
    ├─ GameLoopObjectSetProvider : IObjectSetProvider
    ├─ CommandQueueActionDispatcher : IActionDispatcher
    ├─ FrameStateEventStreamProvider : IEventStreamProvider
    ├─ ScenarioRunner (Layer 2)
    └─ TelemetryAggregator (Layer 3)
```

**Key property preserved:** Core has zero ontology dependency. The ontology *describes* Core's types externally — `DomainOntology.Define()` uses CLR types as markers without requiring those types to implement any Strategos interface. A release build without the Agent assembly compiles and runs identically. The isolation property from v1 is maintained.

---

## 3. Layer 1: Strategos.Ontology Integration

### 3.1 `TacticalOntology : DomainOntology`

A single ontology definition class in the Agent assembly that registers Valkyrie's tactical abstractions into the Strategos graph. This is where design intent becomes machine-readable.

**Domain structure:**

| Domain | Object Types | Purpose |
|---|---|---|
| `formations` | `WedgeFormation`, `BoxFormation`, `LineFormation`, `FileFormation`, `StackFormation` | Formation patterns with parameters, constraints, and relationships |
| `behaviors` | `SliceCorner`, `RoomEntry`, `ButtonHook`, `TacticalMove` | Maneuver executors with lifecycle state machines |
| `threats` | `ThreatType` (18 variants), `ThreatAssessment` | Threat model with weights and detection parameters |
| `units` | `TacticalEntity`, `TacticalTeam` | Runtime unit instances and fire teams |
| `terrain` | `EnvironmentFeature`, `GridCell` | Map features and spatial data |

**Example ontology definition (illustrative, not final API):**

```csharp
public sealed class TacticalOntology : DomainOntology
{
    public override void Define(IOntologyBuilder builder)
    {
        // === Formations Domain ===
        var formations = builder.Domain("formations");

        var wedge = formations.ObjectType<WedgeFormation>("WedgeFormation")
            .Kind(ObjectKind.Configuration)
            .Property(f => f.Spacing)
            .Property(f => f.PointOffset)
            .Property(f => f.MinUnits).Computed()
            .Property(f => f.MaxUnits).Computed();

        wedge.Action("apply")
            .Description("Apply wedge formation to a fire team")
            .Accepts<ApplyFormationRequest>()
            .BoundToTool()
            .Requires("team.Size >= 3", "Requires minimum 3 units")
            .Modifies("team.Formation");

        wedge.HasMany<TacticalEntity>("members")
            .WithCardinality(3, 7);

        // Design-intent links
        wedge.HasMany<BoxFormation>("countered_by")
            .Description("BoxFormation's perimeter coverage neutralizes wedge's concentrated advance");
        wedge.HasMany<SliceCornerExecutor>("synergizes_with")
            .Description("Wedge's point unit naturally leads corner-clearing maneuvers");

        // === Behaviors Domain ===
        var behaviors = builder.Domain("behaviors");

        var sliceCorner = behaviors.ObjectType<SliceCornerExecutor>("SliceCorner")
            .Kind(ObjectKind.Behavior);

        sliceCorner.Lifecycle<ManeuverPhase>()
            .InitialState(ManeuverPhase.Approach)
            .TerminalState(ManeuverPhase.Complete)
            .Transition(ManeuverPhase.Approach, ManeuverPhase.Clear, "corner_reached")
            .Transition(ManeuverPhase.Clear, ManeuverPhase.Complete, "sector_clear");

        sliceCorner.Action("execute")
            .Requires("has_line_of_sight", "Requires LOS to corner")
            .Requires("team.Size >= 2", "Minimum 2 units for corner clear");

        // === Cross-domain links ===
        builder.CrossDomainLink("formations", "WedgeFormation", "enables", "behaviors", "SliceCorner")
            .Description("Wedge point position enables corner-clearing entry");
    }
}
```

### 3.2 Provider implementations

Three provider classes in the Agent assembly, each bridging Strategos' query/dispatch interfaces to Valkyrie's game-loop infrastructure:

#### `GameLoopObjectSetProvider : IObjectSetProvider`

Translates `ObjectSetExpression` trees into queries against the current frame snapshot.

- `ExecuteAsync<T>(expression)` reads from `IFrameStateProvider.Current`, evaluates `Where` predicates against in-memory collections, traverses links via the ontology graph, returns results.
- **Data source mapping:** Each `ObjectTypeDescriptor` maps to a specific component store or registry in the engine:
  - Formation types → static registry (configuration objects, not per-frame state)
  - `TacticalEntity` → `TacticalWorld.Positions` + `TacticalWorld.TeamIds` (frame snapshot)
  - `TacticalTeam` → engine's team registry (frame snapshot)
  - `ThreatType` → static enum registry with configured weights
  - `EnvironmentFeature` → `GridFeatureDetector` results (map-load-time, cached)

- **Snapshot consistency:** Every query reads from a single `FrameState` captured at query start via `IFrameStateProvider.Current`. No mid-query state changes visible.

#### `CommandQueueActionDispatcher : IActionDispatcher`

Maps ontology action dispatch to Valkyrie's command pipeline.

- `DispatchAsync(context, request)` creates the corresponding `ITacticalCommand`, enqueues it via `CommandIntakeQueue.TryEnqueue()`, returns `ActionResult.Accepted`.
- **Action → Command mapping** registered at initialization: `"apply"` on `WedgeFormation` → `SetFormationCommand`, `"execute"` on `SliceCorner` → maneuver initiation command, etc.
- **Precondition evaluation** happens before enqueue: the dispatcher reads the current frame snapshot, evaluates the action's hard preconditions against live state, and rejects with a constraint report if any fail. Soft preconditions emit warnings but don't block.

#### `FrameStateEventStreamProvider : IEventStreamProvider`

Reads tactical events from the frame snapshot's event history.

- `QueryAsync(query)` filters `FrameState.RecentEvents` by event type, time range, and entity ID.
- **Limitation vs. Marten:** No persistent event history — only the ring buffer's capacity (configurable, default 256 events). Sufficient for real-time agent interaction; long-session replay requires Layer 2's scenario recorder (or a future event-log-to-disk feature).

### 3.3 MCP tool surface integration

With Strategos providers wired, the three ontology MCP tools become available alongside Valkyrie's existing tools:

| Tool | What agents get |
|---|---|
| `ontology_explore` | "Show me all formations, their parameters, constraints, and relationships" — the design encyclopedia |
| `ontology_query` | "Find all formations where MinUnits <= 3 and traverse the `countered_by` links" — design graph queries |
| `ontology_action` | "Apply WedgeFormation to team Alpha" — dispatched through the existing command pipeline |

These complement (not replace) Valkyrie's v1 tools (`get_ui_snapshot`, `click_element`, `move_unit`, etc.). The ontology tools operate at the *design* level; the v1 tools operate at the *interaction* level.

### 3.4 Agent workflow example

An agent co-designing Bronze Rebellion's tactical balance:

```
Agent: ontology_explore { scope: "objectTypes", domain: "formations" }
→ Returns 5 formations with parameters, constraints, links

Agent: ontology_query { objectType: "WedgeFormation", include: "links" }
→ Returns WedgeFormation with countered_by: [BoxFormation], synergizes_with: [SliceCorner]

Agent: "WedgeFormation has no declared counter-play from LineFormation.
        The counter-play graph shows LineFormation is isolated —
        it neither counters nor is countered by any formation.
        Is this intentional or a gap?"

Developer: "Gap — LineFormation should counter WedgeFormation in open terrain
            because its spread neutralizes the concentrated point."

Agent: [Developer updates ontology; agent validates via Layer 2 sandbox]
```

---

## 4. Layer 2: Programmable Sandbox

### 4.1 Purpose

Layer 1 tells agents what the design *declares*. Layer 2 lets agents test whether reality matches. The sandbox provides scenario authoring, headless execution, and parameter sweep infrastructure.

### 4.2 Core types

#### `ScenarioDefinition`

```csharp
public sealed record ScenarioDefinition
{
    public required string Name { get; init; }
    public required TerrainTemplate Terrain { get; init; }
    public required IReadOnlyList<TeamSetup> Teams { get; init; }
    public required VictoryCondition WinCondition { get; init; }
    public int MaxTicks { get; init; } = 3600;  // 60 seconds at 60Hz
    public int RngSeed { get; init; } = 0;       // 0 = random
    public IReadOnlyDictionary<string, float>? ParameterOverrides { get; init; }
}

public sealed record TeamSetup
{
    public required int TeamId { get; init; }
    public required string Formation { get; init; }  // ontology object type name
    public required int UnitCount { get; init; }
    public required Vector2 SpawnPosition { get; init; }
    public string? Behavior { get; init; }            // ontology behavior name
    public IReadOnlyDictionary<string, float>? ThreatWeightOverrides { get; init; }
}
```

#### `ScenarioResult`

```csharp
public sealed record ScenarioResult
{
    public required string ScenarioName { get; init; }
    public required ScenarioOutcome Outcome { get; init; }  // Win/Loss/Draw/Timeout
    public required int WinningTeamId { get; init; }
    public required int TickCount { get; init; }
    public required TimeSpan WallClockDuration { get; init; }
    public required IReadOnlyList<TeamSummary> TeamSummaries { get; init; }
    public required IReadOnlyList<EngagementSummary> Engagements { get; init; }
    public IReadOnlyList<ITacticalEvent>? EventLog { get; init; }  // opt-in, memory cost
}

public sealed record TeamSummary
{
    public required int TeamId { get; init; }
    public required int UnitsAlive { get; init; }
    public required int UnitsLost { get; init; }
    public required float FormationCoherenceAvg { get; init; }
    public required float ThreatResponseLatencyAvg { get; init; }
}
```

#### `ScenarioRunner`

Boots a headless `TacticalAIEngine` (no `Game`, no `GraphicsDevice`), ticks to completion or timeout, returns `ScenarioResult`.

- Depends on v2 headless mode (issue #35 item #2) — engine tick decoupled from MonoGame lifecycle
- Deterministic: seeded `Random` injected into all stochastic systems
- Configurable tick rate: default max-speed (no frame timing), bounded by `MaxTicks`
- Memory-bounded: reuses a single engine instance across scenarios in a sweep (reset between runs)

#### `ParameterSweep`

```csharp
public sealed record SweepDefinition
{
    public required ScenarioDefinition BaseScenario { get; init; }
    public required IReadOnlyList<SweepAxis> Axes { get; init; }
    public int SeedsPerCombination { get; init; } = 50;
}

public sealed record SweepAxis
{
    public required string ParameterPath { get; init; }  // e.g. "teams[0].formation", "threats.flanking.weight"
    public required IReadOnlyList<object> Values { get; init; }
}

public sealed record SweepResult
{
    public required IReadOnlyList<SweepCell> Cells { get; init; }
    // Each cell: parameter combination + aggregated results across seeds
}
```

### 4.3 MCP tools

| Tool | Parameters | Returns |
|---|---|---|
| `create_scenario` | `ScenarioDefinition` | Validated scenario ID |
| `run_scenario` | `scenario_id`, optional `seed` | `ScenarioResult` |
| `sweep_parameters` | `SweepDefinition` | `SweepResult` matrix |
| `compare_results` | `result_id_a`, `result_id_b` | Diff summary (win rate delta, coherence delta, etc.) |

### 4.4 Integration with Layer 1

The ontology amplifies the sandbox:

- `ontology_explore` tells the agent what formations, behaviors, and threat types exist → agent constructs `ScenarioDefinition` using ontology type names
- Ontology constraints validate scenarios before execution: "WedgeFormation requires >= 3 units but you specified 2" → rejected at `create_scenario` with constraint report
- Ontology relationships suggest what to test: "WedgeFormation declares `countered_by: BoxFormation` — run the matchup" → agent generates the sweep automatically
- Sweep results feed back into ontology validation: "WedgeFormation vs BoxFormation win rate is 50/50 — the declared counter-play relationship may be inaccurate"

---

## 5. Layer 3: Semantic Telemetry

### 5.1 Purpose

Layers 1 and 2 tell agents what SHOULD be true and whether it IS true. Layer 3 explains WHY — causal event chains that trace outcomes back to root causes.

### 5.2 Causal event chains

Extend `ITacticalEvent` with an optional causal link:

```csharp
public interface ITacticalEvent
{
    // Existing members unchanged

    /// <summary>
    /// Optional reference to the event that directly caused this event.
    /// Null for root-cause events (e.g., player commands, timer expirations).
    /// </summary>
    long? CausedByEventId { get; }
}
```

Each event in the `TacticalEventStore` gets a monotonic `EventId` (already implicit in ring buffer position; make it explicit). Handlers that emit events in response to other events populate `CausedByEventId`.

**Example causal chain:**
```
[1] MoveUnitCommand dispatched (root cause)
  └─[2] UnitMoveStartedEvent (caused by 1)
      └─[3] ThreatDetectedEvent (caused by 2 — unit entered detection range)
          └─[4] ManeuverStartedEvent:SliceCorner (caused by 3 — threat triggered maneuver)
              └─[5] FormationCoherenceEvent(0.4) (caused by 4 — maneuver broke formation)
```

An agent receiving event [5] can walk the chain to understand: "Formation coherence dropped because a SliceCorner maneuver started because a threat was detected because a unit moved."

### 5.3 Outcome metrics

New event types emitted by tactical systems at meaningful boundaries:

| Event | Emitted by | Data |
|---|---|---|
| `FormationCoherenceEvent` | `FormationControlSystem` | `teamId`, `coherenceScore` (0-1), `driftFromIdeal` |
| `EngagementResolvedEvent` | Combat system (future) | `attackerTeamId`, `defenderTeamId`, `casualtyRatio`, `durationTicks` |
| `ManeuverOutcomeEvent` | `ManeuverExecutionSystem` | `maneuverType`, `outcome` (Success/Fail/Abort), `durationTicks` |
| `ThreatResponseEvent` | `ThreatDetectionSystem` | `entityId`, `threatType`, `responseLatencyTicks` |

### 5.4 Aggregation service

A `TelemetryAggregator` in the Agent assembly that accumulates outcome metrics across frames and scenarios:

- Per-formation: average coherence, coherence-under-fire, time-to-reform
- Per-behavior: success rate, average duration, failure-cause distribution
- Per-threat-type: detection rate, average response latency, false-positive rate
- Per-engagement: win rate by formation matchup, average casualty ratio

Aggregated data exposed via an MCP tool (`get_telemetry_summary`) and queryable through the ontology (telemetry results attached as computed properties on formation/behavior object types).

### 5.5 Integration with Layers 1 and 2

- **Layer 1 → Layer 3:** Ontology declares "WedgeFormation is offensive" → telemetry validates: "WedgeFormation's average engagement initiation rate is 78% (consistent with offensive role)" or flags: "WedgeFormation initiates only 30% of engagements (inconsistent with declared offensive role)"
- **Layer 2 → Layer 3:** Sandbox runs a scenario → telemetry captures causal chains for every outcome → agent can explain *why* BoxFormation won, not just *that* it won

---

## 6. What Lives Where

### 6.1 Valkyrie changes

| Assembly | Changes |
|---|---|
| `Valkyrie.TacticalAI.Core` | Causal event IDs on `ITacticalEvent` (Layer 3). New outcome event types. Deterministic RNG injection point for headless mode. **No Strategos dependency.** |
| `Valkyrie.TacticalAI.MonoGame.Agent` | `TacticalOntology` definition (Layer 1). Provider implementations (Layer 1). `ScenarioRunner` + `ScenarioDefinition` (Layer 2). `TelemetryAggregator` (Layer 3). New MCP tools for all three layers. Strategos.Ontology NuGet reference. |
| `Valkyrie.TacticalAI.Formations` | Design-intent attributes/metadata readable by the ontology definition (optional — ontology can hardcode this knowledge initially). **No Strategos dependency.** |
| `Valkyrie.TacticalAI.Behaviors` | Same as Formations — metadata for the ontology. **No Strategos dependency.** |

### 6.2 Strategos changes (parallel development)

| Package | Extension |
|---|---|
| `Strategos.Ontology` | Possibly: `QueryContext` for snapshot-scoped queries. Document non-persistent `IObjectSetWriter` semantics. |
| `Strategos.Ontology.InMemory` (NEW) | In-memory `ObjectSetExpression` evaluator — `Where`/`TraverseLink`/`OfInterface` against `IReadOnlyList<T>`. Useful for Valkyrie, test harnesses, and embedded scenarios. |
| `Strategos.Ontology.MCP` | No changes expected — tools are provider-agnostic; they query the ontology graph and dispatch through provider interfaces. |

### 6.3 Dependency direction (final)

```
Strategos.Ontology               Strategos.Ontology.InMemory (NEW)
    ▲                                ▲
    │                                │
    └────────────────┬───────────────┘
                     │
Valkyrie.TacticalAI.Core          (no external deps)
    ▲                                
    │                                
Valkyrie.TacticalAI.MonoGame.Agent (references Core + Strategos.Ontology + .InMemory)
```

**DIM-6 compliance:** Dependencies point inward. Core has no knowledge of ontology. Agent depends on Core and Strategos. Nothing depends on Agent. Release builds without Agent compile and run identically.

---

## 7. Phased Delivery

The three layers are independently shippable. Each builds on v1/v2 infrastructure and can proceed in parallel with Strategos extensions.

### Phase 1: Strategos.Ontology Integration (Layer 1)

**Depends on:** v1 MCP server shipped, `Strategos.Ontology.InMemory` package available
**Parallel with:** Strategos.InMemory development

- Define `TacticalOntology` covering the 5 formations, 4 maneuvers, 18 threat types
- Implement `GameLoopObjectSetProvider` backed by `IFrameStateProvider`
- Implement `CommandQueueActionDispatcher` backed by `CommandIntakeQueue`
- Implement `FrameStateEventStreamProvider` backed by `FrameState.RecentEvents`
- Wire ontology MCP tools alongside existing Valkyrie tools
- Test: `ontology_explore` returns correct formation/behavior/threat catalog
- Test: `ontology_query` traverses `countered_by` links correctly
- Test: `ontology_action` dispatches through command pipeline

### Phase 2: Programmable Sandbox (Layer 2)

**Depends on:** v2 headless mode (issue #35 item #2), deterministic RNG in Core
**Parallel with:** Phase 1

- Implement `ScenarioDefinition`, `ScenarioResult`, `ScenarioRunner`
- Implement `ParameterSweep` and `SweepResult`
- MCP tools: `create_scenario`, `run_scenario`, `sweep_parameters`, `compare_results`
- Ontology-driven scenario validation (precondition checks from Layer 1)
- Test: headless scenario runs to completion, deterministic across seeds
- Benchmark: scenario throughput (target: >10 scenarios/second for simple matchups)

### Phase 3: Semantic Telemetry (Layer 3)

**Depends on:** None beyond v1 (can start early)
**Parallel with:** Phases 1 and 2

- Add `EventId` and `CausedByEventId` to `ITacticalEvent`
- Add outcome event types (`FormationCoherenceEvent`, `ManeuverOutcomeEvent`, etc.)
- Implement `TelemetryAggregator` in Agent assembly
- MCP tool: `get_telemetry_summary`
- Wire aggregated telemetry as computed properties on ontology object types
- Test: causal chain from command → event → outcome is traceable
- Test: aggregation produces correct statistics over multi-frame runs

---

## 8. Success Criteria

When all three layers ship, the Bronze Rebellion development experience looks like this:

1. Developer adds a new formation (`DiamondFormation`) to Core.
2. Developer registers it in `TacticalOntology` with design-intent metadata: role, ideal size, strengths, weaknesses, counter-play relationships, constraints.
3. Agent discovers the new formation via `ontology_explore`. It notices that DiamondFormation has no `countered_by` links and flags this as a design gap.
4. Developer declares counter-play relationships. Agent auto-generates scenarios from the ontology graph and runs parameter sweeps in the sandbox.
5. Sweep results show DiamondFormation dominates StackFormation at all squad sizes. Agent reports this with telemetry: "DiamondFormation's spread neutralizes Stack's concentrated firepower; coherence stays above 0.8 while Stack drops below 0.4 by tick 200."
6. Developer adjusts DiamondFormation's spacing parameter. Agent reruns the sweep. Results converge to balanced.
7. All of this happened through MCP tool calls — no custom scripts, no manual test harnesses, no prompt engineering.

The engine is agent-first: not because it requires an agent, but because its abstractions are designed to be machine-readable, experimentally testable, and semantically rich enough for agents to reason about design quality.

---

## 9. Out of Scope

- **Player-facing agent features.** This is developer/designer tooling. The ontology and sandbox are dev-time capabilities behind the assembly boundary.
- **Strategos.Ontology internals.** This design specifies what Valkyrie *needs* from Strategos. The Strategos extensions themselves are designed and implemented in the Strategos repo.
- **Full combat system.** The sandbox and telemetry types reference engagement resolution and casualty ratios. These require a combat system that doesn't exist yet. The framework is designed to accommodate it; the combat system itself is separate work.
- **Persistent scenario storage.** Scenario definitions and results are in-memory for v1 of the sandbox. Persistence (save/load scenario suites, historical result comparison) is a future enhancement.

---

## 10. References

- v1 MCP Agent Server design: `docs/designs/2026-04-11-mcp-agent-server.md`
- v2 scope: [lvlup-sw/valkyrie#35](https://github.com/lvlup-sw/valkyrie/issues/35)
- Strategos.Ontology: `../strategos/src/Strategos.Ontology/`
- Strategos ontology design: `../strategos/docs/designs/2026-02-24-ontology-layer.md`
- ADR-001: Event Type Semantics: `docs/architecture/adr-001-event-type-semantics.md`
- ADR-002: Command Type Semantics: `docs/architecture/adr-002-command-type-semantics.md`
- Axiom backend quality dimensions: `/axiom:backend-quality` reference
