---
title: "MAF Workflows vs Strategos: Framework Comparison"
---

# MAF Workflows vs Strategos: Framework Comparison

> A detailed comparison of Microsoft Agent Framework Workflows and Strategos for production AI agent orchestration.

---

## Executive Summary

Microsoft Agent Framework (MAF) Workflows and Strategos both solve the fundamental challenge of building reliable AI agent systems, but they take fundamentally different approaches. MAF Workflows provides a **managed, Azure-native solution** with built-in multi-agent patterns and serverless hosting, while Strategos offers a **cloud-agnostic, event-sourced architecture** with intelligent agent selection and complete audit trails.

MAF Workflows excels in Azure-native environments where serverless scale-to-zero economics matter, and where pre-built multi-agent patterns (Group Chat, Handoff, Magentic) accelerate development. Its Bulk Synchronous Parallel (BSP) execution model provides deterministic, reproducible execution with automatic checkpointing at superstep boundaries.

Strategos differentiates through its event-sourcing foundation, providing full audit trails that answer "what did the agent see?" at any point in history. Its Thompson Sampling agent selection learns optimal agent-to-task routing over time. The fluent DSL with compile-time source generation catches workflow errors before runtime, and the Wolverine/Marten infrastructure provides battle-tested durability without Azure lock-in.

### Key Differentiators

| Aspect | MAF Workflows | Strategos |
|--------|---------------|------------------|
| **Execution Model** | BSP supersteps (synchronized rounds) | Message-driven saga orchestration |
| **State Persistence** | Checkpoint snapshots at superstep boundaries | Full event sourcing (every decision recorded) |
| **Agent Selection** | Manual or declarative edge routing | Thompson Sampling (learns optimal routing) |
| **Hosting** | Azure Functions + Durable Task Scheduler | Any .NET host (Wolverine/Marten on PostgreSQL) |
| **Multi-Agent Patterns** | 5 built-in (Group Chat, Handoff, etc.) | DSL-composable (Fork/Join, Branch, Loop) |
| **Compile-Time Safety** | Runtime validation | Source-generated state machines with diagnostics |
| **Human-in-Loop Cost** | $0 during wait (serverless) | Minimal (saga paused, no active resources) |
| **Observability** | DTS Dashboard + OpenTelemetry | Marten projections + OpenTelemetry |

### Quick Decision Guide

**Choose MAF Workflows if:**
- You're already invested in Azure
- Need managed infrastructure with minimal ops
- Want pre-built multi-agent orchestration patterns
- Human approvals may take hours/days (serverless economics)

**Choose Strategos if:**
- You need cloud portability or on-premises deployment
- Complete audit trails are regulatory requirements
- You want the system to learn optimal agent routing
- Compile-time workflow validation is valuable
- You have complex compensation/rollback requirements

---

## Capability Comparison Matrix

### 1. Execution Model

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Model** | Bulk Synchronous Parallel (BSP) | Message-driven Saga |
| **Parallelism** | Synchronized supersteps (barrier at each round) | True async (steps run independently) |
| **Determinism** | Guaranteed via superstep synchronization | Event replay produces identical state |
| **Blocking Behavior** | Slowest parallel branch blocks all | Branches complete independently |
| **Trade-off** | Simple reasoning, potential latency | Complex reasoning, optimal latency |

**MAF BSP Model:**
```
Superstep N: All executors run → BARRIER → Checkpoint → Superstep N+1
```
Every parallel path must complete before any can proceed to the next step.

**Strategos Saga Model:**
```
Step A completes → Cascade message → Step B starts immediately
Fork paths run independently, Join aggregates when all complete
```
No artificial synchronization barriers; natural async flow.

### 2. State Management

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Persistence Model** | Checkpoint snapshots | Event sourcing |
| **What's Captured** | Current state at superstep boundary | Every state transition as event |
| **Recovery** | Resume from last checkpoint | Replay events to any point |
| **Audit Question** | "Where is it now?" | "How did it get there?" |
| **Storage** | Durable Task Scheduler (managed) | PostgreSQL via Marten |
| **Time-Travel** | Limited (checkpoint versions) | Full (any event, any timestamp) |

**Event Sourcing Advantage:**

Strategos captures:
- `WorkflowStarted` with initial state
- `PhaseChanged` for every transition
- `StepCompleted` with inputs/outputs
- `ContextAssembled` (what agent saw)
- `AgentDecisionEvent` with confidence, model version, tokens
- `ApprovalRequested`/`ApprovalReceived`
- `CompensationExecuted`

MAF captures checkpoint snapshots—sufficient for recovery but not for understanding the decision journey.

### 3. Durability & Fault Tolerance

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Backend** | Durable Task Scheduler (Azure managed) | Wolverine + PostgreSQL |
| **Exactly-Once** | Via DTS work item semantics | Via transactional outbox |
| **Retry Policy** | Configurable in DTS | Wolverine retry policies |
| **Resume Capability** | Any Azure Function instance | Any application instance |
| **Operational Burden** | Managed (Azure handles it) | Self-managed (PostgreSQL required) |
| **Lock-In** | Azure-specific | Cloud-agnostic |

Both provide strong durability guarantees. The difference is operational: MAF is managed, Strategos is portable.

### 4. Workflow Definition

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Definition Style** | Declarative graph (nodes/edges) | Fluent DSL (prose-like) |
| **Vocabulary** | Executors, Edges, Conditions | Steps, Branch, Fork/Join, Loop |
| **Compile-Time Validation** | None (runtime errors) | 8 diagnostics via source generator |
| **Generated Artifacts** | None | Phase enum, commands, events, saga, transitions |
| **Reusability** | Executor classes | Step classes with DI |

**Strategos Compile-Time Diagnostics:**

| Code | Description |
|------|-------------|
| AGWF001 | Empty workflow name |
| AGWF002 | No steps found |
| AGWF003 | Duplicate step name (use instance names) |
| AGWF009 | Missing StartWith |
| AGWF010 | Missing Finally |
| AGWF012 | Fork without Join |
| AGWF014 | Loop without body |

**DSL Comparison:**

```csharp
// MAF Workflows (graph-based)
builder.AddNode<TriageExecutor>("triage");
builder.AddConditionalEdge("triage", "billing", msg => msg.Type == "billing");
builder.AddConditionalEdge("triage", "support", msg => msg.Type == "support");

// Strategos (fluent DSL)
Workflow<ClaimState>.Create("process-claim")
    .StartWith<Triage>()
    .Branch(state => state.ClaimType,
        when: ClaimType.Billing, then: flow => flow.Then<BillingProcess>(),
        when: ClaimType.Support, then: flow => flow.Then<SupportProcess>())
    .Finally<Notify>();
```

### 5. Multi-Agent Orchestration

| Pattern | MAF Workflows | Strategos |
|---------|---------------|------------------|
| **Sequential** | Direct edges (A → B → C) | `.Then<A>().Then<B>().Then<C>()` |
| **Parallel** | Fan-out/Fan-in executors | `.Fork(...).Join<Aggregator>()` |
| **Conditional** | Conditional edges with predicates | `.Branch(selector, when:...)` |
| **Group Chat** | Built-in pattern with manager | Build with Loop + dynamic dispatch |
| **Handoff** | Built-in pattern (mesh routing) | Build with Branch + state routing |
| **Magentic (Planner)** | Built-in pattern | Build with Loop + planning step |
| **Iteration** | Loop via edge back to previous | `.RepeatUntil(condition, maxIterations, body)` |

**MAF's Built-in Patterns:**
- Group Chat: Manager selects next speaker from specialist pool
- Handoff: Agents self-organize via dynamic routing
- Magentic: Planner decomposes and delegates to specialists

**Strategos's Composable Approach:**
The DSL primitives (Branch, Fork/Join, RepeatUntil) compose to build any pattern. No special constructs needed—patterns emerge from composition.

### 6. Intelligent Agent Selection

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Selection Method** | Manual (Group Chat manager) or declarative edges | Thompson Sampling (multi-armed bandit) |
| **Learning** | None | Online learning from outcomes |
| **Personalization** | None | Per-category beliefs |
| **Exploration/Exploitation** | N/A | Automatic (Beta distribution sampling) |

**Thompson Sampling in Strategos:**

```csharp
// Configure agent selection
services.AddAgentSelection(options => options
    .WithPrior(alpha: 2, beta: 2)  // Uninformative prior
    .WithCategories(TaskCategory.Analysis, TaskCategory.Coding));

// Select agent for task
var selection = await selector.SelectAgentAsync(new AgentSelectionContext
{
    AvailableAgentIds = ["analyst", "coder", "researcher"],
    TaskDescription = "Analyze the sales data trends"
});

// Record outcome for learning
await selector.RecordOutcomeAsync(
    selection.SelectedAgentId,
    selection.TaskCategory,
    AgentOutcome.Succeeded(confidenceScore: 0.85));
```

The system learns which agents perform best for which task categories over time—no manual tuning required.

### 7. Human-in-the-Loop

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Pattern** | Request/Response (first-class) | AwaitApproval DSL |
| **Wait Cost** | $0 (serverless scale-to-zero) | Minimal (saga persisted, no compute) |
| **Timeout Handling** | Built-in with configurable escalation | DSL-declared with OnTimeout path |
| **Event Notification** | RequestInfoEvent emitted | ApprovalRequested event stored |
| **Response Submission** | workflow.send_response(request_id, response) | Wolverine message to saga |

**MAF Request/Response:**
```python
response = await ctx.request_info(
    request_data=ApprovalRequest(item="Purchase $5000"),
    response_type=bool,
    timeout=timedelta(hours=24)
)
```

**Strategos AwaitApproval:**
```csharp
.AwaitApproval<LegalTeam>(options => options
    .WithTimeout(TimeSpan.FromDays(2))
    .OnTimeout(flow => flow.Then<EscalateToManager>())
    .OnRejection(flow => flow.Then<HandleRejection>()))
```

Both handle long-running human workflows well. MAF's serverless model provides true $0 wait cost.

### 8. Observability

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Tracing** | Native OpenTelemetry spans | OpenTelemetry via Wolverine |
| **Dashboard** | DTS Dashboard (visual flow, history) | Custom (Marten projections) |
| **Conversation History** | Full chat log in dashboard | Event-sourced ChatMessageRecorded |
| **Tool Call Visibility** | Inputs/outputs in dashboard | Events + projection |
| **Metrics** | Built-in (processing time, counts) | Custom projection-based |

**DTS Dashboard Capabilities:**
- Visual graph showing agent interactions
- Full conversation history for any session
- Tool call inputs/outputs
- Execution timeline
- Queue status and performance metrics

This is a significant MAF advantage—purpose-built debugging UI versus DIY projections.

### 9. Resource Management

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Budget Enforcement** | Not built-in | IBudgetGuard abstraction |
| **Token Tracking** | Manual via agent implementation | AgentDecisionEvent captures tokens |
| **Loop Detection** | Not built-in | ILoopDetector with configurable thresholds |
| **Cost Attribution** | Manual | Per-workflow event trail |

**Strategos Budget Enforcement:**
```csharp
public interface IBudgetGuard
{
    Task<bool> CanProceedAsync(string workflowId, ResourceBudget budget);
    Task RecordUsageAsync(string workflowId, ResourceUsage usage);
}
```

Prevents runaway agent costs—critical for production deployments.

### 10. Error Handling & Recovery

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Retry Policy** | Configurable in DTS | Wolverine retry policies |
| **Compensation** | Manual (reverse event application) | `.Compensate<T>()` DSL with automatic ordering |
| **Step-Level Handlers** | Not built-in | `.OnFailure(f => f.When<Exception>().Then<Handler>())` |
| **Workflow-Level Fallback** | Via error edges | `.OnFailure(flow => flow.Then<Fallback>())` |

**Strategos Compensation:**
```csharp
.Then<ChargePayment>()
    .Compensate<RefundPayment>()
.Then<ReserveInventory>()
    .Compensate<ReleaseInventory>()
```
On failure, compensations run in reverse order automatically.

### 11. Deployment & Hosting

| Dimension | MAF Workflows | Strategos |
|-----------|---------------|------------------|
| **Hosting Options** | Azure Functions only | Any .NET host |
| **Scaling** | Serverless (0 to thousands) | Application-dependent |
| **Local Development** | Azurite + DTS Emulator (Docker) | PostgreSQL + app |
| **Cloud Portability** | Azure only | Any cloud or on-premises |
| **Operational Model** | Managed infrastructure | Self-managed |

---

## Gap Analysis

### MAF Features to Consider for Strategos

| Feature | MAF Capability | Gap Severity | Recommendation |
|---------|---------------|--------------|----------------|
| **Group Chat Pattern** | Built-in manager + specialist topology | Medium | Add as orchestration template in docs |
| **Handoff Pattern** | Dynamic agent-to-agent routing | Medium | Add as orchestration template in docs |
| **Magentic Pattern** | Planner-driven task decomposition | Low | Can be built with current DSL (Loop + Branch) |
| **Visual Dashboard** | DTS dashboard for debugging | High | Consider Marten Admin UI or custom projection viewer |
| **Request/Response Primitive** | First-class external wait | Low | AwaitApproval covers most cases |
| **Serverless Economics** | $0 wait cost | Medium | Document cost comparison; saga pause is low-cost |

### Strategos Advantages to Preserve

| Feature | Why It Matters |
|---------|---------------|
| **Thompson Sampling** | Unique—MAF has no intelligent selection. Enables learning optimal agent routing. |
| **Event Sourcing** | Full audit trail vs snapshot checkpoints. Critical for compliance and debugging. |
| **Compile-Time Validation** | 8 diagnostics catch errors before runtime. Faster development cycle. |
| **Loop Detection/Recovery** | Production-critical for preventing stuck agent loops. |
| **Budget Governance** | Resource control prevents runaway costs. Essential for enterprise. |
| **Cloud-Agnostic** | Freedom from Azure lock-in. Deploy anywhere with PostgreSQL. |
| **Confidence Routing** | Route low-confidence decisions to humans automatically. |
| **Fluent DSL** | Reads like prose. Lower cognitive load than graph APIs. |
| **Compensation Handlers** | Automatic reverse-order execution for rollback. |

---

## Targeted Use Case Guide

### Choose MAF Workflows When:

1. **Azure-Native Environment**
   - Already using Azure Functions, Cosmos DB, Azure AI
   - Azure identity and networking established
   - Team familiar with Azure deployment patterns

2. **Serverless Economics Matter**
   - Workflows with long human wait times (hours/days)
   - Highly variable load (burst traffic)
   - Cost optimization is primary concern

3. **Pre-Built Patterns Accelerate Delivery**
   - Group Chat for iterative agent refinement
   - Handoff for customer service routing
   - Need patterns working quickly without custom implementation

4. **Managed Infrastructure Preferred**
   - Limited DevOps capacity
   - Don't want to manage PostgreSQL
   - Prefer Azure's operational model

5. **Visual Debugging is Critical**
   - Non-technical stakeholders need visibility
   - DTS dashboard provides immediate value
   - Conversation history browsing required

### Choose Strategos When:

1. **Cloud Portability Required**
   - Multi-cloud strategy
   - On-premises deployment requirements
   - Avoiding vendor lock-in

2. **Complete Audit Trails**
   - Regulatory compliance (HIPAA, SOX, GDPR)
   - Need to answer "what did the agent see?"
   - Time-travel debugging for AI decisions
   - Legal discovery requirements

3. **Intelligent Agent Selection**
   - Multiple agents with overlapping capabilities
   - Want system to learn optimal routing
   - Exploration/exploitation balance matters

4. **Compile-Time Safety**
   - Large workflow definitions
   - Team values catching errors early
   - Frequent workflow refactoring

5. **Complex Error Recovery**
   - Multi-step transactions requiring rollback
   - Step-specific failure handling
   - Compensation logic with ordering guarantees

6. **Budget Governance**
   - Cost control for LLM usage
   - Per-workflow resource limits
   - Preventing runaway agent loops

### Consider Both When:

1. **Hybrid Scenarios**
   - Azure for some workflows (human-heavy, visual debugging)
   - Strategos for others (audit-critical, learning-based)

2. **Migration Path**
   - Start with Strategos for control
   - Migrate specific workflows to MAF when Azure advantages apply

3. **Pattern Inspiration**
   - MAF patterns can inform Strategos orchestration templates
   - Both ecosystems evolving—cross-pollination valuable

---

## Summary Matrix

| Capability | MAF Workflows | Strategos | Notes |
|------------|:-------------:|:----------------:|-------|
| Durability | ✅ | ✅ | Both provide strong guarantees |
| Event Sourcing | ❌ Snapshots | ✅ Full | Key differentiator |
| Intelligent Selection | ❌ | ✅ Thompson | Unique to Strategos |
| Visual Dashboard | ✅ DTS | ⚠️ DIY | MAF advantage |
| Compile-Time Validation | ❌ | ✅ 8 diagnostics | Strategos advantage |
| Built-in Multi-Agent Patterns | ✅ 5 patterns | ⚠️ Composable | MAF faster start |
| Cloud Portability | ❌ Azure only | ✅ Any | Strategos advantage |
| Serverless Economics | ✅ $0 wait | ⚠️ Low cost | MAF advantage |
| Compensation Handlers | ⚠️ Manual | ✅ DSL | Strategos advantage |
| Budget Governance | ❌ | ✅ | Unique to Strategos |
| Loop Detection | ❌ | ✅ | Unique to Strategos |
| Human-in-Loop | ✅ First-class | ✅ DSL | Both strong |
| OpenTelemetry | ✅ Native | ✅ Via Wolverine | Both strong |

---

## References

### MAF Workflows
- [Workflows Overview](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/overview)
- [Core Concepts](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/core-concepts/overview)
- [Multi-Agent Patterns](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/multi-agent-patterns)
- [Durable Task Scheduler](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler)

### Strategos
- [Design Document](./design.md)
- [Library Roadmap](./workflow-library-roadmap-v2.md)
- [Exploration-Exploitation Theory](./theory/exploration-exploitation.md)

---

*Comparison based on MAF Workflows documentation (January 2026) and Strategos v2.0 design.*
