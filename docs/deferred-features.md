# Deferred Features Analysis

**Document Version:** 1.0
**Analysis Date:** 2025-12-05
**Reference:** [Design Document](./design.md)

---

## Executive Summary

This document catalogs all features from the Strategos design specification that were intentionally deferred from the initial implementation. Each deferral is analyzed for its rationale, impact, and recommended path forward.

> **Status update — 2026-06-18 (epic #135, _step resilience lowering_).** Step-level
> resilience is **now lowered into the emitted Wolverine + Marten saga and proven on a
> real host** — superseding the parts of this analysis that described it as deferred or
> only "declared." What now lowers end-to-end (each with a behavioral test that runs a
> generated saga against a real Postgres):
> - **`WithRetry`** → a per-handler Wolverine error policy (`Configure(HandlerChain)` → `OnAnyException().RetryTimes`/`RetryWithCooldown`).
> - **`WithTimeout`** → a saga `TimeoutMessage` deadline race (idempotent guard). *Durable cross-restart delivery requires the Marten transactional outbox — `AddMarten(…).IntegrateWithWolverine()` — otherwise scheduled/timeout delivery is in-memory only.*
> - **`Compensate<T>`** → the worker's `Configure` chain publishes the (previously orphaned) `Trigger…FailureHandlerCommand` after retries exhaust, and a dedicated saga compensation chain runs the rollback step → terminal `Failed`.
> - **`RequireConfidence` / `OnLowConfidence`** → saga routing on the runtime confidence score.
> - **`WithContext`** → the (previously dead) `ContextAssemblerEmitter` is wired in; a `.WithContext` step assembles **ontology-backed** context (`IObjectSetProvider.ExecuteSimilarityAsync`, no `Strategos.Rag`).
> - **Expressibility:** `Then<TStep>(configure)` now also exists on the **branch** and **failure-handler** builders (fork landed earlier via #134).
> - **Diagnostics:** invalid resilience config now reports `AGWF017`–`AGWF021`.
>
> **Known boundaries (carried as follow-ons, not regressions):** only a **single-step,
> terminating** `OnLowConfidence` handler is lowered (multi-step chains + rejoin-to-main
> are not yet); **workflow-level `OnFailure` handler-chain interop with `Compensate<T>` is
> deferred** (see §2.1 — the workflow-level failure-handler chain is independently
> non-functional); resilience config is attachable only via `.Then<TStep>(s => …)` (not
> `StartWith`/`Finally`); and a `[Workflow("name")]` name must PascalCase to its
> partial-class name. See `docs/designs/2026-06-17-step-resilience-lowering.md`.

**Total Deferred Features:** 8
**Deferral Categories:**
- Agent-Specific Patterns (4 features)
- Generator Enhancements (2 features)
- Consumer-Responsibility Features (2 features)

---

## Deferral Decision Framework

Features were deferred based on one or more of the following criteria:

| Criterion | Description |
|-----------|-------------|
| **Domain-Specific** | Implementation varies significantly by use case; no single solution fits all |
| **External Dependency** | Requires integration with third-party systems not under library control |
| **Consumer Responsibility** | Better implemented at the application layer with domain knowledge |
| **Technical Limitation** | Source generators cannot access runtime-only constructs |
| **Low Priority** | Value-to-effort ratio does not justify inclusion in MVP |

---

## Category 1: Agent-Specific Patterns

These features from the design document relate to AI agent workflows specifically.

### 1.1 AgentStep Base Class

**Design Spec Reference:** Pattern 3: Agent Steps (LLM-Specific)

**Intended Functionality:**
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

**Deferral Rationale:**
- **Too Opinionated:** LLM providers (OpenAI, Anthropic, Azure OpenAI, local models) have incompatible APIs
- **Rapidly Evolving:** Agent frameworks (Semantic Kernel, AutoGen, LangChain) are in flux; premature abstraction creates technical debt
- **Configuration Complexity:** Model selection, prompt templating, output parsing, retry strategies vary by use case

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Developer Experience | Medium | Consumers must implement `IWorkflowStep<T>` manually for agent steps |
| Boilerplate | Medium | ~20-30 lines per agent step vs ~10 lines with base class |
| Flexibility | Positive | Full control over LLM integration patterns |

**Workaround:**
```csharp
// Consumers implement their own base class tailored to their LLM provider
public abstract class MyAgentStep<TState> : IWorkflowStep<TState>
    where TState : class, IWorkflowState
{
    protected abstract string GetPrompt(TState state);
    protected abstract TState ApplyResult(TState state, string llmResponse);

    public async Task<StepResult<TState>> ExecuteAsync(
        TState state, StepContext context, CancellationToken ct)
    {
        var prompt = GetPrompt(state);
        var response = await _llmClient.CompleteAsync(prompt, ct);
        var newState = ApplyResult(state, response);
        return StepResult<TState>.Success(newState);
    }
}
```

**Recommendation:** Provide sample implementations in documentation for common providers (OpenAI, Semantic Kernel) rather than a rigid base class.

---

### 1.2 Context Assembly

**Design Spec Reference:** Context Assembly

**Intended Functionality:**
```csharp
.Then<AnalyzeDocument>(step => step
    .WithContext(ctx => ctx
        .FromState(state => state.Document)
        .FromRetrieval<KnowledgeBase>(r => r
            .Query(state => state.SearchQuery)
            .TopK(10)
            .MinRelevance(0.7m))
        .FromHistory(h => h
            .LastN(20)
            .MaxTokens(4000))))
```

**Deferral Rationale:**
- **Domain-Specific:** Context requirements vary dramatically (legal docs vs code vs customer support)
- **External Dependencies:** Retrieval sources (Pinecone, Qdrant, pgvector, Azure AI Search) each have unique APIs
- **Complex Serialization:** Context capture for audit trails requires domain-specific formatting

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Auditability | Medium | Consumers must manually capture assembled context in events |
| RAG Integration | High | No built-in retrieval-augmented generation support |
| Prompt Engineering | Neutral | Consumers have full control over prompt construction |

**Workaround:**
```csharp
public class AnalyzeDocument : IWorkflowStep<DocState>
{
    private readonly IKnowledgeBase _kb;
    private readonly IDocumentSession _session;

    public async Task<StepResult<DocState>> ExecuteAsync(
        DocState state, StepContext context, CancellationToken ct)
    {
        // Manual context assembly
        var retrievedDocs = await _kb.SearchAsync(state.Query, topK: 10, ct);
        var assembledContext = new AssembledContext(state.Document, retrievedDocs);

        // Capture for audit
        _session.Events.Append(context.WorkflowId,
            new ContextAssembled(context.StepName, assembledContext, DateTimeOffset.UtcNow));

        // Use context in LLM call
        var result = await _llm.AnalyzeAsync(assembledContext, ct);
        return StepResult<DocState>.Success(state with { Analysis = result });
    }
}
```

**Recommendation:** Provide a `ContextBuilder` utility class and `IContextCapture` interface as optional helpers, not integrated into the core DSL.

---

### 1.3 RAG Integration

**Design Spec Reference:** Retrieval-Augmented Generation (RAG)

**Intended Functionality:**
```csharp
.FromRetrieval<CompanyDocs>(r => r
    .Query(state => state.Question)
    .TopK(10)
    .MinRelevance(0.75m)
    .WithHybridSearch(vectorWeight: 0.7m)
    .WithReranking<CohereReranker>())
```

**Deferral Rationale:**
- **External Dependency:** Vector databases and rerankers are external services
- **Vendor Lock-in Risk:** Deep integration with specific providers limits portability
- **Configuration Complexity:** Embedding models, chunk strategies, and retrieval parameters are highly domain-specific

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Development Velocity | Medium | No turnkey RAG; consumers implement retrieval manually |
| Flexibility | Positive | Freedom to use any vector store or hybrid search approach |
| Testing | Positive | No mocking complex retrieval abstractions |

**Workaround:**
```csharp
// Register retrieval as a standard dependency
services.AddSingleton<IRetrievalSource, PineconeRetrievalSource>();

// Use in steps via DI
public class ResearchStep(IRetrievalSource retrieval) : IWorkflowStep<ResearchState>
{
    public async Task<StepResult<ResearchState>> ExecuteAsync(...)
    {
        var docs = await retrieval.SearchAsync(state.Query, ct);
        return StepResult<ResearchState>.Success(state with { Sources = docs });
    }
}
```

**Recommendation:** Create a separate `Strategos.Rag` package with adapters for common vector stores, keeping the core library dependency-free.

---

### 1.4 Conversation History Management

**Design Spec Reference:** Conversation History Management

**Intended Functionality:**
```csharp
[ConversationHistory(MaxTokens = 8000, SummarizeAfter = 20)]
public ConversationHistory Messages { get; init; }

.FromHistory(h => h
    .LastN(20)
    .MaxTokens(4000)
    .SummarizeOlder(sum => sum
        .PreserveFacts()
        .PreserveDecisions()))
```

**Deferral Rationale:**
- **Domain-Specific:** Summarization strategies depend on conversation type (support, sales, technical)
- **Token Counting:** Varies by model (GPT-4, Claude, Llama have different tokenizers)
- **LLM Dependency:** Summarization requires LLM calls, adding cost and latency

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Long Conversations | Medium | Consumers must implement windowing/summarization |
| Token Optimization | Medium | No automatic context window management |
| State Size | Low | Standard `[Append]` attribute handles message accumulation |

**Workaround:**
```csharp
[WorkflowState]
public record ChatState : IWorkflowState
{
    public Guid WorkflowId { get; init; }

    [Append]
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    public string? ConversationSummary { get; init; }
}

// Periodic summarization step
public class SummarizeHistory : IWorkflowStep<ChatState>
{
    public async Task<StepResult<ChatState>> ExecuteAsync(ChatState state, ...)
    {
        if (state.Messages.Count <= 20)
            return StepResult<ChatState>.Success(state);

        var summary = await _llm.SummarizeAsync(state.Messages.Take(15), ct);
        var recentMessages = state.Messages.Skip(15).ToList();

        return StepResult<ChatState>.Success(state with
        {
            Messages = recentMessages,
            ConversationSummary = summary
        });
    }
}
```

**Recommendation:** Provide a `ConversationManager` utility class with pluggable summarization strategies.

---

## Category 2: Generator Enhancements

These features relate to source generator capabilities.

### 2.1 OnFailure Saga Emitters

**Design Spec Reference:** Error Handling and Compensation

**Current State (updated #135):**
- Runtime DSL: `OnFailure(flow => flow.Then<NotifyFailure>())` works
- **Step-level `Compensate<T>()` now lowers and RUNS** (#135): retries exhaust → the worker's `Configure` chain publishes `Trigger…FailureHandlerCommand` → a dedicated saga compensation chain runs the rollback step → terminal `Failed`. Proven on a real host.
- **Still deferred:** the *workflow-level* `OnFailure` handler **chain** — #135 found it independently non-functional (its generated `ExecuteFailureHandler…WorkerCommand` has no worker handler). `Compensate<T>` deliberately routes through a separate, working compensation chain; the dedicated emitter no-ops when a workflow-level failure handler is present (to avoid a duplicate `Handle(Trigger…)` overload). Wiring the workflow-level chain + its interop with `Compensate<T>` is the remaining work here.

**Deferral Rationale (workflow-level OnFailure chain):**
- **Infrastructure Complexity:** Failure handlers require exception capture, context preservation, and saga state coordination
- **Partial Implementation Risk:** Incomplete failure handling is worse than no handling
- **Priority:** Fork/Join and Approval patterns prioritized for MVP

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Error Recovery | Medium | Workflow-level `OnFailure` *handler chain* still routes to `Failed` without running its handler steps |
| Compensation | **Resolved** (#135) | Step-level `Compensate<T>()` now lowers + runs the rollback on a real saga |

**Workaround:**
```csharp
// Use partial class extension for custom failure handling
public partial class OrderProcessingSaga
{
    // Override generated handler in partial class
    public async Task HandleFailure(WorkflowFailedEvent evt, IDocumentSession session)
    {
        await _notificationService.NotifyAsync(evt.Exception, CancellationToken.None);
        session.Events.Append(WorkflowId, new FailureNotified(WorkflowId, DateTimeOffset.UtcNow));
    }
}
```

**Recommendation:** Implement in next milestone; infrastructure foundation exists.

---

### 2.2 Lambda Step Generators

**Design Spec Reference:** Pattern 2: Inline Lambda

**Current State:**
- Runtime DSL: `Then("log-entry", (state, ctx) => state.With(s => s.Started, ctx.Timestamp))` works
- Generator: Lambda steps not visible in syntax trees

**Deferral Rationale:**
- **Technical Limitation:** Source generators analyze syntax trees; lambda bodies are opaque at compile time
- **Type Safety:** Cannot extract `TState` updates from arbitrary lambda expressions
- **Runtime-Only by Nature:** Lambdas capture closures that don't exist at generation time

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Worker Handlers | None | Lambda steps execute inline in saga; no worker needed |
| DI Registration | None | No type to register |
| Diagnostics | Low | Lambda step names appear in phases but without detailed type info |

**Workaround:** None needed—lambda steps work correctly at runtime. They're intentionally lightweight and don't require generation.

**Recommendation:** Document as expected behavior; no generator support needed.

---

## Category 3: Consumer-Responsibility Features

These features are better implemented at the application layer.

### 3.1 Marten Projections

**Design Spec Reference:** Projections and Read Models

**Intended Functionality:**
```csharp
// Auto-generated projection
public class ProcessClaimProjection : SingleStreamProjection<ProcessClaimReadModel>
{
    public ProcessClaimReadModel Create(ProcessClaimStarted evt) => new() { ... };
    public void Apply(ProcessClaimPhaseChanged evt, ProcessClaimReadModel model) { ... }
}
```

**Deferral Rationale:**
- **Domain-Specific:** Read model shapes depend on query patterns
- **Over-Generation Risk:** Generic projections rarely match actual query needs
- **Marten Expertise Required:** Projection strategies (inline, async, live) depend on scale and consistency requirements

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| Query Support | Medium | Consumers must create projections for workflow queries |
| Boilerplate | Medium | ~50-100 lines per projection |
| Flexibility | Positive | Full control over read model design |

**Workaround:**
```csharp
// Follow existing pattern from ProgressLedgerProjection
public class ClaimWorkflowProjection : SingleStreamProjection<ClaimWorkflowReadModel>
{
    public ClaimWorkflowReadModel Create(ClaimWorkflowStarted evt) => new()
    {
        WorkflowId = evt.WorkflowId,
        ClaimNumber = evt.InitialState.ClaimNumber,
        CurrentPhase = ClaimWorkflowPhase.NotStarted,
        StartedAt = evt.Timestamp
    };

    public void Apply(ClaimWorkflowPhaseChanged evt, ClaimWorkflowReadModel model)
    {
        model.CurrentPhase = evt.ToPhase;
        model.LastTransitionAt = evt.Timestamp;
    }
}
```

**Recommendation:** Provide projection templates and documentation; do not auto-generate.

---

### 3.2 Agent Versioning Tracking

**Design Spec Reference:** Agent Versioning

**Intended Functionality:**
```csharp
AgentDecisionEvent {
    AgentId: "claim-assessor",
    AgentVersion: "v2.3.1",
    ModelUsed: "gpt-4o",
    Decision: { ... },
    Confidence: 0.92,
    TokensUsed: 1247
}
```

**Deferral Rationale:**
- **No Agent Abstraction:** Without `AgentStep`, there's no hook for version tracking
- **Provider-Specific:** Token counting and model metadata vary by LLM provider
- **Application Concern:** Version management is a deployment/operations concern

**Impact:**
| Aspect | Impact Level | Description |
|--------|--------------|-------------|
| A/B Testing | Medium | Consumers must track agent versions manually |
| Cost Attribution | Medium | Token usage must be captured in custom events |
| Debugging | Low | Workflow events still capture all state changes |

**Workaround:**
```csharp
public record AgentExecutionEvent(
    Guid WorkflowId,
    string StepName,
    string AgentId,
    string AgentVersion,
    string ModelUsed,
    int InputTokens,
    int OutputTokens,
    decimal Confidence,
    DateTimeOffset Timestamp) : IProgressEvent;

// Emit from agent steps
public class AssessClaimStep : IWorkflowStep<ClaimState>
{
    public async Task<StepResult<ClaimState>> ExecuteAsync(...)
    {
        var result = await _agent.ExecuteAsync(state, ct);

        _session.Events.Append(context.WorkflowId, new AgentExecutionEvent(
            context.WorkflowId,
            context.StepName,
            AgentId: "claim-assessor",
            AgentVersion: _agentConfig.Version,
            ModelUsed: result.Model,
            InputTokens: result.Usage.InputTokens,
            OutputTokens: result.Usage.OutputTokens,
            Confidence: result.Confidence,
            Timestamp: DateTimeOffset.UtcNow));

        return StepResult<ClaimState>.Success(state with { Assessment = result.Output });
    }
}
```

**Recommendation:** Define standard event schemas in documentation for consumers to adopt.

---

## Implementation Priority Matrix

| Feature | Effort | Value | Priority | Recommendation |
|---------|--------|-------|----------|----------------|
| OnFailure Emitters | Medium | High | **P1** | Next milestone |
| AgentStep Base | High | Medium | P3 | Provide samples |
| Context Assembly | Medium | Medium | P3 | Utility package |
| RAG Integration | High | Medium | P3 | Separate package |
| Conversation History | Medium | Low | P4 | Utility class |
| Projections | Low | Low | P4 | Documentation |
| Agent Versioning | Low | Low | P4 | Event schemas |
| Lambda Generators | N/A | N/A | N/A | Not applicable |

---

## Future Roadmap

### Milestone 16 (Recommended)
- **OnFailure Saga Emitters** - Complete failure handler generation
- **Exception Context Capture** - Preserve stack traces and step context in failure events

### Milestone 17+ (Optional)
- **Strategos.Agents** package - Provider-agnostic agent base classes
- **Strategos.Rag** package - Vector store adapters
- **Context Builder** utility - Declarative context assembly helpers

### Not Planned
- Lambda step generation (technical limitation)
- Auto-generated projections (too opinionated)
- Built-in token counting (provider-specific)

---

## Conclusion

The deferred features represent deliberate architectural decisions prioritizing:

1. **Flexibility over Convenience** - Consumers retain full control over LLM integration
2. **Core Stability** - No premature abstractions that may require breaking changes
3. **Clean Dependencies** - Core library has no external LLM/vector store dependencies
4. **MVP Focus** - Essential workflow patterns (Fork/Join, Approvals) prioritized

All deferred features have documented workarounds using standard library patterns. The library is production-ready for its intended scope: deterministic workflow orchestration with event-sourced audit trails.

---

## Appendix: Quick Reference

### What's Implemented
- Full Fluent DSL (12 features)
- Complete source generation (9 artifacts)
- State management with reducers
- Fork/Join parallel execution
- Human-in-the-loop approvals
- Confidence-based routing
- Compensation handlers
- Infrastructure implementations

### Resolved by #135 (step resilience lowering)
- Step `WithRetry` / `WithTimeout` / `Compensate<T>` / `RequireConfidence` + `OnLowConfidence` — now lower into the saga and run on a real host
- `WithContext` — ontology-backed context assembly now lowers (Context Assembly below is resolved for the lowered path; manual assembly no longer required)

### What's Deferred (with Workarounds)
- AgentStep base class -> Implement `IWorkflowStep<T>` with custom LLM integration
- RAG Integration -> Standard DI with retrieval services
- Conversation History -> `[Append]` attribute + manual summarization
- Workflow-level OnFailure handler chain -> Partial class extension (step-level `Compensate<T>()` resolved by #135; see §2.1)
- Multi-step / rejoining `OnLowConfidence` handlers -> single-step terminating handler lowered by #135; chains/rejoin pending
- Projections -> Manual Marten projections
- Agent Versioning -> Custom events in step implementations
