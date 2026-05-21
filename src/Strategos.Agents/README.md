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

## Diagnostics

Strategos.Agents throws typed `AgentException` subclasses keyed by short
diagnostic identifiers (`AGAG001`..`AGAG006`). The identifiers are part of
the public contract — catch on the exception type, branch on
`exception.Diagnostic`, and forward the code to your telemetry pipeline.
The literals live in `Strategos.Agents.Diagnostics.AgentDiagnostics`.

| Code | Exception | Meaning | When thrown |
|------|-----------|---------|-------------|
| `AGAG001` | `AgentBuilderValidationException` | A required builder hook delegate was missing at `Build()` time. | `AgentStepBuilder.Build()` invoked with a required hook missing (DR-2). |
| `AGAG002` | `AgentStructuredOutputException` | Structured-output deserialization failed — `ChatResponse<T>.TryGetResult` returned false. | The chat client returned a `ChatResponse<TResult>` whose payload would not bind to `TResult` (DR-3). Carries a truncated (≤4 KB) copy of the raw payload. |
| `AGAG003` | `AgentDuplicateToolException` | Duplicate tool name registered on an `AgentStepBuilder`. | `AgentStepBuilder.Build()` detects two `AIFunction`s with the same name (DR-4). |
| `AGAG004` | `AgentMcpException` | MCP client handshake or tool-discovery failure. | An `IMcpToolSource` adapter fails to open the MCP transport or list tools (DR-5). The endpoint is surfaced with user-info credentials stripped. |
| `AGAG005` | `AgentToolLoopException` | Tool-invocation iteration count exceeded the configured maximum. | The chat-tool loop hits its bounded cap (DR-8). Carries the cap and a partial trace of the tool-call messages. |
| `AGAG006` | `AgentChatResponseException` | Chat client returned a null or empty `ChatResponse<T>`. | The chat client yielded no usable response object for the agent step (DR-10). |

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
