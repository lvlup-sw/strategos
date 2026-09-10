# Strategos Library Roadmap

**A Unified Roadmap for MAF Integration and Workflow Library Enhancements**

**Version:** 2.0
**Status:** Approved
**Date:** 2025-12-20
**Supersedes:** `maf-integration-enhancements.md`, `workflow-library-roadmap.md`

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Current State Analysis](#current-state-analysis)
3. [Feature Roadmap](#feature-roadmap)
4. [Implementation Phases](#implementation-phases)
5. [Technical Debt](#technical-debt)
6. [References](#references)

---

## Executive Summary

This document is the **canonical roadmap** for the Strategos library, consolidating all planned enhancements for Microsoft Agent Framework integration and workflow DSL evolution.

### Design Principles

1. **MAF Alignment** - Leverage Microsoft Agent Framework patterns (`IChatClient` middleware, `TextSearchProvider`, streaming) rather than reinventing
2. **Progressive Enhancement** - Each feature is independently deployable; no big-bang releases
3. **Event Sourcing First** - All agent decisions captured as immutable events for replay and audit
4. **Contributor Focus** - This document targets library contributors; consumer utility is explained for each feature

### Package Boundaries

**Public Library Packages (Published to NuGet):**

| Package | Purpose |
|---------|---------|
| `Strategos` | Core DSL, abstractions, step contracts |
| `Strategos.Infrastructure` | Thompson Sampling, loop detection, budget enforcement |
| `Strategos.Generators` | Roslyn source generators for saga artifacts |
| `Strategos.Agents` | MAF integration, specialist abstractions, streaming |
| `Strategos.Rag` | Vector store adapters (NEW - planned) |

**Consumer Responsibility (NOT part of library):**
- Concrete DI registration and middleware wiring
- Application-specific service implementations
- Infrastructure integration (databases, queues, etc.)

> **Note:** This roadmap focuses exclusively on the public library packages. Consumer applications implement their own wiring using the library's abstractions and extension methods.

### Feature Summary (6 Features)

| ID | Feature | Consumer Benefit | Effort |
|----|---------|------------------|--------|
| F1 | RAG Integration | Automatic knowledge injection | 3-4 days |
| F2 | Streaming Responses | Real-time UI feedback | 2-3 days |
| F3 | Conversation Replay | Debug agent decisions | 1-2 days |
| F4 | Context Assembly DSL | Declarative context specification | 5-7 days |
| F5 | Conversation History DSL | Automatic token management | 3-5 days |
| F6 | Step-Scoped Failure Handlers | Precise error recovery | 3-4 days |

**Total Estimated Effort:** 17-25 days

---

## Current State Analysis

### Implemented Foundation (Library Packages Only)

| Package | Component | Purpose |
|---------|-----------|---------|
| **Strategos** | `IWorkflowStep<TState>` | Step execution contract |
| | `IWorkflowBuilder<TState>` | Fluent DSL entry point |
| | `[Append]`, `[Merge]` attributes | State reducer semantics |
| **Strategos.Agents** | `IAgentStep<TState>` | LLM-powered step contract |
| | `SpecialistAgent` | Base class with HSM state machine |
| | `StreamingExecutionMode` | Buffered vs streaming enum |
| | `StreamingResponseHandler` | Token aggregation and event emission |
| | `ChatMessageRecorded` | LLM interaction audit event |
| | `StreamingTokenReceived` | Per-token streaming event |
| | `IConversationThreadManager` | Per-specialist thread management |
| **Strategos.Infrastructure** | `IAgentSelector` | Thompson Sampling agent selection |
| | `ILoopDetector` | Stuck workflow detection |
| | `IBudgetGuard` | Resource budget enforcement |

### Identified Gaps

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CURRENT STATE                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│  ✅ IAgentStep<TState> contract defined in Strategos.Agents          │
│  ✅ StreamingResponseHandler exists but not integrated                       │
│  ✅ ChatMessageRecorded events captured in workflows                         │
│  ⚠️  No IVectorSearchAdapter abstraction in library                         │
│  ⚠️  No RagContextMiddleware in library (consumer responsibility)           │
│  ⚠️  Streaming NOT connected in SpecialistAgent base class                  │
│  ⚠️  Events stored but NO projection for conversation reconstruction        │
│  ⚠️  Context assembly is MANUAL in each step implementation                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Feature Roadmap

### F1: RAG Integration (Merged Feature)

**Consumer Benefit:**
> *"My specialists automatically receive relevant context from my knowledge base. I just configure the vector store and the framework handles context injection, formatting, and audit capture."*

**What This Enables:**
- Knowledge-grounded agent responses without manual context assembly
- Automatic citation generation for compliance
- Graceful degradation when search fails
- Pluggable vector store backends (Pinecone, Azure AI Search, Qdrant, pgvector)

**Architecture:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      RAG Integration Architecture                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   SpecialistAgent                                                            │
│        │                                                                     │
│        ▼                                                                     │
│   IChatClient Pipeline                                                       │
│        │                                                                     │
│        ├──► RagContextMiddleware ──► IVectorSearchAdapter ──► Vector Store  │
│        │         │                                                          │
│        │         └──► FormatContextForPrompt() ──► System Message           │
│        │                                                                     │
│        ├──► WorkflowContextMiddleware                                        │
│        ├──► BudgetEnforcementMiddleware                                      │
│        └──► OpenTelemetry                                                    │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**MAF Alignment:**
- Mirrors `TextSearchProvider` with `BeforeAIInvoke` behavior
- `RagSpecialistConfiguration` maps to `TextSearchProviderOptions`
- `SearchAdapter` function signature compatible with MAF pattern

**Library Integration Points:**
| Package | File | Change |
|---------|------|--------|
| `Strategos.Agents` | `Contracts/IVectorSearchAdapter.cs` | NEW: Vector search abstraction |
| `Strategos.Agents` | `Middleware/RagContextMiddleware.cs` | NEW: IChatClient middleware |
| `Strategos.Agents` | `Configuration/RagConfiguration.cs` | NEW: Options for RAG behavior |
| `Strategos.Rag` | `Adapters/*.cs` | NEW: Vector store implementations |

> **Consumer Wiring:** Consumers register middleware via `chatClientBuilder.UseRagContext(...)` in their DI setup.

**Vector Store Adapters (New Package: `Strategos.Rag`):**
| Adapter | Priority | Notes |
|---------|----------|-------|
| `AzureAISearchAdapter` | High | Enterprise standard |
| `PgVectorAdapter` | High | Already have Marten/PostgreSQL |
| `QdrantAdapter` | Medium | Popular open-source option |
| `PineconeAdapter` | Medium | Managed cloud option |
| `InMemoryAdapter` | Complete | For testing |

---

### F2: Streaming Response Support

**Consumer Benefit:**
> *"Users see tokens appear in real-time during code generation. No more frozen UIs during long LLM calls."*

**What This Enables:**
- Real-time token display in UI via consumer-provided callback
- Early cancellation of poor responses
- Progress indication for long-running specialists
- Event emission for downstream processing (SSE, WebSockets, etc.)

**Architecture:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Streaming Response Flow                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   SpecialistAgent.GenerateCodeAsync()                                        │
│        │                                                                     │
│        ├── ExecutionMode == Buffered ──► GetResponseAsync() ──► Full Text   │
│        │                                                                     │
│        └── ExecutionMode == Streaming                                        │
│                 │                                                            │
│                 ▼                                                            │
│        GetStreamingResponseAsync()                                           │
│                 │                                                            │
│                 ▼                                                            │
│        IAsyncEnumerable<ChatResponseUpdate>                                  │
│                 │                                                            │
│                 ├──► StreamingResponseHandler.ProcessStreamingResponseAsync()│
│                 │         │                                                  │
│                 │         ├──► Append token to StringBuilder                 │
│                 │         ├──► Emit StreamingTokenReceived event             │
│                 │         └──► Return aggregated response                    │
│                 │                                                            │
│                 └──► IStreamingCallback (consumer-provided)                  │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**MAF Alignment:**
- MAF's `RunStreamingAsync` returns `AgentRunResponseUpdate`
- Our `ChatResponseUpdate` via `Microsoft.Extensions.AI` is compatible
- Both use `IAsyncEnumerable` for token streaming

**Library Integration Points:**
| Package | File | Change |
|---------|------|--------|
| `Strategos.Agents` | `Agents/SpecialistAgent.cs` | Add streaming path in `GenerateCodeAsync()` |
| `Strategos.Agents` | `Services/StreamingResponseHandler.cs` | Already implemented |
| `Strategos.Agents` | `Events/StreamingTokenReceived.cs` | Already defined |
| `Strategos.Agents` | `Extensions/ServiceCollectionExtensions.cs` | NEW: `AddWorkflowAgents()` registers handler |

> **Consumer Wiring:** Consumers call `services.AddWorkflowAgents()` and implement `IStreamingCallback` for UI updates.

---

### F3: Conversation Replay

**Consumer Benefit:**
> *"I can see exactly what the agent saw and said for any workflow execution. Time-travel debugging for AI decisions."*

**What This Enables:**
- Full conversation reconstruction from events
- Markdown export for audit/compliance
- Context injection for conversation continuation
- Time-travel debugging ("what did the agent see at step 5?")

**Architecture:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Conversation Replay Architecture                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   LIBRARY (Strategos.Agents)                                          │
│   ─────────────────────────────────                                          │
│   ChatMessageRecorded { WorkflowId, TaskId, Specialist, Role, Content }      │
│        │                                                                     │
│        ▼                                                                     │
│   ConversationReplayBuilder                                                  │
│        │                                                                     │
│        ├── FromEvents(IEnumerable<ChatMessageRecorded>)                      │
│        ├── ToMafMessages() ──► List<ChatMessage>                             │
│        └── ExportToMarkdown() ──► string                                     │
│                                                                              │
│   IConversationHistoryService (abstraction)                                  │
│        │                                                                     │
│        ├── GetConversationAsync(workflowId, specialist, taskId)             │
│        └── GetWorkflowConversationsAsync(workflowId)                         │
│                                                                              │
│   CONSUMER (e.g., with Marten)                                               │
│   ────────────────────────────                                               │
│   ConversationHistoryService : IConversationHistoryService                   │
│        │                                                                     │
│        └── Queries event store, uses ConversationReplayBuilder               │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**MAF Alignment:**
- Reconstructed conversations can be injected into MAF `AgentThread`
- `ToMafMessages()` converts to `ChatMessage` list for context injection
- Enables "resume from checkpoint" patterns

**Library Integration Points:**
| Package | File | Change |
|---------|------|--------|
| `Strategos.Agents` | `Events/ChatMessageRecorded.cs` | Already defined |
| `Strategos.Agents` | `Contracts/IConversationHistoryService.cs` | NEW: Service abstraction |
| `Strategos.Agents` | `Models/ConversationMessage.cs` | NEW: Message record |
| `Strategos.Agents` | `Models/ConversationReadModel.cs` | NEW: Read model contract |
| `Strategos.Agents` | `Helpers/ConversationReplayBuilder.cs` | NEW: Reconstructs `List<ChatMessage>` from events |

> **Consumer Wiring:** Consumers implement `IConversationHistoryService` using their event store (Marten, EventStoreDB, etc.). The library provides `ConversationReplayBuilder` to reconstruct conversations from raw events.

---

### F4: Context Assembly DSL

**Consumer Benefit:**
> *"I declare what context my step needs and the framework assembles it. The assembled context is automatically captured for audit."*

**What This Enables:**
- Declarative context specification in workflow DSL
- Automatic context capture (what the agent saw)
- Token-aware context truncation
- Composition of state, retrieval, and literal sources

**DSL Design:**

```csharp
Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<AnalyzeOrder>(step => step
        .WithContext(ctx => ctx
            .FromState(s => s.Order)
            .FromState(s => $"Customer: {s.CustomerName}")
            .FromRetrieval<PolicyDocs>(r => r
                .Query(s => s.Order.Category)
                .TopK(5)
                .MinRelevance(0.75m))
            .FromLiteral("Follow company guidelines.")
            .WithMaxTokens(8000)))
    .Finally<Confirm>();
```

**Architecture:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Context Assembly Pipeline                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   DSL Definition                                                             │
│        │                                                                     │
│        ▼                                                                     │
│   Source Generator (ContextAssemblyEmitter)                                  │
│        │                                                                     │
│        ├── Parses .WithContext() expressions                                 │
│        ├── Generates IContextAssembler<TState> implementation                │
│        └── Wires into saga handler                                           │
│                                                                              │
│   Runtime Execution                                                          │
│        │                                                                     │
│        ▼                                                                     │
│   ContextBuilder.AssembleAsync(state)                                        │
│        │                                                                     │
│        ├── StateContextSource.GetContent(state)                              │
│        ├── RetrievalContextSource.GetContent(state, vectorSearch)            │
│        ├── LiteralContextSource.GetContent()                                 │
│        └── TokenCounter.Truncate(assembled, maxTokens)                       │
│                                                                              │
│   Event Emission                                                             │
│        │                                                                     │
│        └── ContextAssembled { StepName, Sources[], TokenCount, Timestamp }   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Library Integration Points:**
| Package | File | Change |
|---------|------|--------|
| `Strategos` | `Builders/StepBuilder.cs` | Add `.WithContext()` method |
| `Strategos` | `Context/IContextBuilder.cs` | Already defined |
| `Strategos` | `Context/ContextBuilder.cs` | Already implemented |
| `Strategos.Generators` | `Emitters/ContextAssemblyEmitter.cs` | NEW: Generator |
| `Strategos` | `Events/ContextAssembled.cs` | NEW: Audit event |

---

### F5: Conversation History DSL

**Consumer Benefit:**
> *"The framework manages my conversation history automatically. It windows and summarizes so I never exceed token limits."*

**What This Enables:**
- Automatic token counting per LLM provider
- Configurable windowing strategies (sliding, fixed, adaptive)
- Optional summarization for compressed context
- Preservation heuristics (keep decisions, facts, sentiment shifts)

**DSL Design:**

```csharp
// Attribute-based approach
[WorkflowState]
public record ConversationalState
{
    [ConversationHistory(MaxTokens = 8000, SummarizeAfter = 20)]
    public ImmutableList<ChatMessage> Messages { get; init; } = [];
}

// Or builder-based approach
.Then<ChatStep>(step => step
    .WithConversationHistory(h => h
        .MaxTokens(8000)
        .WindowStrategy(WindowStrategy.SlidingWithSummary)
        .PreserveDecisions()
        .SummarizeWith<GptSummarizer>()))
```

**Architecture:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Conversation History Management                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Workflow State                                                             │
│        │                                                                     │
│        └── Messages: ImmutableList<ChatMessage> with [ConversationHistory]   │
│                                                                              │
│   ConversationHistoryManager                                                 │
│        │                                                                     │
│        ├── TokenCounter (provider-specific: tiktoken, etc.)                  │
│        │                                                                     │
│        ├── WindowingStrategy                                                 │
│        │    ├── SlidingWindow (keep last N messages)                         │
│        │    ├── FixedWindow (keep first + last N)                            │
│        │    └── AdaptiveWindow (budget-aware)                                │
│        │                                                                     │
│        ├── Summarizer (optional)                                             │
│        │    ├── LlmSummarizer (call LLM for compression)                     │
│        │    └── ExtractiveSummarizer (keyword extraction)                    │
│        │                                                                     │
│        └── PreservationHeuristics                                            │
│             ├── PreserveDecisions (agent conclusions)                        │
│             ├── PreserveFacts (extracted entities)                           │
│             └── PreserveSentiment (emotional turns)                          │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**MAF Alignment:**
- MAF's `RecentMessageMemoryLimit` is simple truncation
- Our approach is more sophisticated with summarization
- Can interop by converting managed history to MAF messages

**Library Integration Points:**
| Package | File | Change |
|---------|------|--------|
| `Strategos` | `Attributes/ConversationHistoryAttribute.cs` | NEW |
| `Strategos` | `Context/IConversationHistoryManager.cs` | NEW |
| `Strategos` | `Context/WindowingStrategy.cs` | NEW: Enum + implementations |
| `Strategos` | `Context/ITokenCounter.cs` | NEW: Provider abstraction |
| `Strategos.Infrastructure` | `Context/TiktokenCounter.cs` | NEW: OpenAI impl |

---

### F6: Step-Scoped Failure Handlers

**Consumer Benefit:**
> *"Each step can have its own error handling. Payment failures trigger refunds; other failures just log."*

**What This Enables:**
- Per-step exception routing by exception type
- Automatic compensation ordering (reverse of execution)
- Exception context preservation for debugging
- Conditional failure paths based on exception type

**DSL Design:**

```csharp
Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ChargePayment>(step => step
        .Compensate<RefundPayment>()
        .OnFailure(f => f
            .When<PaymentDeclinedException>().Then<NotifyPaymentDeclined>()
            .When<PaymentTimeoutException>().Then<RetryPayment>()
            .Otherwise().Then<LogGenericPaymentError>())))
    .Then<ShipOrder>()
    .Finally<Confirm>()
    .OnFailure(flow => flow.Then<NotifyFailure>()); // Workflow-level fallback
```

**Architecture:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Step-Scoped Failure Handling                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   DSL Parsing                                                                │
│        │                                                                     │
│        └── FailureHandlerExtractor parses step-level .OnFailure()           │
│                                                                              │
│   Generated Saga Handler                                                     │
│        │                                                                     │
│        ├── try { await step.ExecuteAsync(state, ct); }                       │
│        │                                                                     │
│        └── catch (Exception ex)                                              │
│             │                                                                │
│             ├── Match exception type to .When<T>() handlers                  │
│             │    └── Route to specified handler step                         │
│             │                                                                │
│             ├── If no match, use .Otherwise() handler                        │
│             │                                                                │
│             └── If no step handler, bubble to workflow .OnFailure()          │
│                                                                              │
│   Compensation Execution                                                     │
│        │                                                                     │
│        └── After failure handling, run compensations in reverse order        │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Library Integration Points:**
| Package | File | Change |
|---------|------|--------|
| `Strategos` | `Builders/StepBuilder.cs` | Add `.OnFailure()` method |
| `Strategos` | `Definitions/StepFailureHandlerDefinition.cs` | NEW |
| `Strategos.Generators` | `Helpers/FailureHandlerExtractor.cs` | Extend for step scope |
| `Strategos.Generators` | `Emitters/Saga/SagaStepHandlersEmitter.cs` | Add try/catch generation |

---

## Implementation Phases

### Phase 1: MAF Integration Completion (6-9 days)

**Goal:** Complete MAF integration abstractions and streaming support in library packages.

| Week | Feature | Deliverable |
|------|---------|-------------|
| 1 | F1: RAG Integration | `IVectorSearchAdapter`, `RagContextMiddleware` in Strategos.Agents |
| 1 | F2: Streaming | `SpecialistAgent` streaming path, `IStreamingCallback` abstraction |
| 2 | F3: Conversation Replay | `ConversationReplayBuilder`, `IConversationHistoryService` abstraction |
| 2 | F1: RAG Adapters | `PgVectorAdapter`, `AzureAISearchAdapter` in Strategos.Rag |

### Phase 2: DSL Evolution (8-12 days)

**Goal:** Declarative patterns for context and history management.

| Week | Feature | Deliverable |
|------|---------|-------------|
| 3-4 | F4: Context Assembly DSL | `.WithContext()` DSL, generator, audit event |
| 5 | F5: Conversation History DSL | Attribute, manager, windowing strategies |

### Phase 3: Error Handling (3-4 days)

**Goal:** Per-step failure handling for precise error recovery.

| Week | Feature | Deliverable |
|------|---------|-------------|
| 6 | F6: Step-Scoped Failure | `.OnFailure()` DSL, exception routing |

---

## Dependency Graph

```
F1 (RAG Integration) ────────────────────────────────────────────────►
     │                                                                │
     ├── Adds IVectorSearchAdapter and RagContextMiddleware           │
     └── Adds vector store adapters in Strategos.Rag           │
                                                                      │
F4 (Context Assembly DSL) ────────────────────────────────────────────┤
     │                                                                │
     └── Depends on F1 for .FromRetrieval() sources                   │
                                                                      │
F2 (Streaming) ───────────────────────────────────────────────────────┤
     │                                                                │
     └── Independent, no dependencies                                 │
                                                                      │
F3 (Conversation Replay) ─────────────────────────────────────────────┤
     │                                                                │
     └── Independent, enables F5 history source                       │
                                                                      │
F5 (Conversation History DSL) ────────────────────────────────────────┤
     │                                                                │
     └── Can leverage F3 for history retrieval                        │
                                                                      │
F6 (Step-Scoped Failure) ─────────────────────────────────────────────┘
     │
     └── Independent, extends existing OnFailure pattern
```

---

## Technical Debt

Items to address before or during feature work:

| Item | Priority | Feature Impact |
|------|----------|----------------|
| Generated code edge cases | High | All generator features |
| Source generator caching | Medium | Build performance |
| XML doc coverage for public APIs | Medium | Developer experience |
| Integration test coverage | High | All features need E2E tests |

---

## References

### Internal Documentation
- [Agentic Workflow Library Design](../design.md) - Core DSL design
- [Agentic Workflow Theory](../theory/strategos-theory.md) - Formal framework

### External Documentation
- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)
- [Agent Middleware Guide](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-middleware)
- [Agent RAG Integration](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-rag)
- [Marten Event Projections](https://martendb.io/events/projections/)

### Superseded Documents
- `maf-integration-enhancements.md` - Archived (from original agentic-engine repo)
- `workflow-library-roadmap.md` - Archived (from original agentic-engine repo)
