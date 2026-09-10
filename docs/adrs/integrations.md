# Integration Patterns

Strategos builds on proven .NET infrastructure rather than reinventing durability. This document explains how the library integrates with each component.

## Overview

| Integration | Purpose | Required |
|-------------|---------|----------|
| [Wolverine](https://wolverine.netlify.app/) | Saga orchestration, message routing, transactional outbox | Yes |
| [Marten](https://martendb.io/) | Event sourcing, projections, time-travel queries | Yes |
| [PostgreSQL](https://www.postgresql.org/) | Persistence for both Wolverine and Marten | Yes |
| [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/) | LLM integration via IChatClient | For AI workflows |

---

## Wolverine

[Wolverine Documentation](https://wolverine.netlify.app/)

### How Workflows Become Sagas

Every workflow definition is compiled into a Wolverine saga at build time. The source generator produces:

- A saga class with `[SagaIdentity]` for workflow instance tracking
- Command handlers for each step transition
- Automatic message cascading between steps

```
Workflow Definition → Source Generator → Wolverine Saga
     (DSL)              (compile-time)      (runtime)
```

### Message Routing

Step transitions are implemented as Wolverine messages:

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

This provides exactly-once processing semantics without manual implementation.

### Retry Policies

Wolverine handles transient failures automatically. Configure retry policies at the handler level:

```csharp
services.AddWolverine(opts =>
{
    opts.Handlers.OnException<HttpRequestException>()
        .RetryWithCooldown(100.Milliseconds(), 500.Milliseconds(), 1.Seconds());
});
```

---

## Marten

[Marten Documentation](https://martendb.io/)

### Event Sourcing

Every significant workflow occurrence is captured as an immutable event:

| Event | When Emitted |
|-------|--------------|
| `WorkflowStarted` | Workflow instance created |
| `PhaseChanged` | Transition between phases |
| `StepCompleted` | Step finished executing |
| `BranchTaken` | Routing decision made |
| `ApprovalRequested` | Workflow paused for human input |
| `ApprovalReceived` | Human input received |
| `WorkflowCompleted` | Workflow reached terminal state |

Events are appended to a stream identified by the workflow instance ID:

```csharp
session.Events.Append(
    workflowId,
    new PhaseChanged(workflowId, fromPhase, toPhase, timestamp));
```

### Time-Travel Queries

Because all state changes are captured as events, you can reconstruct state at any point in history:

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

Events are projected into read models optimized for querying:

```csharp
// Query workflows by phase
var awaitingApproval = await session
    .Query<WorkflowReadModel>()
    .Where(w => w.CurrentPhase == Phase.AwaitingApproval)
    .ToListAsync();
```

The source generator produces default projections; custom projections can be added via Marten's projection API.

---

## PostgreSQL

[PostgreSQL Documentation](https://www.postgresql.org/docs/)

### Why PostgreSQL

Both Wolverine and Marten use PostgreSQL as their persistence layer:

- **Wolverine**: Stores saga state, transactional outbox, and scheduled messages
- **Marten**: Stores event streams and document projections

Using a single database simplifies deployment, backup, and transactions.

### What's Stored

| Table/Schema | Content |
|--------------|---------|
| `wolverine_*` | Saga state, outbox messages, dead letters |
| `mt_events` | Event streams (append-only) |
| `mt_doc_*` | Document projections (read models) |
| `mt_streams` | Stream metadata and version tracking |

### Connection String

Configure via standard .NET options:

```csharp
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

[Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/)

### How Agent Steps Integrate

The `Strategos.Agents` package provides `IAgentStep<TState>` which integrates with `IChatClient` from Microsoft.Extensions.AI:

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

Any `IChatClient` implementation works:

- **OpenAI** via `Microsoft.Extensions.AI.OpenAI`
- **Azure OpenAI** via `Microsoft.Extensions.AI.AzureOpenAI`
- **Ollama** via `OllamaChatClient`
- **Custom** via `IChatClient` interface

### Streaming Responses

For real-time token streaming, implement `IStreamingCallback`:

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

## Planned Integrations

### pgvector (PostgreSQL Vector Extension)

Status: Planned for `Strategos.Rag`

Will provide `PgVectorAdapter` implementing `IVectorSearchAdapter` for vector similarity search using the same PostgreSQL database.

### Azure AI Search

Status: Planned for `Strategos.Rag`

Will provide `AzureAISearchAdapter` for enterprise-scale vector and hybrid search.

---

## Configuration Example

Complete setup with all integrations:

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

See the [Wolverine](https://wolverine.netlify.app/) and [Marten](https://martendb.io/) documentation for detailed configuration options.
