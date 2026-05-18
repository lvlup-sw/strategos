# Strategos.Agents

Microsoft Agent Framework integration for Strategos. Provides abstractions for LLM-powered workflow steps with conversation continuity and streaming responses.

## Installation

```bash
dotnet add package LevelUp.Strategos.Agents
```

## Features

### Agent Steps

Build an `IAgentStep<TState, TResult>` with the fluent builder (post-DR-11
two-arity contract). `WithSystemPrompt`, `WithUserPrompt`, and
`WithApplyResult` are required; missing any throws `AGAG001` at `Build()`:

```csharp
using Strategos.Agents;
using Strategos.Steps;

var step = new AgentStepBuilder<DocumentState, DocumentAnalysis>()
    .WithSystemPrompt(_ => "You are a document analyst. Extract key insights.")
    .WithUserPrompt(s => s.DocumentText)
    .WithApplyResult((state, result, _) =>
        Task.FromResult(new StepResult<DocumentState>(state with { Analysis = result })))
    .WithTool(summarizeFunction)
    .ConfigureChatClient(b => b.UseLogging(loggerFactory))
    .Build(chatClient);

var stepResult = await step.ExecuteAsync(state, context, ct);
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
| `IAgentStep<TState, TResult>` | Workflow step powered by LLM agent with typed structured result |
| `AgentStepBuilder<TState, TResult>` | Fluent builder; the only sanctioned construction path |
| `IConversationalState` | State with per-agent conversation threads |
| `IConversationThreadManager` | Manages conversation thread lifecycle |
| `IStreamingCallback` | Handles real-time token streaming |

## Documentation

- **[Agent Selection Guide](https://lvlup-sw.github.io/strategos/guide/agents)** - Configuring agents in workflows
- **[Agents API Reference](https://lvlup-sw.github.io/strategos/reference/api/agents)** - Complete API documentation

## License

MIT
