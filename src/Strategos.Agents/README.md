# Strategos.Agents

Microsoft Agent Framework integration for Strategos. Provides abstractions for LLM-powered workflow steps with conversation continuity and streaming responses.

## Installation

```bash
dotnet add package LevelUp.Strategos.Agents
```

## Features

### Agent Steps

Create workflow steps powered by LLM agents:

```csharp
public class AnalyzeDocumentStep : IAgentStep<DocumentState>
{
    public string GetSystemPrompt() => """
        You are a document analyst. Analyze the provided document and extract key insights.
        Focus on: main topics, sentiment, key entities, and actionable recommendations.
        """;

    public Type? GetOutputSchemaType() => typeof(DocumentAnalysis);

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        // Agent execution handled by generated worker
        return StepResult<DocumentState>.FromState(state);
    }
}
```

### Conversation Continuity

Enable per-agent conversation threads for context retention:

```csharp
public record MyState : IWorkflowState, IConversationalState
{
    public Guid WorkflowId { get; init; }
    public ImmutableDictionary<string, string> SerializedThreads { get; init; }
        = ImmutableDictionary<string, string>.Empty;

    public IConversationalState WithSerializedThread(string agentType, string thread)
        => this with { SerializedThreads = SerializedThreads.SetItem(agentType, thread) };
}
```

### Streaming Responses

Handle real-time token streaming:

```csharp
public class WebSocketStreamingCallback : IStreamingCallback
{
    public async Task OnTokenReceivedAsync(
        string token, Guid workflowId, string stepName, CancellationToken ct)
    {
        await _hubContext.Clients.Group(workflowId.ToString())
            .SendAsync("TokenReceived", token, ct);
    }

    public async Task OnResponseCompletedAsync(
        string fullResponse, Guid workflowId, string stepName, CancellationToken ct)
    {
        await _hubContext.Clients.Group(workflowId.ToString())
            .SendAsync("ResponseCompleted", fullResponse, ct);
    }
}
```

## Configuration

```csharp
services.AddStrategosAgents()
    .AddConversationThreadManager<MyThreadManager>()
    .AddStreamingCallback<WebSocketStreamingCallback>();
```

## Core Abstractions

| Interface | Purpose |
|-----------|---------|
| `IAgentStep<TState>` | Workflow step powered by LLM agent |
| `IConversationalState` | State with per-agent conversation threads |
| `IConversationThreadManager` | Manages conversation thread lifecycle |
| `IStreamingCallback` | Handles real-time token streaming |

## Documentation

- **[Agent Selection Guide](https://lvlup-sw.github.io/strategos/guide/agents)** - Configuring agents in workflows
- **[Agents API Reference](https://lvlup-sw.github.io/strategos/reference/api/agents)** - Complete API documentation

## License

MIT
