# Strategos

**A Design Document for Deterministic Agentic Workflow Orchestration**

Version 2.0 | Status: Implemented (MVP)

Last Updated: 2025-12-26

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Implementation Status](#implementation-status)
3. [Design Philosophy](#design-philosophy)
4. [Conceptual Model](#conceptual-model)
5. [The Fluent Domain-Specific Language](#the-fluent-domain-specific-language)
6. [State Management](#state-management)
7. [Agent-Specific Patterns](#agent-specific-patterns)
8. [Runtime Architecture](#runtime-architecture)
9. [Event Sourcing and Audit Trail](#event-sourcing-and-audit-trail)
10. [Infrastructure Integration](#infrastructure-integration)
11. [Source-Generated Artifacts](#source-generated-artifacts)
12. [Project Structure](#project-structure)
13. [Comparison with Existing Frameworks](#comparison-with-existing-frameworks)
14. [Key Design Decisions](#key-design-decisions)
15. [Future Considerations](#future-considerations)
16. [Conclusion](#conclusion)
17. [References](#references)

---

## Executive Summary

Strategos is a .NET library for building production-grade agentic workflows. It combines the ergonomics of modern agent frameworks with the reliability guarantees of enterprise workflow engines, while adding capabilities unique to AI-powered systems.

### Vision Statement

> Enable developers to build AI agent workflows that are as reliable and auditable as traditional enterprise systems, while preserving the flexibility that makes agents powerful.

### The Core Problem

AI agents are inherently probabilistic. Given the same input, an LLM may produce different outputs. This non-determinism creates fundamental challenges for production systems that require predictable behavior, auditability, and recovery from failures.

Current solutions force a choice between two unsatisfying options:

- **Agent frameworks** (LangGraph, CrewAI, AutoGen) that prioritize developer experience but lack production reliability—no durability, limited error recovery, poor auditability
- **Workflow engines** (Temporal, Durable Task) that provide reliability but have no awareness of agent-specific patterns—confidence handling, context management, AI-aware compensation

### Our Solution

Strategos resolves this tension through a key insight: while agent outputs are probabilistic, the workflow itself can be deterministic if we treat each agent decision as an immutable event in an event-sourced system.

The library provides:

- An intuitive fluent DSL that reads like natural language
- Automatic durability through Wolverine saga orchestration
- Full audit trails via Marten event sourcing
- Agent-native patterns: confidence routing, context assembly, conversation management
- Compensation and rollback for AI decisions
- Source-generated state machines for type safety and queryability

---

## Implementation Status

**MVP Complete:** All core features implemented with 3400+ tests passing.

| Component | Status | Description |
|-----------|--------|-------------|
| Core DSL & Builders | ✅ Complete | 11 builders, 20 abstractions |
| Source Generators | ✅ Complete | 9 artifact types, 27 emitters |
| Thompson Sampling | ✅ Complete | Contextual agent selection with Beta priors |
| State Reducers | ✅ Complete | Append, Merge, Snapshot semantics |
| Fork/Join | ✅ Complete | Parallel execution with recovery |
| Approvals | ✅ Complete | Timeout escalation, rejection paths |
| MAF Integration | ✅ Complete | Conversational state, budget enforcement |
| Instance Naming | ✅ Complete | Step type reuse with distinct identities + AGWF003 diagnostic |
| Compiler Diagnostics | ✅ Complete | 8 diagnostics for compile-time validation |

### Compiler Diagnostics Reference

The source generator reports the following diagnostics to catch workflow definition errors at compile time:

| Code | Severity | Title | Description |
|------|----------|-------|-------------|
| AGWF001 | Error | Empty workflow name | Workflow name cannot be empty or whitespace |
| AGWF002 | Warning | No steps found | Could not find any steps in workflow (verify DSL usage) |
| AGWF003 | Error | Duplicate step name | Same step type appears multiple times (use instance names) |
| AGWF004 | Error | Invalid namespace | Workflow must be declared in a namespace |
| AGWF009 | Error | Missing StartWith | Workflow must begin with StartWith\<T\>() |
| AGWF010 | Warning | Missing Finally | Workflow should end with Finally\<T\>() for completion |
| AGWF012 | Error | Fork without Join | Every Fork must be followed by Join\<T\>() |
| AGWF014 | Error | Loop without body | RepeatUntil loop must contain at least one step |

**State Reducer Diagnostics:**

| Code | Severity | Title | Description |
|------|----------|-------|-------------|
| AGSR001 | Error | Invalid attribute usage | Reducer attribute on wrong member type |
| AGSR002 | Warning | No reducers found | State class has no reducer attributes |

### Deferred Features (Consumer Responsibility)

The following features were intentionally deferred from the MVP. They are the consumer's responsibility to implement when needed:

| Feature | Workaround |
|---------|------------|
| AgentStep base class | Implement `IWorkflowStep<T>` with custom LLM integration |
| Context Assembly DSL | Manual assembly in step implementations using `IVectorSearchAdapter` |
| RAG DSL | Use `IVectorSearchAdapter` via DI |
| Conversation History Management | `[Append]` attribute + manual windowing/summarization |
| OnFailure Handler Emitter | Manual Wolverine handlers for compensation |
| Auto Projections | Manual Marten projection registration |

See [Deferred Features](./deferred-features.md) for detailed workarounds.

---

## Design Philosophy

### Core Principles

#### 1. Determinism from Probabilism

Agent outputs are non-deterministic, but workflow execution is deterministic. Every agent decision is captured as an immutable event. Given the same event history, the workflow will always be in the same state. This separation allows us to reason about workflow behavior even when individual agent outputs vary.

#### 2. Intuition Over Abstraction

The API should read like natural language. Developers describe what they want to happen, not the infrastructure that makes it happen. We avoid computer science jargon (nodes, edges, state machines) in favor of intuitive terms (steps, branches, approvals).

#### 3. Capture What the Agent Saw

For every agent decision, we record the complete context that informed that decision: the assembled prompt, retrieved documents, conversation history, and relevant state. This enables debugging ("why did it decide that?"), compliance ("what information was used?"), and replay ("what would a different model decide?").

#### 4. Durable by Default

Workflows survive process restarts, network failures, and infrastructure outages without developer intervention. Durability is not an opt-in feature—it is the fundamental execution model.

#### 5. Explicit Over Implicit

Workflow structure, branching logic, error handling, and compensation are declared explicitly in the workflow definition. There is no hidden magic. Developers can read a workflow definition and understand exactly what will happen.

#### 6. Progressive Disclosure

Simple workflows are simple to write. Complexity is opt-in. A basic linear workflow requires minimal code; advanced features (parallel execution, compensation, confidence routing) are available when needed but don't clutter simple cases.

#### 7. Production-First

The library is designed for production use from day one. This means comprehensive observability, graceful degradation, explicit error handling, and operational tooling—not just a happy-path demo.

---

## Conceptual Model

### What is a Workflow?

A workflow is a defined sequence of steps that transforms an initial state into a final state. Each step may involve agent reasoning, external service calls, human decisions, or pure computation. The workflow definition specifies the steps, their ordering, branching conditions, and error handling.

### Fundamental Concepts

#### Workflow

The top-level container that defines a complete process. A workflow has a name, a state type, an entry point, and a series of steps connected by transitions. Workflows are compiled at application startup and executed on-demand.

#### State

A strongly-typed, immutable record that captures all information relevant to a workflow execution. State is the single source of truth for workflow progress. Every step receives the current state and produces an updated state. State changes are captured as events.

#### Step

A unit of work within a workflow. Steps can be agent invocations, service calls, computations, or human review points. Each step receives the current state, performs work, and returns an updated state. Steps are the boundary where non-determinism (agent output) becomes determinism (recorded decision).

#### Transition

The movement from one step to another. Transitions can be unconditional (always proceed to the next step), conditional (branch based on state), or terminal (workflow complete). Transitions are implicit in the fluent DSL—the sequence of method calls defines the flow.

#### Phase

A discrete position within a workflow's execution. Phases are derived from step definitions and form a finite state machine. At any moment, a workflow execution is in exactly one phase. Phases enable querying ("how many claims are awaiting approval?") and visualization.

#### Context

The assembled information provided to an agent step. Context includes relevant state fields, retrieved documents (RAG), conversation history, and any other information the agent needs. Context assembly is declarative and automatically captured for audit.

#### Event

An immutable record of something that happened during workflow execution: a step completed, a branch was taken, an approval was requested, a decision was made. Events are the foundation of the audit trail and enable time-travel debugging.

### The Workflow Lifecycle

#### 1. Definition

Developers define workflows using the fluent DSL. Workflow definitions are validated at compile time (via source generators) and application startup. Invalid workflows fail fast with clear error messages.

#### 2. Compilation

Workflow definitions are compiled into executable artifacts: state machine phases, message types, event types, and saga handlers. This compilation happens automatically—developers work with the high-level DSL.

#### 3. Instantiation

When a workflow is started, an instance is created with an initial state and unique identifier. The instance is immediately persisted, ensuring durability from the first moment.

#### 4. Execution

Steps execute in sequence according to the workflow definition. After each step, the updated state is persisted and an event is recorded. If the process crashes, execution resumes from the last persisted state.

#### 5. Branching

At branch points, the workflow evaluates conditions against current state and routes to the appropriate path. Branch decisions are recorded as events for auditability.

#### 6. Pausing

Workflows can pause to await external input: human approval, external service callbacks, or scheduled delays. Paused workflows are persisted and resume when input arrives.

#### 7. Completion

When a workflow reaches a terminal step, it completes. The final state is recorded, completion events are emitted, and the workflow instance may be archived or retained based on policy.

#### 8. Compensation

If a workflow fails or needs to be rolled back, compensation handlers execute in reverse order to undo prior steps' effects. Compensation is itself recorded as events.

---

## The Fluent Domain-Specific Language

### Design Goals

The DSL is the primary interface developers use to define workflows. Its design prioritizes:

- **Readability**: Workflow definitions should read like prose
- **Discoverability**: IDE autocomplete guides developers to valid options
- **Type Safety**: Invalid workflows should fail at compile time, not runtime
- **Expressiveness**: Complex workflows should be expressible without escape hatches
- **Familiarity**: Patterns should feel natural to .NET developers

### Vocabulary

The DSL uses carefully chosen terms that convey meaning without requiring graph theory knowledge:

| Term | Meaning | Rationale |
|------|---------|-----------|
| `Workflow` | The complete process definition | Universal business term |
| `StartWith` | The first step to execute | Clear entry point |
| `Then` | The next step in sequence | Natural continuation ("do this, then that") |
| `Branch` | Conditional path selection | Fork in the road metaphor |
| `Fork` / `Join` | Parallel execution and synchronization | Familiar concurrency terms |
| `RepeatUntil` | Iteration with exit condition | Reads like English |
| `AwaitApproval` | Pause for human decision | Intent is immediately clear |
| `Finally` | The concluding step | Familiar from try/finally |
| `Compensate` | Rollback handler for a step | Standard saga terminology |

### Syntax Patterns

#### Basic Linear Workflow

The simplest workflow is a linear sequence of steps:

```csharp
Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Then<FulfillOrder>()
    .Finally<SendConfirmation>();
```

This reads naturally: "Create a process-order workflow. Start with validating the order, then process payment, then fulfill the order, and finally send confirmation."

#### Conditional Branching

Branches route execution based on state:

```csharp
Workflow<ClaimState>
    .Create("process-claim")
    .StartWith<AssessClaim>()
    .Branch(state => state.ClaimType,
        when: ClaimType.Auto, then: flow => flow
            .Then<AutoClaimProcessor>(),
        when: ClaimType.Property, then: flow => flow
            .Then<PropertyInspection>()
            .Then<PropertyClaimProcessor>(),
        otherwise: flow => flow
            .Then<ManualReview>())
    .Finally<NotifyClaimant>();
```

The branch selector extracts a value from state; each `when` clause handles a specific case. Branches automatically rejoin at the next step after the Branch block.

#### Human-in-the-Loop

Workflows can pause for human decisions:

```csharp
Workflow<DocumentState>
    .Create("document-approval")
    .StartWith<DraftDocument>()
    .Then<LegalReview>()
    .AwaitApproval<LegalTeam>(options => options
        .WithTimeout(TimeSpan.FromDays(2))
        .OnTimeout(flow => flow.Then<EscalateToManager>()))
    .Then<PublishDocument>()
    .Finally<NotifyStakeholders>();
```

The workflow pauses at `AwaitApproval`, persists its state, and resumes when approval is received (or handles timeout if configured).

#### Parallel Execution

Fork executes multiple paths concurrently; Join synchronizes results:

```csharp
Workflow<AnalysisState>
    .Create("comprehensive-analysis")
    .StartWith<GatherData>()
    .Fork(
        flow => flow.Then<FinancialAnalysis>(),
        flow => flow.Then<TechnicalAnalysis>(),
        flow => flow.Then<MarketAnalysis>())
    .Join<SynthesizeResults>()
    .Finally<GenerateReport>();
```

All forked paths execute in parallel. The Join step receives the combined state (merged via reducers) from all paths.

#### Iteration

RepeatUntil enables loops with explicit exit conditions:

```csharp
Workflow<RefinementState>
    .Create("iterative-refinement")
    .StartWith<GenerateDraft>()
    .RepeatUntil(
        condition: state => state.QualityScore >= 0.9m,
        maxIterations: 5,
        body: flow => flow
            .Then<Critique>()
            .Then<Refine>())
    .Finally<Publish>();
```

The loop continues until the condition is met or maximum iterations are reached. Built-in cycle detection prevents infinite loops.

#### Error Handling and Compensation

Steps can define compensation handlers for rollback:

```csharp
Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ChargePayment>()
        .Compensate<RefundPayment>()
    .Then<ReserveInventory>()
        .Compensate<ReleaseInventory>()
    .Then<ShipOrder>()
    .Finally<Confirm>()
    .OnFailure(flow => flow.Then<NotifyFailure>());
```

If ShipOrder fails, compensation runs in reverse: ReleaseInventory, then RefundPayment. The OnFailure handler executes after compensation.

### Step Implementation Patterns

Steps referenced in the DSL are implemented using one of three patterns, chosen based on complexity and requirements.

#### Pattern 1: Class-Based Steps (Recommended for Complex Logic)

For steps requiring dependencies, complex logic, or external service calls:

```csharp
public class AssessClaimValidity : WorkflowStep<InsuranceClaimState>
{
    private readonly IClaimAssessmentService _assessmentService;

    public AssessClaimValidity(IClaimAssessmentService assessmentService)
    {
        _assessmentService = assessmentService;
    }

    public override async Task<StepResult<InsuranceClaimState>> ExecuteAsync(
        InsuranceClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        var assessment = await _assessmentService.AssessAsync(state.Claim, ct);

        return state
            .With(s => s.Assessment, assessment)
            .With(s => s.Confidence, assessment.Confidence)
            .AsResult();
    }
}
```

Steps are resolved via dependency injection, enabling constructor injection of any registered service.

#### Pattern 2: Inline Lambda (Simple Transformations)

For trivial state updates that don't warrant a dedicated class:

```csharp
.Then("log-entry", (state, ctx) =>
    state.With(s => s.ProcessingStarted, ctx.Timestamp))
```

Lambda steps receive the current state and a `StepContext` containing correlation ID, timestamp, and execution metadata.

#### Pattern 3: Agent Steps (LLM-Specific)

For steps that invoke language models with structured output:

```csharp
public class AssessClaimValidity : AgentStep<InsuranceClaimState>
{
    public override AgentConfig ConfigureAgent() => new()
    {
        Instructions = "You are an insurance claim assessor...",
        Model = "gpt-4o",
        OutputSchema = typeof(ClaimAssessment)
    };

    public override InsuranceClaimState ApplyResult(
        InsuranceClaimState state,
        ClaimAssessment result)
        => state.With(s => s.Assessment, result);
}
```

The `AgentStep<TState>` base class handles prompt construction, model invocation, response parsing, and confidence extraction. The `ApplyResult` method maps the structured output to state updates.

---

## State Management

### Immutable State Records

Workflow state is defined as an immutable C# record with explicit properties for all data the workflow needs:

```csharp
[WorkflowState]
public record OrderState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public Order Order { get; init; }
    public PaymentResult? Payment { get; init; }
    public ShipmentInfo? Shipment { get; init; }
    public OrderStatus Status { get; init; }
}
```

Immutability ensures that state changes are explicit and traceable. Steps return new state instances using `with` expressions rather than mutating existing state.

### Reducer Semantics

When state is updated, properties are combined using reducers. Different properties may use different reduction strategies:

**Overwrite (Default)**

The new value replaces the old value. This is the default for scalar properties.

**Append**

New items are appended to existing collections. Use the `[Append]` attribute on list properties:

```csharp
[Append]
public ImmutableList<ChatMessage> Messages { get; init; }
```

**Merge**

Dictionary entries are merged, with new values overwriting existing keys. Use the `[Merge]` attribute:

```csharp
[Merge]
public ImmutableDictionary<string, object> Metadata { get; init; }
```

Reducers are generated at compile time by a source generator, ensuring type safety and optimal performance.

### State Validation

State can be validated at transition boundaries to catch invalid conditions early:

```csharp
.Then<ProcessOrder>(step => step
    .ValidateState(state =>
        state.Order.Items.Any()
            ? ValidationResult.Success
            : ValidationResult.Failure("Order must have items")))
```

Validation failures prevent the step from executing and trigger error handling.

---

## Agent-Specific Patterns

While the workflow engine is agent-agnostic, it provides first-class support for patterns unique to AI agent workflows.

### Implemented: Thompson Sampling Agent Selection

The library implements contextual multi-armed bandit agent selection using Thompson Sampling. This enables online learning of agent performance across different task categories:

```csharp
// Configure agent selection
services.AddAgentSelection(options => options
    .WithPrior(alpha: 2, beta: 2)  // Uninformative prior
    .WithCategories(TaskCategory.Analysis, TaskCategory.Coding));

// Select agent for task
var selector = services.GetRequiredService<IAgentSelector>();
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

**Key Types:**
- `IAgentSelector` - Selects agents via Thompson Sampling
- `ITaskFeatureExtractor` - Extracts features for category classification
- `TaskCategory` - 7 categories: Analysis, Coding, Research, Writing, Data, Integration, General
- `AgentBelief` - Beta(α, β) distribution per (agent, category) pair
- `TaskFeatures` - Extracted features with complexity and matched keywords

**Selection Algorithm:**
1. Extract features from task description to classify category
2. For each agent, sample θ from Beta(α, β) for that category
3. Select agent with highest sampled θ
4. After execution, update belief: success → α++, failure → β++

### Implemented: Confidence-Based Routing

Agent outputs often include confidence scores. The framework enables routing based on confidence thresholds:

```csharp
.Then<AssessClaim>(step => step
    .RequireConfidence(0.85m)
    .OnLowConfidence(flow => flow
        .AwaitApproval<SeniorAdjuster>()))
```

If the agent's confidence is below the threshold, execution routes to human review. This pattern enables graceful degradation—high-confidence decisions proceed automatically; uncertain decisions get human oversight.

### Consumer Responsibility: Context Assembly

> **Deferred Feature:** Context assembly DSL was deferred from MVP. Implement manually in step classes.

Agent steps require context assembly. Use `IVectorSearchAdapter` via dependency injection:

```csharp
public class AnalyzeDocumentStep : IWorkflowStep<ClaimState>
{
    private readonly IVectorSearchAdapter _vectorSearch;
    private readonly IChatClient _chatClient;

    public async Task<ClaimState> ExecuteAsync(ClaimState state, CancellationToken ct)
    {
        // Manual context assembly
        var retrievedDocs = await _vectorSearch.SearchAsync(
            state.SearchQuery, topK: 10, minRelevance: 0.7);

        var context = $"""
            Document: {state.Document}
            Related: {string.Join("\n", retrievedDocs.Select(d => d.Content))}
            """;

        // Use assembled context with LLM
        var response = await _chatClient.GetResponseAsync(context, ct);
        return state with { Analysis = response };
    }
}
```

### Consumer Responsibility: RAG Integration

> **Deferred Feature:** RAG DSL was deferred from MVP. Use `IVectorSearchAdapter` directly.

Register vector search adapter at startup:

```csharp
services.AddSingleton<IVectorSearchAdapter, YourVectorSearchAdapter>();
```

The `IVectorSearchAdapter` interface supports:
- Vector similarity search
- Hybrid search (combine vector + keyword)
- Filtering by metadata

### Consumer Responsibility: Conversation History

> **Deferred Feature:** History management DSL was deferred. Use `[Append]` attribute with manual windowing.

For conversational workflows, use the `[Append]` state reducer attribute:

```csharp
public record ConversationalState
{
    [Append]
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];
}
```

Implement manual windowing/summarization in step logic when context limits are reached.

### Agent Versioning

When agents are updated (new prompts, different models), the framework tracks which version produced each decision:

```
AgentDecisionEvent {
    AgentId: "claim-assessor",
    AgentVersion: "v2.3.1",
    ModelUsed: "gpt-4o",
    Decision: { ... },
    Confidence: 0.92,
    TokensUsed: 1247
}
```

This enables A/B testing of agent versions, debugging regressions, and compliance reporting.

---

## Runtime Architecture

The following diagram illustrates the complete compilation and runtime flow:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Developer Experience                             │
│                                                                         │
│   Workflow<ClaimState>                                                  │
│       .Create("ProcessClaim")                                           │
│       .StartWith<GatherContext>()                                       │
│       .Then<AssessClaim>()                                              │
│       .Branch(...)                                                      │
│       .Finally<Notify>();                                               │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ Source Generator (Compile Time)
┌─────────────────────────────────────────────────────────────────────────┐
│                        Generated Artifacts                              │
│                                                                         │
│   1. ProcessClaimPhase enum (state machine phases)                      │
│   2. ProcessClaimTransitions (valid phase transitions)                  │
│   3. ProcessClaimSaga : WorkflowSaga<ClaimState, ProcessClaimPhase>     │
│   4. ProcessClaimCommands (Wolverine messages)                          │
│   5. ProcessClaimEvents (Marten events)                                 │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ Runtime Execution
┌─────────────────────────────────────────────────────────────────────────┐
│                        Wolverine / Marten                               │
│                                                                         │
│   Wolverine:                      Marten:                               │
│   - Routes commands to saga       - Persists saga state                 │
│   - Handles message retry         - Stores event stream                 │
│   - Manages saga lifecycle        - Projects read models                │
│   - Transactional outbox          - Enables time-travel                 │
└─────────────────────────────────────────────────────────────────────────┘
```

### Compilation Pipeline

Workflow definitions are compiled into executable artifacts through a multi-stage pipeline:

**Stage 1: DSL Parsing**

The fluent DSL calls are captured into an intermediate workflow definition structure representing steps, transitions, and configuration.

**Stage 2: Validation**

The workflow definition is validated for structural correctness: unreachable steps, missing transitions, invalid branch targets, cycle detection. Validation errors are reported with clear messages.

**Stage 3: Source Generation**

Source generators produce compile-time artifacts:

- Phase enumeration (the state machine states)
- Transition table (valid phase-to-phase transitions)
- Command types (Wolverine messages)
- Event types (Marten events)
- State reducers
- Saga class with handlers

**Stage 4: Registration**

At application startup, compiled workflows are registered with the dependency injection container and the Wolverine message router.

### Execution Model

Workflow execution follows a message-driven pattern:

1. A `StartWorkflow` command initiates execution
2. The saga handles the command, initializes state, and cascades to the first step
3. Each step handler executes the step logic, updates state, and cascades to the next step
4. At branch points, the handler evaluates conditions and cascades to the appropriate path
5. At pause points (human approval, external events), no cascade occurs—the saga waits
6. When resumption input arrives, the saga continues from where it paused
7. At completion, the saga marks itself complete and emits final events

### Durability Guarantees

The runtime provides strong durability guarantees:

- State is persisted after every step via PostgreSQL
- Message processing uses transactional outbox pattern—state update and next-step message are atomic
- If a process crashes, incomplete steps are retried from the last committed state
- Optimistic concurrency prevents conflicting updates to the same workflow instance

---

## Event Sourcing and Audit Trail

### Events as the Source of Truth

Every significant occurrence during workflow execution is captured as an immutable event:

- **WorkflowStarted**: Workflow instance created with initial state
- **PhaseChanged**: Transition from one phase to another
- **StepCompleted**: A step finished executing with its output
- **BranchTaken**: A routing decision was made
- **ContextAssembled**: The context provided to an agent step
- **ApprovalRequested**: Workflow paused for human input
- **ApprovalReceived**: Human input received
- **WorkflowCompleted**: Workflow reached terminal state
- **CompensationExecuted**: A rollback action was performed

### Time-Travel Debugging

Because all state changes are captured as events, we can reconstruct the state at any point in history:

```csharp
// Get state at a specific version
var historicalState = await checkpointService
    .GetStateAtVersionAsync(workflowId, version: 5);

// Get state at a specific timestamp
var historicalState = await checkpointService
    .GetStateAtTimestampAsync(workflowId, timestamp);
```

This enables powerful debugging: "What was the state when the agent made that decision?" is always answerable.

### Replay and What-If Analysis

Events enable replay scenarios:

- Replay a workflow to verify behavior
- Fork from a historical point to explore alternative paths
- Re-run agent decisions with updated models to compare outputs
- Audit what information was available at each decision point

### Projections and Read Models

Events are projected into read models optimized for querying:

```csharp
// Query workflows by phase
var awaitingApproval = await session
    .Query<WorkflowReadModel>()
    .Where(w => w.CurrentPhase == Phase.AwaitingApproval)
    .ToListAsync();
```

Read models are updated asynchronously from the event stream, providing eventual consistency with minimal latency.

---

## Infrastructure Integration

The following table shows how workflow concepts map to Wolverine and Marten primitives:

| Workflow Concept | Generated Artifact | Wolverine/Marten Primitive |
|------------------|-------------------|---------------------------|
| `Workflow<T>` | `XxxSaga` class | `Saga` base class |
| Workflow instance | Saga instance | Document with `[Identity]` |
| Current position | `CurrentPhase` property | Saga state (persisted) |
| Step execution | Handler method | `Handle(XxxCommand)` |
| Step transition | Command cascade | `return new NextCommand()` |
| Branch decision | Router in handler | Conditional return |
| State change | Event append | `session.Events.Append()` |
| Checkpoint | Event version | Marten stream version |
| Human-in-loop | Saga pause | No cascade + await message |
| Compensation | Compensation handlers | Reverse event application |

This mapping is essential for debugging and understanding how workflows execute at the infrastructure level.

### Wolverine Integration

Wolverine provides the saga orchestration and message routing infrastructure:

**Saga Persistence**

Workflow state is stored as Wolverine saga state in PostgreSQL. The saga lifecycle (creation, updates, completion) is managed automatically.

**Message Routing**

Step transitions are implemented as Wolverine messages. The framework generates command types with proper saga identity attributes for routing.

**Transactional Outbox**

State updates and outgoing messages are committed atomically, ensuring exactly-once processing semantics.

**Retry Policies**

Wolverine's retry policies handle transient failures. The framework provides sensible defaults with configuration options for customization.

### Marten Integration

Marten provides event sourcing and document storage:

**Event Streams**

Each workflow instance has a dedicated event stream. Events are appended atomically with optimistic concurrency.

**Projections**

Marten projections build read models from event streams. The framework provides default projections for workflow status; custom projections can be added.

**Time-Travel Queries**

Marten's `AggregateStreamAsync` enables reconstructing state at any version or timestamp.

### Agent Framework Integration

The library integrates with LLM frameworks through adapters:

- **Microsoft Semantic Kernel**: Native integration for agents and plugins
- **Microsoft Agent Framework**: Adapter for ChatClientAgent with automatic TurnToken handling
- **Direct API Clients**: Adapters for OpenAI, Anthropic, and other providers

Adapters handle the mapping between workflow state and agent input/output, context assembly, and response parsing.

---

## Source-Generated Artifacts

The source generator produces 9 artifact types via 27 specialized emitters:

| Artifact | Emitter | Purpose |
|----------|---------|---------|
| Phase Enum | `PhaseEnumEmitter` | Type-safe workflow phases including loops |
| Commands | `CommandsEmitter` | Wolverine Start + Execute commands |
| Events | `EventsEmitter` | Marten events with `[SagaIdentity]` |
| Transitions | `TransitionsEmitter` | Valid phase transition table |
| State Reducers | `StateReducerEmitter` | Property merge semantics (`[Append]`, `[Merge]`) |
| Saga | `SagaEmitter` (12 sub-emitters) | Complete Wolverine saga with handlers |
| Worker Handlers | `WorkerHandlerEmitter` | Brain & Muscle execution pattern |
| Extensions | `ExtensionsEmitter` | DI registration helpers |
| Mermaid | `MermaidEmitter` | Visual workflow documentation |

**Loop Phase Naming Convention:**

For `RepeatUntil` loops, step phases are prefixed with the loop name:
```
Refinement_Critique    // Loop "Refinement" contains step "Critique"
Outer_Inner_Step       // Nested loop hierarchy preserved
```

### Phase Enumeration

For each workflow, a source generator produces an enumeration of all possible phases:

```csharp
[GeneratedCode("LevelUp.Strategos", "<version>")]
public enum ProcessClaimPhase
{
    NotStarted,
    AssessClaim,
    AutoProcess,
    AwaitingApproval,
    ManualProcess,
    NotifyClaimant,
    Completed,
    Failed
}
```

This provides type-safe workflow position tracking and enables efficient queries.

> **Coverage marking (#148).** Every generated type carries
> `[GeneratedCode("LevelUp.Strategos", <version>)]`, and every generated
> class/struct/record additionally carries `[ExcludeFromCodeCoverage]` (omitted from the
> snippets in this document for brevity; the attribute is invalid on enum/interface/delegate,
> which have no executable code to cover). These are applied **centrally** at the generator's
> single `AddSource` boundary by `GeneratedCodeStamper`, not per emit site, so generated
> code is excluded from every consumer's coverage report with no `.runsettings`.

### Transition Validation

The generator produces a transition table for validation:

```csharp
[GeneratedCode("LevelUp.Strategos", "<version>")]
public static class ProcessClaimTransitions
{
    public static readonly IReadOnlyDictionary<Phase, Phase[]>
        Valid = new Dictionary<Phase, Phase[]>
    {
        [Phase.AssessClaim] = [Phase.AutoProcess, Phase.AwaitingApproval],
        [Phase.AutoProcess] = [Phase.NotifyClaimant],
        // ...
    };
}
```

Invalid transitions are caught immediately rather than causing silent failures.

### Command and Event Types

Each step produces corresponding command and event types:

```csharp
public record ExecuteAssessClaimCommand(
    [property: SagaIdentity] Guid WorkflowId);

public record AssessClaimCompleted(
    Guid WorkflowId,
    ClaimAssessment Result,
    DateTimeOffset Timestamp) : IWorkflowEvent;
```

### Saga Implementation

The generator produces a complete saga class with handlers for each step, proper routing, event emission, and lifecycle management. Developers never write saga boilerplate—they define workflows; the generator produces infrastructure.

### Complete Example

For this workflow definition:

```csharp
var workflow = Workflow<ClaimState>
    .Create("ProcessClaim")
    .StartWith<GatherContext>()
    .Then<AssessClaim>(step => step.RequireConfidence(0.85m))
    .Branch(state => state.ClaimType,
        when: ClaimType.Auto, then: flow => flow.Then<AutoProcess>(),
        when: ClaimType.Manual, then: flow => flow
            .AwaitApproval<ClaimsAdjuster>()
            .Then<ManualProcess>())
    .Finally<NotifyClaimant>();
```

The source generator produces the following artifacts (condensed for illustration):

```csharp
// ═══════════════════════════════════════════════════════════════════════
// 1. Phase Enumeration
// ═══════════════════════════════════════════════════════════════════════

[GeneratedCode("LevelUp.Strategos", "<version>")]
public enum ProcessClaimPhase
{
    NotStarted,
    GatherContext,
    AssessClaim,
    AutoProcess,
    AwaitingApproval,
    ManualProcess,
    NotifyClaimant,
    Completed,
    Failed
}

// ═══════════════════════════════════════════════════════════════════════
// 2. Commands (Wolverine Messages)
// ═══════════════════════════════════════════════════════════════════════

[GeneratedCode("LevelUp.Strategos", "<version>")]
public sealed record StartProcessClaimCommand(
    Guid WorkflowId,
    ClaimState InitialState);

[GeneratedCode("LevelUp.Strategos", "<version>")]
public sealed record ExecuteGatherContextCommand(
    [property: SagaIdentity] Guid WorkflowId);

[GeneratedCode("LevelUp.Strategos", "<version>")]
public sealed record ExecuteAssessClaimCommand(
    [property: SagaIdentity] Guid WorkflowId);

// ... additional commands for each step

// ═══════════════════════════════════════════════════════════════════════
// 3. Saga Class
// ═══════════════════════════════════════════════════════════════════════

[GeneratedCode("LevelUp.Strategos", "<version>")]
public partial class ProcessClaimSaga : Saga
{
    [SagaIdentity]
    [Identity]
    public Guid WorkflowId { get; set; }

    [Version]
    public int Version { get; set; }

    public ClaimState State { get; set; } = new();
    public ProcessClaimPhase CurrentPhase { get; set; } = ProcessClaimPhase.NotStarted;

    // ── Start Handler ──────────────────────────────────────────────────

    public static (ProcessClaimSaga, ExecuteGatherContextCommand) Start(
        StartProcessClaimCommand command,
        IDocumentSession session,
        TimeProvider time)
    {
        var saga = new ProcessClaimSaga
        {
            WorkflowId = command.WorkflowId,
            State = command.InitialState,
            CurrentPhase = ProcessClaimPhase.GatherContext
        };

        session.Events.StartStream<ProcessClaimSaga>(
            command.WorkflowId,
            new ProcessClaimStarted(command.WorkflowId, command.InitialState, time.GetUtcNow()));

        return (saga, new ExecuteGatherContextCommand(command.WorkflowId));
    }

    // ── Linear Step Handler (GatherContext → AssessClaim) ──────────────

    public async Task<ExecuteAssessClaimCommand> Handle(
        ExecuteGatherContextCommand command,
        GatherContext step,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct)
    {
        var result = await step.ExecuteAsync(State, ct);
        State = ClaimStateReducer.Reduce(State, result.StateUpdate);

        session.Events.Append(WorkflowId, new ProcessClaimPhaseChanged(
            WorkflowId, CurrentPhase, ProcessClaimPhase.AssessClaim,
            nameof(GatherContext), time.GetUtcNow()));

        CurrentPhase = ProcessClaimPhase.AssessClaim;
        return new ExecuteAssessClaimCommand(WorkflowId);
    }

    // ── Branch Handler (AssessClaim → Auto/Manual) ─────────────────────

    public async Task<object> Handle(
        ExecuteAssessClaimCommand command,
        AssessClaim step,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct)
    {
        var result = await step.ExecuteAsync(State, ct);
        State = ClaimStateReducer.Reduce(State, result.StateUpdate);

        // Branch based on claim type (generated from DSL)
        return State.ClaimType switch
        {
            ClaimType.Auto => TransitionWithCommand(
                ProcessClaimPhase.AutoProcess,
                new ExecuteAutoProcessCommand(WorkflowId),
                session, time),

            ClaimType.Manual => RequestApproval<ClaimsAdjuster>(
                session, time),

            _ => throw new InvalidWorkflowBranchException(State.ClaimType.ToString())
        };
    }

    // ── Finally Handler (NotifyClaimant → Completed) ───────────────────

    public async Task Handle(
        ExecuteNotifyClaimantCommand command,
        NotifyClaimant step,
        IDocumentSession session,
        TimeProvider time,
        CancellationToken ct)
    {
        var result = await step.ExecuteAsync(State, ct);
        State = ClaimStateReducer.Reduce(State, result.StateUpdate);

        CurrentPhase = ProcessClaimPhase.Completed;

        session.Events.Append(WorkflowId, new ProcessClaimCompleted(
            WorkflowId, State, WorkflowOutcome.Success, time.GetUtcNow()));

        MarkCompleted();  // Signal Wolverine to archive saga
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 4. Projection (Read Model)
// ═══════════════════════════════════════════════════════════════════════

[GeneratedCode("LevelUp.Strategos", "<version>")]
public class ProcessClaimProjection : SingleStreamProjection<ProcessClaimReadModel>
{
    public ProcessClaimReadModel Create(ProcessClaimStarted evt) => new()
    {
        WorkflowId = evt.WorkflowId,
        CurrentPhase = ProcessClaimPhase.GatherContext,
        StartedAt = evt.Timestamp
    };

    public void Apply(ProcessClaimPhaseChanged evt, ProcessClaimReadModel model)
    {
        model.CurrentPhase = evt.ToPhase;
        model.LastTransitionAt = evt.Timestamp;
    }

    public void Apply(ProcessClaimCompleted evt, ProcessClaimReadModel model)
    {
        model.CurrentPhase = ProcessClaimPhase.Completed;
        model.CompletedAt = evt.Timestamp;
        model.Outcome = evt.Outcome;
    }
}
```

This example demonstrates the key patterns: saga lifecycle management, command cascading for step transitions, branch routing via switch expressions, and event emission for audit trails. The complete generated output for a production workflow is typically 400-600 lines depending on complexity.

---

## Project Structure

The implementation spans three projects with 169 total types:

```
src/Strategos/                     (73 files, 93 types)
├── Abstractions/                          20 interfaces
│   ├── IWorkflowStep.cs                   Step contract
│   ├── IBeliefStore.cs                    Thompson Sampling persistence
│   ├── IAgentSelector.cs                  Agent selection contract
│   └── ...
├── Builders/                              11 implementations
│   ├── WorkflowBuilder.cs                 Main fluent API entry
│   ├── StepBuilder.cs                     Step configuration
│   ├── BranchBuilder.cs                   Conditional routing
│   ├── ForkBuilder.cs                     Parallel execution
│   ├── LoopBuilder.cs                     RepeatUntil loops
│   └── ...
├── Selection/                             7 types (Thompson Sampling)
│   ├── AgentBelief.cs                     Beta(α, β) parameters
│   ├── TaskCategory.cs                    7 task categories
│   ├── TaskFeatures.cs                    Extracted features
│   └── AgentSelection.cs                  Selection result
├── Steps/                                 5 types
│   ├── WorkflowStepResult.cs              Step execution result
│   └── ...
└── Definitions/                           18 types
    ├── WorkflowDefinition.cs              Parsed workflow model
    ├── StepDefinition.cs                  Step metadata
    └── ...

src/Strategos.Infrastructure/      (14 files, 16 types)
└── Selection/                             5 implementations
    ├── InMemoryBeliefStore.cs             Testing/dev persistence
    ├── ContextualAgentSelector.cs         Thompson Sampling selector
    ├── KeywordTaskFeatureExtractor.cs     Category classification
    └── ...

src/Strategos.Generators/          (66 files, 60 types)
├── Emitters/                              27 implementations
│   ├── PhaseEnumEmitter.cs                Phase enumeration
│   ├── CommandsEmitter.cs                 Wolverine commands
│   ├── EventsEmitter.cs                   Marten events
│   ├── SagaEmitter.cs                     Saga orchestration
│   └── Saga/                              12 sub-emitters
│       ├── SagaStartMethodEmitter.cs
│       ├── SagaStepHandlersEmitter.cs
│       ├── SagaApprovalHandlersEmitter.cs
│       └── ...
├── Models/                                11 types
│   ├── WorkflowModel.cs                   Parsed workflow for emission
│   ├── StepModel.cs                       Step metadata
│   └── ...
├── Helpers/                               16 utilities
│   ├── StepExtractor.cs                   DSL parsing
│   ├── BranchExtractor.cs                 Branch parsing
│   └── ...
└── Diagnostics/                           2 types
    ├── WorkflowDiagnostics.cs             Analyzer warnings
    └── StateReducerDiagnostics.cs         State reducer warnings
```

---

## Comparison with Existing Frameworks

### Positioning

Strategos occupies a unique position in the framework landscape:

| Framework | Strengths | Gaps We Address |
|-----------|-----------|-----------------|
| **LangGraph** | Agent-native, good visualization, active community | Snapshot-only checkpoints, no compensation, no confidence routing |
| **CrewAI** | Simple mental model, quick prototyping | No durability, limited workflow control, no human-in-loop |
| **AutoGen** | Flexible multi-agent patterns, Microsoft backing | No durability, implicit state, non-deterministic flow |
| **Temporal** | Battle-tested scale, strong durability | No agent awareness, imperative style, separate server |
| **Durable Task** | Native .NET, Azure integration | No agent awareness, limited event model |

### Unique Capabilities

The following capabilities are unique to Strategos or rare among alternatives:

- Confidence-based routing as a first-class DSL feature
- Declarative context assembly with automatic capture
- Event-sourced audit trail (not just checkpoints)
- Compensation handlers for agent decisions
- Fluent DSL with source-generated state machines
- Integrated RAG and conversation history management
- Agent versioning and decision attribution

### Target Use Cases

Strategos is ideal for:

- Production AI systems requiring reliability and auditability
- Regulated industries needing complete decision trails
- Complex multi-step agent workflows with human oversight
- .NET organizations with existing Wolverine/Marten infrastructure
- Teams valuing type safety and compile-time validation

---

## Key Design Decisions

### Wolverine-First Runtime

**Decision**: Use Wolverine sagas as the primary execution runtime, not Microsoft Agent Framework workflows.

**Rationale**: Wolverine provides battle-tested durability, transactional outbox, and PostgreSQL integration. MAF Workflows are in preview with uncertain API stability. Wolverine-first isolates us from MAF API churn while preserving agent integration through adapters.

### Source Generation over Reflection

**Decision**: Generate state machines, reducers, and saga handlers at compile time rather than using runtime reflection.

**Rationale**: Source generation provides compile-time validation, optimal performance, and excellent IDE support. Invalid workflows fail at build time with clear errors rather than at runtime with cryptic exceptions.

### Derived State Machine Phases

**Decision**: Generate explicit HSM phases from workflow definitions rather than using string-based step tracking.

**Rationale**: Explicit phases enable type-safe queries, transition validation, and visualization. Developers work with intuitive DSL; operations teams get queryable state machines.

### Fluent DSL over Graph API

**Decision**: Use intuitive vocabulary (Then, Branch, Finally) rather than graph-theoretic terms (Node, Edge, ConditionalEdge).

**Rationale**: The DSL should read like prose describing a business process. Graph terminology creates unnecessary cognitive distance between intent and implementation.

### Context Assembly as First-Class Concept

**Decision**: Make context assembly declarative and automatically captured, rather than leaving it implicit in step implementations.

**Rationale**: For AI systems, "what did the agent see?" is a critical question for debugging, compliance, and replay. Declarative context assembly makes this question always answerable.

### Event Sourcing over Snapshots

**Decision**: Use full event sourcing rather than checkpoint snapshots for persistence.

**Rationale**: Snapshots answer "where is it now?" but events answer "how did it get there?" For auditable AI systems, the journey matters as much as the destination.

### Implicit Branch Rejoining

**Decision**: Branches automatically rejoin at the next `Then()` or `Finally()` call after the `Branch()` block, unless a branch explicitly calls `Complete()`.

**Rationale**: Most branches are temporary divergences that converge back to a common path. Implicit rejoining reduces boilerplate and matches developer intuition. Branches that terminate the workflow can opt out with `Complete()`.

### Convention-Based Step Naming with Instance Names

**Decision**: Step names are derived from type names by default (PascalCase to kebab-case), with optional explicit override: `.Then<ProcessPayment>()` produces step name "process-payment"; `.Then<ProcessPayment>("charge-customer")` overrides to "charge-customer".

**Rationale**: Convention over configuration reduces verbosity while preserving flexibility. Derived names are predictable and match common .NET naming patterns.

**Instance Names for Step Reuse**: Instance names enable reusing the same step type in multiple parallel paths:

```csharp
.Fork(
    path => path.Then<AnalyzeStep>("Technical"),
    path => path.Then<AnalyzeStep>("Fundamental"))
.Join<SynthesizeStep>()
```

This generates:
- **Phases:** `Technical`, `Fundamental` (distinct identities for state machine routing)
- **Handler:** ONE `AnalyzeStepHandler` (shared by step TYPE - Wolverine routes by message type)
- **Commands/Events:** ONE `ExecuteAnalyzeStepWorkerCommand` and `AnalyzeStepCompleted` (shared by TYPE)

Without instance names, duplicate step types in Fork paths (or linear flow) trigger AGWF003 compile-time error. See [Step Deduplication Constraints](./archive/step-deduplication-constraints.md) for full architectural details.

### Escape Hatch to Explicit Graph

**Decision**: The `AsGraph()` method provides access to explicit graph operations (`WithNode`, `WithEdge`, `WithConditionalEdge`) when the fluent DSL is insufficient.

**Rationale**: The fluent DSL covers common patterns elegantly, but edge cases exist. Rather than bloating the DSL with rarely-used features, we provide an escape hatch to the underlying graph API. This preserves the simplicity of the DSL while ensuring expressiveness.

---

## Future Considerations

The following capabilities are not in the initial design but are anticipated for future versions:

### Visual Workflow Editor

A graphical editor for designing workflows, with bidirectional sync to the DSL. Workflows designed visually would generate equivalent code; code changes would reflect in the visual representation.

### Cross-Workflow Coordination

Patterns for workflows that depend on each other: Workflow A waiting for Workflow B's output, shared state projections, and rendezvous points.

### Speculative Execution

Starting multiple possible paths concurrently and committing to one when a decision crystallizes, compensating the others. Useful for latency optimization when agent calls are slow.

### Multi-Model Ensembles

Built-in support for querying multiple models and aggregating their outputs, with configurable consensus strategies.

### Cost Attribution

Per-workflow and per-step tracking of LLM costs (tokens, API calls), enabling cost allocation and optimization.

### Workflow Versioning

Managing workflow definition changes when instances are in flight: migration strategies, parallel version support, and gradual rollout.

---

## Conclusion

Strategos represents a synthesis of two previously separate domains: the ergonomics of modern agent frameworks and the reliability of enterprise workflow engines. By treating agent decisions as events in an event-sourced system, we achieve deterministic workflow behavior from probabilistic agent outputs.

The design prioritizes:

- **Developer intuition** through a fluent, readable DSL
- **Production reliability** through Wolverine/Marten integration
- **Complete auditability** through event sourcing
- **Agent-native patterns** through confidence routing and context assembly
- **Type safety** through source generation

The result is a framework that makes building production-grade AI agent workflows as straightforward as building traditional business processes—without sacrificing the flexibility that makes agents powerful.

> Enable developers to build AI agent workflows that are as reliable and auditable as traditional enterprise systems, while preserving the flexibility that makes agents powerful.

---

## References

### Implementation Documentation

- [Implementation Roadmap](./archive/implementation-roadmap.md) - 12 milestones of workflow library development
- [Deferred Features](./deferred-features.md) - Features deferred from MVP with workarounds

### External References

- [Wolverine Documentation](https://wolverine.netlify.app/) - Saga orchestration framework
- [Marten Documentation](https://martendb.io/) - Event sourcing and document database
- [Thompson Sampling](https://en.wikipedia.org/wiki/Thompson_sampling) - Multi-armed bandit algorithm
