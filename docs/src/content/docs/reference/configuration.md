---
title: "Configuration"
---

# Configuration

Strategos integrates with proven .NET infrastructure. This reference covers setup and configuration for each integration.

## Overview

| Integration | Purpose | Required |
|-------------|---------|----------|
| [Wolverine](https://wolverine.netlify.app/) | Saga orchestration, message routing, transactional outbox | Yes |
| [Marten](https://martendb.io/) | Event sourcing, projections, time-travel queries | Yes |
| [PostgreSQL](https://www.postgresql.org/) | Persistence for both Wolverine and Marten | Yes |
| [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/) | LLM integration via IChatClient | For AI workflows |

---

## Wolverine

Wolverine provides saga orchestration for workflow execution.

### How Workflows Become Sagas

Every workflow definition is compiled into a Wolverine saga at build time:

```text
Workflow Definition -> Source Generator -> Wolverine Saga
     (DSL)              (compile-time)      (runtime)
```

The source generator produces:
- A saga class with `[SagaIdentity]` for workflow instance tracking
- Command handlers for each step transition
- Automatic message cascading between steps

### Generated Message Types

```csharp
// Generated command for a step
public record ExecuteProcessPaymentCommand(
    [property: SagaIdentity] Guid WorkflowId);

// Handler in the saga
public async Task<ExecuteFulfillOrderCommand> Handle(
    ExecuteProcessPaymentCommand command,
    ProcessPayment step,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, ct);
    State = StateReducer.Reduce(State, result.StateUpdate);
    return new ExecuteFulfillOrderCommand(WorkflowId);
}
```

### Transactional Outbox

Wolverine's transactional outbox ensures state updates and outgoing messages are committed atomically. If a process crashes after updating state but before sending the next message, the message is recovered from the outbox on restart.

### Retry Configuration

```csharp
services.AddWolverine(opts =>
{
    opts.Handlers.OnException<HttpRequestException>()
        .RetryWithCooldown(100.Milliseconds(), 500.Milliseconds(), 1.Seconds());
});
```

---

## Marten

Marten provides event sourcing and projections.

### Event Types

| Event | When Emitted |
|-------|--------------|
| `WorkflowStarted` | Workflow instance created |
| `PhaseChanged` | Transition between phases |
| `StepCompleted` | Step finished executing |
| `BranchTaken` | Routing decision made |
| `ApprovalRequested` | Workflow paused for human input |
| `ApprovalReceived` | Human input received |
| `WorkflowCompleted` | Workflow reached terminal state |

### Appending Events

```csharp
session.Events.Append(
    workflowId,
    new PhaseChanged(workflowId, fromPhase, toPhase, timestamp));
```

### Time-Travel Queries

Reconstruct state at any point in history:

```csharp
// Get state at a specific version
var historicalState = await session.Events
    .AggregateStreamAsync<WorkflowState>(
        workflowId,
        version: 5);

// Get state at a specific timestamp
var historicalState = await session.Events
    .AggregateStreamAsync<WorkflowState>(
        workflowId,
        timestamp: specificTime);
```

### Projections

Query workflows by phase or other criteria:

```csharp
var awaitingApproval = await session
    .Query<WorkflowReadModel>()
    .Where(w => w.CurrentPhase == Phase.AwaitingApproval)
    .ToListAsync();
```

---

## PostgreSQL

Both Wolverine and Marten use PostgreSQL as their persistence layer.

### Database Tables

| Table/Schema | Content |
|--------------|---------|
| `wolverine_*` | Saga state, outbox messages, dead letters |
| `mt_events` | Event streams (append-only) |
| `mt_doc_*` | Document projections (read models) |
| `mt_streams` | Stream metadata and version tracking |

### Connection Configuration

```csharp
var connectionString = builder.Configuration.GetConnectionString("Postgres");

services.AddMarten(opts =>
{
    opts.Connection(connectionString);
});

services.AddWolverineWithMarten(opts =>
{
    opts.PersistenceConnection(connectionString);
});
```

---

## Microsoft.Extensions.AI

Integration for LLM-powered workflow steps.

### IChatClient Integration

```csharp
public class AnalyzeStep : IAgentStep<DocumentState>
{
    private readonly IChatClient _chatClient;

    public AnalyzeStep(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var response = await _chatClient.GetResponseAsync(
            $"Analyze: {state.Content}",
            ct);

        return state.With(s => s.Analysis, response).AsResult();
    }
}
```

### Supported Providers

| Provider | Package |
|----------|---------|
| OpenAI | `Microsoft.Extensions.AI.OpenAI` |
| Azure OpenAI | `Microsoft.Extensions.AI.AzureOpenAI` |
| Ollama | `OllamaChatClient` |
| Custom | Implement `IChatClient` |

### Streaming Responses

```csharp
public class StreamingStep : IAgentStep<ChatState>
{
    public async Task<StepResult<ChatState>> ExecuteAsync(
        ChatState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        await foreach (var chunk in _chatClient.GetStreamingResponseAsync(...))
        {
            context.StreamingCallback?.OnToken(chunk.Text);
        }
        // ...
    }
}
```

---

## Complete Setup Example

Full configuration with all integrations:

```csharp
var builder = WebApplication.CreateBuilder(args);

// PostgreSQL connection
var connectionString = builder.Configuration.GetConnectionString("Postgres");

// Marten (event sourcing)
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.AutoCreateSchemaObjects = AutoCreate.All;
});

// Wolverine (saga orchestration)
builder.Services.AddWolverineWithMarten(opts =>
{
    opts.PersistenceConnection(connectionString);
});

// Microsoft.Extensions.AI (LLM integration)
builder.Services.AddSingleton<IChatClient>(
    new OpenAIChatClient("gpt-4o", apiKey));

// Strategos
builder.Services.AddStrategos()
    .AddWorkflow<ProcessOrderWorkflow>()
    .AddThompsonSampling()
    .AddLoopDetection()
    .AddBudgetGuard();
```

---

## Planned Integrations

### pgvector

Status: Planned for `Strategos.Rag`

Will provide `PgVectorAdapter` implementing `IVectorSearchAdapter` for vector similarity search using the same PostgreSQL database.

### Azure AI Search

Status: Planned for `Strategos.Rag`

Will provide `AzureAISearchAdapter` for enterprise-scale vector and hybrid search.

---

## External Documentation

- [Wolverine Documentation](https://wolverine.netlify.app/)
- [Marten Documentation](https://martendb.io/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/)
