# Microsoft Agent Framework Workflows

> Comprehensive technical reference for Microsoft Agent Framework Workflows.
> Based on official [Microsoft Learn documentation](https://learn.microsoft.com/en-us/agent-framework/).

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Core Architecture](#core-architecture)
3. [Execution Model](#execution-model)
4. [State Management](#state-management)
5. [Control Flow Patterns](#control-flow-patterns)
6. [Multi-Agent Orchestration](#multi-agent-orchestration)
7. [External Integration & Human-in-the-Loop](#external-integration--human-in-the-loop)
8. [Durable Task Scheduler](#durable-task-scheduler)
9. [Observability](#observability)
10. [Deployment & Hosting](#deployment--hosting)
11. [Capability Comparison Matrix](#capability-comparison-matrix)
12. [References](#references)

---

## Executive Summary

### TL;DR

Microsoft Agent Framework Workflows is a **graph-based orchestration system** for building AI agent applications that need **deterministic execution, automatic state persistence, and fault tolerance**. It runs on Azure Functions with the Durable Task Scheduler providing managed durability infrastructure.

Think of it as: **LangGraph's graph model + Temporal's durability + Azure's serverless scaling**, purpose-built for multi-agent AI systems.

### What Problems Does This Solve?

| Problem | How MAF Workflows Addresses It |
|---------|-------------------------------|
| **Agent coordination complexity** | Built-in patterns for sequential, parallel, handoff, and group chat orchestration |
| **State loss on failures** | Automatic checkpointing at synchronization points; state survives crashes |
| **Long-running workflows** | Pause indefinitely for human input with zero compute cost during wait |
| **Debugging multi-agent flows** | OpenTelemetry tracing + visual dashboard showing agent interactions |
| **Scaling concerns** | Serverless hosting scales to thousands of instances or down to zero |
| **Race conditions** | BSP execution model guarantees deterministic, reproducible execution |

### Key Differentiators

| Aspect | MAF Workflows Approach |
|--------|----------------------|
| **Execution Model** | Bulk Synchronous Parallel (BSP) with supersteps—not event-driven |
| **Durability** | Managed Durable Task Scheduler (separate Azure resource) |
| **State** | Automatic checkpointing + agent thread persistence—no manual state management |
| **Human-in-Loop** | First-class Request/Response pattern with zero-cost waiting |
| **Observability** | Native OpenTelemetry + built-in dashboard (not bolt-on) |
| **Hosting** | Azure Functions with pay-per-invocation, scale-to-zero |

---

## Core Architecture

### In Plain English

A workflow is a **directed graph** where:
- **Nodes (Executors)** are processing units that receive messages, do work, and emit messages
- **Edges** are routing rules that determine which executor(s) receive each message
- **Messages** flow through the graph based on edge conditions

The framework handles message routing, parallel execution, state persistence, and failure recovery automatically.

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Executor A │───▶│  Executor B │───▶│  Executor C │
└─────────────┘    └─────────────┘    └─────────────┘
       │                                     ▲
       │          ┌─────────────┐            │
       └─────────▶│  Executor D │────────────┘
                  └─────────────┘
```

### Executors: The Processing Units

**What they are**: Self-contained units that process incoming messages and produce outputs.

**How they work**:
1. Executor receives a message (typed input)
2. Executor has access to `WorkflowContext` for:
   - Sending messages to other executors
   - Yielding outputs back to the caller
   - Emitting observability events
   - Accessing shared state
3. Executor produces output message(s)
4. Framework routes outputs via connected edges

| Executor Type | Description | When to Use |
|---------------|-------------|-------------|
| **Custom Executor** | Your code inheriting from `Executor<TInput, TOutput>` | Business logic, transformations, custom integrations |
| **Agent Executor** | Wraps an AI agent (LLM) | When you need LLM reasoning in the workflow |
| **Function Executor** | Created from a simple function | Stateless operations, quick transformations |

**Key capability**: Executors can handle **multiple input types** by configuring multiple handlers. The framework dispatches to the correct handler based on message type.

### Edges: The Routing Rules

**What they are**: Connections between executors that define how messages flow.

**How they work**:
1. When an executor produces output, the framework evaluates all outgoing edges
2. Each edge has a condition (or is unconditional)
3. Messages are delivered to all executors whose edge conditions match
4. Delivery happens in the next superstep (see Execution Model)

| Edge Type | Behavior | Use Case |
|-----------|----------|----------|
| **Direct** | Always routes to target | Sequential pipelines |
| **Conditional** | Routes if predicate returns true | Binary decisions (spam vs not-spam) |
| **Switch-Case** | First matching case wins, with default | Multi-way routing (priority levels) |
| **Fan-Out** | Sends to multiple targets | Parallel processing |
| **Fan-In** | Receives from multiple sources | Aggregation point |
| **Multi-Selection** | Dynamic target selection based on content | Variable parallelism |

> **Comparison Note**: This is similar to LangGraph's nodes and edges, but with a key difference: MAF uses BSP execution (synchronized rounds) rather than event-driven execution. This provides determinism guarantees but means parallel branches synchronize at each step.

---

## Execution Model

### In Plain English

Workflows don't execute continuously—they run in discrete **rounds called supersteps**. In each superstep:
1. All pending messages are collected
2. All target executors run **in parallel**
3. The framework **waits for ALL to complete** before the next superstep
4. State is checkpointed at the boundary

This is the **Bulk Synchronous Parallel (BSP)** model, borrowed from distributed graph processing systems like Google's Pregel.

### How Supersteps Work

```
                        SUPERSTEP N
    ┌────────────────────────────────────────────────────┐
    │                                                    │
    │  1. Collect all pending messages from superstep N-1│
    │                     ▼                              │
    │  2. Route messages to target executors via edges   │
    │                     ▼                              │
    │  3. Execute ALL target executors IN PARALLEL       │
    │     (no interference, isolated execution)          │
    │                     ▼                              │
    │  4. BARRIER: Wait for ALL executors to complete    │
    │                     ▼                              │
    │  5. Collect output messages, checkpoint state      │
    │                                                    │
    └────────────────────────────────────────────────────┘
                          │
                          ▼
                     SUPERSTEP N+1
```

### Why BSP? The Trade-offs

| Benefit | Explanation |
|---------|-------------|
| **Determinism** | Same input always produces same execution order—critical for debugging and replay |
| **Safe checkpointing** | State is consistent at superstep boundaries—no mid-operation snapshots |
| **No race conditions** | Executors in the same superstep can't interfere with each other |
| **Reliable recovery** | Resume from any checkpoint; replay produces identical results |

| Trade-off | Explanation |
|-----------|-------------|
| **Slowest executor blocks** | If you fan out to 3 paths and one takes 10x longer, the others wait |
| **Not real-time streaming** | Messages batch at superstep boundaries rather than flowing continuously |
| **Synchronization overhead** | Each superstep has barrier coordination cost |

### Practical Implication: Fan-Out Blocking

```
         ┌──▶ Executor B (fast) ──┐
         │                        │
Start ───┤                        ├──▶ Executor D
         │                        │
         └──▶ Executor C (slow) ──┘

Executor B finishes quickly but WAITS for Executor C
before Executor D can run in the next superstep.
```

**Mitigation**: If you need truly independent parallel paths, consolidate sequential chains into single executors to minimize superstep boundaries.

> **Comparison Note**: Event-driven systems (like traditional actor models) allow independent progress but require explicit synchronization and are harder to checkpoint consistently. BSP trades flexibility for strong guarantees.

---

## State Management

### In Plain English

State management in MAF Workflows has three layers:

1. **Checkpointing**: Workflow state (executor states + pending messages) automatically saved at superstep boundaries
2. **Agent Threads**: Conversation history for AI agents persisted across invocations
3. **State Isolation**: Factory functions ensure separate workflow instances don't share state

You don't manually save/load state—the framework handles it. Your job is to ensure your executors are compatible with this model.

### How Checkpointing Works

**What gets captured at each superstep boundary**:
- All executor internal states (via `OnCheckpointingAsync` hook)
- All pending messages queued for next superstep
- All pending external requests/responses
- Shared state across the workflow

**When checkpoints are created**:
- Automatically at end of each superstep (when CheckpointManager provided)
- Captures complete workflow state at a consistent point

**Storage options**:
```
CheckpointManager.Default          → In-memory (testing/development)
CheckpointManager.CreateJson(...)  → Custom storage backend (production)
```

**Recovery operations**:
- **Resume**: Continue from checkpoint on same workflow run
- **Rehydrate**: Create new workflow instance from checkpoint (for migration/failover)

### How Agent Threads Work

AI agents in workflows maintain **persistent conversation threads**:

1. Each agent gets a unique thread ID
2. Thread stores complete conversation history (messages, tool calls, context)
3. Thread persists in Durable Task Scheduler storage
4. Any compute instance can resume the conversation

**This means**: If your Azure Function instance dies mid-conversation, another instance picks up with full context intact.

### State Isolation: Why Factory Functions Matter

**The problem**: If you pass executor instances directly to WorkflowBuilder, all workflows share those instances.

```python
# BAD: Shared executor instance
executor = MyExecutor()
builder.add_edge(executor, other)  # All workflows share this executor's state!
```

**The solution**: Use factory functions that create fresh instances.

```python
# GOOD: Factory creates new instance per workflow
builder.add_edge(lambda: MyExecutor(), other)  # Each workflow gets isolated state
```

**Same applies to agents**: Use agent factory functions to ensure each workflow gets its own conversation thread.

> **Comparison Note**: Many workflow frameworks require you to implement your own state persistence. MAF provides built-in checkpointing with configurable storage backends. The trade-off is you must follow the factory function pattern for proper isolation.

---

## Control Flow Patterns

### In Plain English

Control flow in MAF Workflows is **declarative via edges**, not imperative via code. You define routing rules upfront; the framework evaluates them at runtime based on message content.

This is fundamentally different from: `if condition: call_agent_a() else: call_agent_b()`

Instead: `add_conditional_edge(source, target_a, lambda msg: msg.is_spam)`

### Pattern Catalog

| Pattern | How It Works | When to Use |
|---------|--------------|-------------|
| **Sequential** | Direct edges form a chain: A → B → C | Step-by-step processing pipelines |
| **Conditional (Binary)** | Two edges with opposing predicates | Yes/no decisions (approved vs rejected) |
| **Switch-Case** | Multiple edges with ordered conditions + default | Multi-category routing (priority levels, types) |
| **Fan-Out (Broadcast)** | One source, multiple targets, all receive message | Parallel analysis by multiple agents |
| **Fan-Out (Selective)** | Target selector function chooses which targets | Dynamic parallel processing |
| **Fan-In** | Multiple sources, one target | Aggregation, collecting parallel results |
| **Multi-Selection** | Dynamic number of targets based on content | Variable parallelism (process N items in parallel) |

### How Conditional Routing Works

Edge conditions are **predicates evaluated at routing time**:

```python
# Condition function receives the message
def is_high_priority(message):
    return message.priority == "high"

# Edge only routes if condition returns True
builder.add_conditional_edge(
    source=triage_executor,
    target=priority_handler,
    condition=is_high_priority
)
```

**Evaluation order for switch-case**:
1. Conditions evaluated in definition order
2. First matching condition wins
3. Default case catches unmatched messages
4. Always include a default to avoid dropped messages

### How Fan-Out/In Works

**Fan-Out (Broadcast)**:
```
                ┌──▶ Analyst A ──┐
                │                │
Input ──────────┼──▶ Analyst B ──┼──▶ Aggregator
                │                │
                └──▶ Analyst C ──┘

All analysts receive the SAME message in the SAME superstep.
All run in parallel; all must complete before Aggregator runs.
```

**Fan-In (Aggregation)**:
```
The Aggregator receives messages from A, B, C.
It can collect them all and produce a combined output.
```

**Selective Fan-Out**:
```python
def select_targets(message):
    # Return indices of targets to activate
    if message.length > 1000:
        return [0, 1, 2]  # Long: use all three
    else:
        return [0]  # Short: just first one

builder.add_fan_out_edge(source, [t1, t2, t3], selector=select_targets)
```

> **Comparison Note**: Declarative edges vs imperative branching is a design philosophy choice. Edges make the flow visible in the graph structure but require upfront planning. Imperative code is more flexible but harder to visualize and checkpoint.

---

## Multi-Agent Orchestration

### In Plain English

Multi-agent orchestration is about **coordinating multiple AI agents** to accomplish tasks no single agent could handle well. MAF Workflows provides five built-in patterns for common coordination needs.

The key insight: different patterns suit different problems. Sequential for pipelines, concurrent for parallel analysis, group chat for iterative refinement, handoff for dynamic routing, magentic for complex generalist tasks.

### Pattern Deep Dive

#### Sequential Pattern

**Topology**: Linear pipeline (A → B → C)

**How it works**:
1. Agent A processes input, produces output
2. Output becomes input to Agent B
3. Agent B processes, output goes to Agent C
4. Each agent sees the accumulated conversation history

**Data flow**: Each agent's output feeds into the next agent's input.

**Use cases**:
- Translation chains (English → French → Spanish → English)
- Refinement pipelines (Draft → Edit → Polish)
- Multi-stage analysis (Research → Analyze → Summarize)

```
User Input ──▶ Researcher ──▶ Writer ──▶ Editor ──▶ Final Output
                   │              │           │
              Researches     Writes based   Refines
              the topic      on research    the draft
```

---

#### Concurrent Pattern

**Topology**: Fan-out/Fan-in (parallel execution)

**How it works**:
1. Input broadcast to all agents simultaneously
2. All agents process independently (same superstep)
3. Results collected at fan-in aggregator
4. Aggregator produces combined output

**Data flow**: Same input to all; outputs merged.

**Use cases**:
- Parallel analysis (Technical + Market + Competitor research)
- Ensemble decisions (multiple agents vote/score)
- Diverse perspectives (different agent personalities analyze same problem)

```
              ┌──▶ Technical Analyst ──┐
              │                        │
User Query ───┼──▶ Market Analyst ─────┼──▶ Aggregator ──▶ Summary
              │                        │
              └──▶ Competitor Analyst ─┘

All three analyze the SAME query in PARALLEL.
Aggregator combines their insights.
```

---

#### Group Chat Pattern

**Topology**: Star with manager (hub-and-spoke)

**How it works**:
1. Manager agent controls the conversation
2. Manager selects which agent speaks next (speaker selection)
3. Selected agent contributes to shared conversation
4. All agents see the full conversation history
5. Manager decides when to terminate

**Data flow**: Shared conversation context; manager orchestrates turn-taking.

**Use cases**:
- Iterative refinement (agents critique and improve each other's work)
- Consensus building (agents debate until agreement)
- Collaborative problem-solving (specialists contribute expertise)

```
                    ┌─────────────┐
                    │   Manager   │ (selects next speaker)
                    └──────┬──────┘
                           │
         ┌─────────────────┼─────────────────┐
         ▼                 ▼                 ▼
    ┌─────────┐       ┌─────────┐       ┌─────────┐
    │ Agent A │◀─────▶│ Agent B │◀─────▶│ Agent C │
    └─────────┘       └─────────┘       └─────────┘
         │                 │                 │
         └────────────────────────────────────┘
                   (shared conversation)
```

**Manager can be**:
- Round-robin (simple turn-taking)
- AI-based (agent decides who should speak based on context)
- Custom logic (your speaker selection algorithm)

---

#### Handoff Pattern

**Topology**: Mesh (no central manager)

**How it works**:
1. Current agent processes and decides whether to handle or hand off
2. If hand off, agent specifies which agent should take over
3. Context transfers to new agent
4. No central coordinator—agents self-organize

**Data flow**: Context passes between agents dynamically; no fixed path.

**Use cases**:
- Expert routing (triage agent routes to specialists)
- Escalation (basic agent escalates to senior agent)
- Fallback handling (primary agent fails, fallback takes over)

```
User Query ──▶ Triage Agent ─┬──▶ Billing Specialist
                             │
                             ├──▶ Technical Support
                             │
                             └──▶ Account Manager

Triage decides which specialist based on query content.
Specialist can hand off to another if needed.
```

---

#### Magentic Pattern

**Topology**: Manager-based with planner (MagenticOne-inspired)

**How it works**:
1. Planner agent creates execution plan
2. Planner coordinates specialist agents
3. Specialists execute assigned tasks
4. Planner aggregates results and iterates if needed

**Data flow**: Planner decomposes task, delegates to specialists, synthesizes results.

**Use cases**:
- Complex generalist tasks (open-ended problems)
- Multi-step workflows with dynamic planning
- Tasks requiring diverse tool use

> **Comparison Note**: Most frameworks require you to implement these patterns yourself. MAF provides them as built-in primitives with the coordination logic handled by the framework.

---

## External Integration & Human-in-the-Loop

### In Plain English

Sometimes workflows need to **pause and wait for external input**—human approval, API responses, user decisions. MAF Workflows has a first-class Request/Response pattern for this:

1. Executor requests external input
2. Workflow pauses (state checkpointed)
3. External system processes request
4. Response submitted to workflow
5. Workflow resumes from checkpoint

**Key insight**: On serverless hosting, compute spins down during the wait. You pay nothing while waiting for a human to approve something.

### How Request/Response Works

**Step 1: Executor requests input**
```python
# Inside an executor
response = await ctx.request_info(
    request_data=ApprovalRequest(item="Purchase $5000 equipment"),
    response_type=bool
)
# Workflow pauses here until response arrives
```

**Step 2: Framework emits event**
```python
# External code monitoring workflow events sees:
RequestInfoEvent(
    request_id="abc123",
    data=ApprovalRequest(item="Purchase $5000 equipment")
)
```

**Step 3: Workflow status changes**
```
Status: IDLE_WITH_PENDING_REQUESTS
(Compute can spin down—zero cost waiting)
```

**Step 4: External system responds**
```python
# Could be hours or days later
await workflow.send_response(
    request_id="abc123",
    response=True  # Approved!
)
```

**Step 5: Workflow resumes**
```python
# The executor's request_info() call returns
response = True  # Continue processing with approval
```

### Zero-Cost Waiting: The Serverless Advantage

On Azure Functions Flex Consumption:

| Phase | Compute Cost |
|-------|--------------|
| Generate content (2 seconds) | Billed |
| Send approval request | Billed |
| Wait for human (24 hours) | **$0** |
| Process approval (1 second) | Billed |

Total: ~3 seconds of compute, not 24 hours.

### Timeout Handling

```python
try:
    approval = await ctx.request_info(
        request_data=request,
        response_type=bool,
        timeout=timedelta(hours=24)
    )
except TimeoutError:
    # Escalate to manager or auto-reject
    await ctx.send_message(EscalationRequest(original=request))
```

### Event Types for Integration

| Event | When Emitted | Use For |
|-------|--------------|---------|
| `RequestInfoEvent` | Executor requests external input | Triggering external handlers |
| `WorkflowStatusEvent` | Status changes (pending requests, idle) | Monitoring workflow state |
| `WorkflowOutputEvent` | Workflow produces final output | Capturing results |
| `AgentResponseUpdateEvent` | Agent streams response chunks | Real-time UI updates |

> **Comparison Note**: Many frameworks handle human-in-the-loop via polling or callback URLs. MAF's Request/Response is a first-class primitive with built-in timeout handling and zero-cost waiting on serverless. No external queuing infrastructure needed.

---

## Durable Task Scheduler

### In Plain English

The Durable Task Scheduler (DTS) is a **managed Azure service** that handles all the hard parts of durable execution:
- Storing workflow state
- Coordinating distributed execution
- Ensuring exactly-once processing
- Providing observability dashboard

You don't run it yourself—it's a separate Azure resource that your Azure Functions connect to.

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    Your Azure Functions App                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Agent A    │  │   Agent B    │  │  Workflow    │       │
│  │   Function   │  │   Function   │  │  Function    │       │
│  └──────────────┘  └──────────────┘  └──────────────┘       │
│           │                │                │                │
└───────────┼────────────────┼────────────────┼────────────────┘
            │                │                │
            ▼                ▼                ▼
┌──────────────────────────────────────────────────────────────┐
│              Durable Task Scheduler (Managed)                 │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  gRPC Connection (TLS + Identity Auth)                 │  │
│  ├────────────────────────────────────────────────────────┤  │
│  │  Work Item Queue │ State Store │ Dashboard │ Scheduler │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### How It Works

| Component | Function |
|-----------|----------|
| **gRPC Push Model** | Work items streamed to Functions (low latency vs polling) |
| **Hybrid Storage** | In-memory for hot state, persistent for recovery |
| **Task Hubs** | Logical containers for workflow instances (isolation) |
| **Scheduler** | Coordinates execution across multiple Function instances |

### How Fault Tolerance Works

**Scenario**: Your Azure Function crashes mid-execution.

1. **Last checkpoint preserved**: State was saved at previous superstep boundary
2. **Work item requeued**: DTS notices function didn't complete, requeues work
3. **Any instance resumes**: Another Function instance picks up the work item
4. **Replay from checkpoint**: Workflow resumes from last consistent state
5. **Execution continues**: No data loss, no duplicate processing

**This works because**:
- Checkpoints capture complete state at superstep boundaries
- DTS provides exactly-once delivery semantics
- Replay is deterministic (BSP model guarantees same execution order)

### Dashboard Capabilities

| Feature | What You Can See |
|---------|------------------|
| **Conversation History** | Full chat log for any agent session, at any point in time |
| **Orchestration Flow** | Visual graph showing agent interactions and branches |
| **Execution Timeline** | When each step ran, how long it took |
| **Tool Calls** | What tools agents invoked, with inputs/outputs |
| **Queue Status** | Pending work items, active orchestrations |
| **Performance Metrics** | Response times, token usage, duration |

### Limits

| Resource | Limit |
|----------|-------|
| Schedulers per region/subscription | 5 |
| Task hubs per scheduler (Dedicated) | 25 |
| Task hubs per scheduler (Consumption) | 5 |
| Payload size (inputs/outputs) | 1 MB |

> **Comparison Note**: DTS is a fully managed service—no Redis, no database, no queue infrastructure to operate. Compare to Temporal (self-hosted or cloud) or custom durability solutions. Trade-off: Azure lock-in for operational simplicity.

---

## Observability

### In Plain English

Observability in MAF Workflows is **built-in, not bolted-on**:
- **Tracing**: OpenTelemetry spans for every operation
- **Metrics**: Counts, durations, status codes
- **Dashboard**: Visual UI for debugging and monitoring

You enable it once; the framework instruments everything automatically.

### How Tracing Works

Every workflow operation emits OpenTelemetry spans:

```
workflow.run (parent span)
├── message.send (Executor A → Executor B)
│   └── executor.process (Executor B)
│       └── message.send (Executor B → Executor C)
│           └── executor.process (Executor C)
└── edge_group.process (routing evaluation)
```

### Span Types

| Span | When Created | Contains |
|------|--------------|----------|
| `workflow.build` | Workflow compiled from builder | Build configuration |
| `workflow.run` | Workflow execution started | Input, status, duration |
| `message.send` | Message sent between executors | Source, target, message type |
| `executor.process` | Executor processing message | Executor ID, processing time |
| `edge_group.process` | Edge group evaluated | Routing decisions |

### Span Relationships

Spans are **linked**, not nested, when execution crosses superstep boundaries:

```
Superstep N:
  executor.process (A) ──creates──▶ message.send (A→B)

Superstep N+1:
  executor.process (B) ◀──linked to── message.send (A→B)
```

This creates a **traceable path** through the workflow even across superstep barriers.

### Metrics Available

| Metric | Aggregations |
|--------|--------------|
| Executor processing time | Average, P50, P95, P99 |
| Message routing time | Average, count |
| Workflow completion time | Average, count by status |
| Superstep count | Count per workflow |
| Error rate | Count by error type |

**Grouping dimensions**: Workflow name, Executor ID, Executor type, Status code

### Debugging Workflow: Finding Issues

1. **Start with Dashboard**: See visual flow, identify where things went wrong
2. **Check Conversation History**: For agent issues, see full chat log
3. **Examine Tool Calls**: See what agents tried to do, what failed
4. **Trace Spans**: Follow execution path across executors
5. **Check Metrics**: Look for latency spikes, error rate changes

> **Comparison Note**: Native observability vs third-party integration. MAF emits standard OpenTelemetry, so you can use any compatible backend (Jaeger, Zipkin, Azure Monitor). The DTS dashboard is purpose-built for agent workflows—more useful than generic tracing UIs for this use case.

---

## Deployment & Hosting

### In Plain English

MAF Workflows runs on **Azure Functions** with the **Durable Task Scheduler** managing state. You write your workflow code, deploy to Azure Functions, connect to DTS, and the platform handles scaling, reliability, and infrastructure.

### Hosting Options

| Plan | Scaling | Billing | Best For |
|------|---------|---------|----------|
| **Flex Consumption** | 0 to thousands, instant | Per-invocation | Production (recommended) |
| **Consumption** | 0 to ~200, slower cold start | Per-invocation | Development, low-traffic |
| **Dedicated** | Manual or auto-scale rules | Reserved capacity | Predictable high-traffic |

### Cost Model

**Flex Consumption (recommended)**:
- Pay only for actual execution time (per 100ms)
- Scale to zero when idle—$0 when nothing running
- Human-in-the-loop workflows cost almost nothing during wait periods
- Scales instantly for burst traffic

### Local Development

**Required emulators**:

| Service | Purpose | Docker Command |
|---------|---------|----------------|
| **Azurite** | Azure Storage emulation | `docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite` |
| **DTS Emulator** | Durable Task Scheduler | `docker run -p 8080:8080 -p 8082:8082 mcr.microsoft.com/dts/dts-emulator:latest` |

**Dashboard access**: http://localhost:8082 (DTS Emulator dashboard)

### Packages

**C# (.NET 9+)**:
```bash
dotnet add package Microsoft.Agents.AI.Workflows --prerelease
dotnet add package Microsoft.Agents.AI.Hosting.AzureFunctions --prerelease
# Requires Microsoft.Azure.Functions.Worker v2.2.0+
```

**Python**:
```bash
pip install agent-framework --pre
pip install agent-framework-azurefunctions --pre
```

### Deployment

```bash
# Using Azure Developer CLI
azd init --template durable-agents-quickstart-dotnet
azd deploy
```

---

## Capability Comparison Matrix

### Out-of-Box Capabilities

This matrix helps compare MAF Workflows with other workflow/orchestration frameworks.

| Capability | MAF Workflows | Notes |
|------------|---------------|-------|
| **State Persistence** | Built-in | Automatic checkpointing at supersteps; DTS-managed storage |
| **Retries/Fault Tolerance** | Built-in | Deterministic replay; any-instance resume |
| **Schedules/Timers** | Built-in | Durable timers; Azure Functions timer triggers |
| **Queues** | Built-in | DTS work items; Azure Functions queue triggers |
| **Observability** | Built-in | OpenTelemetry + DTS dashboard |
| **Human-in-the-Loop** | Built-in (First-class) | Request/Response pattern; zero-cost waiting |
| **Secrets** | External | Use Azure Key Vault or app settings |
| **Multi-Agent Patterns** | Built-in (5 patterns) | Sequential, Concurrent, Group Chat, Handoff, Magentic |
| **Streaming Responses** | Built-in | AgentResponseUpdateEvent for real-time |
| **Type Safety** | Built-in | Compile + runtime validation |

### Comparison Dimensions

When evaluating against other frameworks, consider:

| Dimension | MAF Workflows Approach | Alternative Approaches |
|-----------|----------------------|----------------------|
| **Execution Model** | BSP (synchronized rounds) | Event-driven, async/await chains |
| **Durability Backend** | Managed DTS (Azure) | Self-hosted (Temporal), DB-backed, Redis |
| **Graph Definition** | Declarative edges | Imperative code, YAML/JSON |
| **Agent Integration** | First-class AI agent support | Generic task workers |
| **Hosting** | Azure Functions (serverless) | Kubernetes, VMs, containers |
| **Observability** | Native + Dashboard | Third-party, DIY |
| **Human-in-Loop** | Built-in pattern | Callbacks, polling, external queues |

### What's NOT Included (External/DIY)

| Capability | Status | Recommendation |
|------------|--------|----------------|
| Secrets management | External | Azure Key Vault |
| Custom storage backends | DIY | Implement CheckpointManager interface |
| Non-Azure hosting | Not supported | Azure Functions required |
| Self-hosted DTS | Not available | DTS is managed-only |

---

## References

### Official Documentation

| Topic | URL |
|-------|-----|
| Workflows Overview | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/overview |
| Core Concepts | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/core-concepts/overview |
| Executors | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/core-concepts/executors |
| Edges | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/core-concepts/edges |
| Execution Model | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/core-concepts/workflows |
| Checkpoints | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/checkpoints |
| State Isolation | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/state-isolation |
| Observability | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/observability |
| Multi-Agent Patterns | https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/multi-agent-patterns |
| Durable Agents | https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-types/durable-agent/create-durable-agent |
| Durable Agent Features | https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-types/durable-agent/features |
| Durable Task Scheduler | https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler |
| Create Durable Agent Tutorial | https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/create-and-run-durable-agent |

### GitHub Samples

| Language | URL |
|----------|-----|
| C# Samples | https://github.com/microsoft/agent-framework/tree/main/dotnet/samples |
| Python Samples | https://github.com/microsoft/agent-framework/tree/main/python/samples |

---

*Generated from official Microsoft Learn documentation. Last updated: January 2026.*
