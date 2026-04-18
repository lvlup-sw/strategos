# Strategic Framing: Exarchos × Basileus × Strategos

**Date:** 2026-04-18
**Status:** Accepted
**Scope:** Product boundaries, contract ownership, evolutionary direction

## Three-Product Model

| Product | Type | Role |
|---------|------|------|
| **Strategos** | OSS NuGet library | Fluent DSL + Roslyn source generators for durable workflows. Owns the shared SDLC event contracts via a `Strategos.Contracts` companion package. |
| **Exarchos** | OSS CLI + plugin | Local SDLC workflow harness. State tracking, schema enforcement, convergence gates, context shedding/hydration. Makes any coding agent more effective. |
| **Basileus** | Commercial SaaS | Agent host platform. Phronesis execution, sandboxed compute (Firecracker VMs), ontological data fabric, durable orchestration (Wolverine sagas). |

## Core Principle: Convergence, Not Overlap

Exarchos is the open-source entry point and natural funnel to Basileus. Features must converge — never overlap. No feature in Exarchos should be an inferior local replica of something Basileus does better. Instead, each product operates at a different scope on the same contracts.

### Exarchos Owns Process. Basileus Owns Execution.

- **Exarchos** = workflow orchestration. State machines, phase transitions, convergence gates, dispatch decisions, developer session management. It tells agents *what to do next* and *whether what they did was good enough*.
- **Basileus** = agent execution. Phronesis loops, tool use, reflective reasoning, semantic scoring, sandboxed compute. It *does the agent-shaped work*.

Agent-shaped work (LLM reasoning, tool use, reflective loops) belongs in the agent host. This boundary was established in basileus#146: review triage with semantic scoring moved from Exarchos to Basileus as a Phronesis Code Review agent.

### Exarchos Has Agents — The Boundary Is Subtler

Exarchos delegates to the *host runtime's* agents (Claude Code subagents, Codex agents, Cursor agents). It doesn't own agent execution — it harnesses whatever coding agent the developer's environment provides. The workflow discipline, convergence gates, and dispatch logic make those agents more effective.

Basileus provides its *own* agent execution infrastructure: Phronesis loop, Firecracker VMs, budget algebra, loop detection, reflective reasoning.

The distinction is not "orchestration vs execution" — it's **"developer-attended, host-mediated vs autonomous, platform-native."**

## Same Contract, Different Scope

Autonomous features (#1119 merge orchestrator, #1120 self-healing shepherd, #1121 TDD swarm) are legitimate Exarchos features. They make delegation to host agents smarter. The scoping rule:

| Concern | Exarchos (Local) | Basileus (Platform) |
|---------|-----------------|---------------------|
| **Merge orchestrator** | Topology validation, phase gating, dispatch to host subagents | Semantic conflict resolution, overnight autonomous runs, durable execution |
| **Self-healing shepherd** | PR lifecycle state machine, event routing, single-PR monitoring | Classify-and-fix loop, parallel fix agents, multi-PR overnight |
| **TDD swarm** | Dispatch + judge selection across host subagents | Competitive execution across Firecracker VMs, durable budgets |

Each feature works locally with best-effort guarantees. On Basileus, the same workflow contracts get durable execution, sandboxed compute, and real coordination.

The funnel moment: *"My local TDD swarm works, but I want it running on 5 machines overnight without babysitting a terminal."*

## Contract Ownership: Strategos.Contracts

The shared event schema is the load-bearing contract between both products. It lives in a companion package alongside Strategos — not in either product.

### Architecture

```
Strategos.Contracts (NuGet + JSON Schema artifacts)
    ├── TypeSpec source (canonical)
    ├── JSON Schema (emitted, language-neutral)
    ├── C# records (emitted, consumed by Basileus)
    └── (Exarchos derives Zod types from JSON Schema via CI)
```

### Dependency Direction

```
Strategos.Contracts  ←── Strategos (core DSL)
        ↑                      ↑
        │                      │
    Exarchos              Basileus
  (derives Zod)        (depends on NuGet)
```

Neither product depends on the other. Both depend on the shared contracts. Exarchos has no NuGet dependency — it consumes JSON Schema files and generates Zod types.

### Schema Sync Pipeline

TypeSpec is the canonical schema source. The build pipeline emits:
- JSON Schema files (language-neutral intermediate)
- C# records via NJsonSchema or TypeSpec C# emitter (for Strategos.Contracts NuGet)
- Zod types via `json-schema-to-zod` (for Exarchos, consumed at build time)

CI validates both derivations match the canonical schema on every commit.

### Event Envelope

Both products use the same event envelope:

```
SdlcEventEnvelope {
  streamId, sequence, timestamp, type,
  correlationId?, causationId?,
  agentId?, agentRole?, source?,
  schemaVersion?, data?
}
```

The `source` field (`"exarchos"` | `"basileus"`) distinguishes provenance. The `type` field discriminates the `data` payload. Unknown event types are logged but never rejected (forward compatibility).

## Graceful Degradation

Exarchos must be fully functional without Basileus. When Basileus is connected:
- Event streams sync bidirectionally via the Streaming Sync Engine
- Platform-scale observability replaces local projections
- Agent execution can be offloaded to sandboxed VMs

When Basileus is disconnected:
- All workflow orchestration works locally
- Quality gates run against host-runtime subagents
- Deterministic triage routers serve as fallbacks for agent-shaped review work
- `export` bundles local state for later Basileus consumption

## Milestone Alignment

| Version | Focus | Relationship to Basileus |
|---------|-------|--------------------------|
| v2.8.0 | Event-store correctness, discovery workflow | Foundation for contract convergence |
| v2.8.5 | Pruner fix, architectural principles docs | Codifies the boundary |
| v2.9.0 | Lifecycle verbs (ps, describe, wait, export) | Local implementations of contracts Basileus implements at platform scale |
| v3.0.0 | CLI infrastructure, HATEOAS + NDJSON, autonomous features | Defines the shared output protocol; autonomous features scoped to local tier |

## Related

- basileus#146 — Architectural decision: agent-shaped work belongs in Basileus
- basileus#147 — Phronesis Code Review agent (first concrete boundary enforcement)
- basileus#120 — Remote Event Types & Schema Mapping
- basileus#119 — Streaming Sync Engine
- basileus#127 — Commercial positioning documentation
- basileus#129 — Exarchos onboarding guide with harness positioning
- exarchos#1109 — Cross-cutting: event-sourcing integrity + MCP parity + basileus-forward
- Spike: `spikes/typespec-contracts/` — TypeSpec → JSON Schema → Zod proof-of-concept
