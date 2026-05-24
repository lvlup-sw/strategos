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

### Tool Sources

Beyond per-call `WithTool`, register `IToolSource` ports that resolve their
`AIFunction`s lazily on first execution. The in-process `AgentToolSource`
reflects `[AgentTool]`-annotated methods (no external dependency); the
`McpToolSource` (in `Strategos.Agents.Mcp`) discovers tools from a remote MCP
server. Sources merge after `WithTool` tools, in registration order:

```csharp
using Strategos.Agents;
using Strategos.Agents.Mcp;

var step = new AgentStepBuilder<DocumentState, DocumentAnalysis>()
    .WithSystemPrompt(_ => "Use the available tools.")
    .WithUserPrompt(s => s.DocumentText)
    .WithApplyResult((state, result, _) =>
        Task.FromResult(new StepResult<DocumentState>(state with { Analysis = result })))
    .WithToolSource(AgentToolSource.FromObject(new MyLocalSkills()))
    .WithToolSource(McpToolSource.ForHttpEndpoint(
        new Uri("https://tools.example.com/mcp"), TimeSpan.FromSeconds(30)))
    .Build(chatClient);
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

Register an `IStreamingHandler` with `WithStreaming(...)` to observe tokens as
they arrive. Streaming is an observability side-channel — the terminal typed
result contract is unchanged (tokens fire before `ApplyResult` runs):

```csharp
var step = new AgentStepBuilder<DocumentState, DocumentAnalysis>()
    .WithSystemPrompt(_ => "You are a document analyst.")
    .WithUserPrompt(s => s.DocumentText)
    .WithApplyResult((state, result, _) =>
        Task.FromResult(new StepResult<DocumentState>(state with { Analysis = result })))
    .WithStreaming(myStreamingHandler) // IStreamingHandler
    .Build(chatClient);
```

## Diagnostics

Strategos.Agents throws typed `AgentException` subclasses keyed by short
diagnostic identifiers (`AGAG001`..`AGAG009`). The identifiers are part of
the public contract — catch on the exception type, branch on
`exception.Diagnostic`, and forward the code to your telemetry pipeline.
The literals live in `Strategos.Agents.Diagnostics.AgentDiagnostics`.

| Code | Exception | Meaning | When thrown | Remediation |
|------|-----------|---------|-------------|-------------|
| `AGAG001` | `AgentBuilderValidationException` | A required builder hook delegate was missing at `Build()` time. | `AgentStepBuilder.Build()` invoked with a required hook missing (DR-2). | Supply the named hook (`WithSystemPrompt` / `WithUserPrompt` / `WithApplyResult`). |
| `AGAG002` | `AgentStructuredOutputException` | Structured-output deserialization failed — `ChatResponse<T>.TryGetResult` returned false. | The chat client returned a `ChatResponse<TResult>` whose payload would not bind to `TResult` (DR-3). Carries a truncated (≤4 KB) copy of the raw payload. | Tighten the prompt's output schema; inspect the carried payload. |
| `AGAG003` | `AgentDuplicateToolException` | Duplicate tool name registered on an `AgentStepBuilder`. | `AgentStepBuilder.Build()` detects two `AIFunction`s with the same name (DR-4). | Rename or de-duplicate the colliding tool. |
| `AGAG004` | `AgentMcpException` | MCP client handshake or tool-discovery failure. | An `McpToolSource` adapter fails to open the MCP transport or list tools (DR-5). The endpoint is surfaced with user-info credentials stripped. | Verify the MCP endpoint reachability/credentials. |
| `AGAG005` | `AgentToolLoopException` | Tool-invocation iteration count exceeded the configured maximum. | The chat-tool loop hits its bounded cap (DR-8). Carries the cap and a partial trace of the tool-call messages. | Raise `WithMaxToolIterations` or simplify the tool plan. |
| `AGAG006` | `AgentChatResponseException` | Chat client returned a null or empty `ChatResponse<T>`. | The chat client yielded no usable response object for the agent step (DR-10). | Check the model/transport; retry the request. |
| `AGAG007` | `AgentToolSourceException` | In-process tool-source resolution failed. | An `AgentToolSource` fails to build its `AIFunction`s (e.g. reflection/factory error). The offending source type is named. | Fix the `[AgentTool]` method signature or delegate on the named source. |
| `AGAG009` | `AgentStreamingException` | A streaming handler callback threw mid-stream. | An `IStreamingHandler` registered via `WithStreaming` faults during `OnTokenReceivedAsync` / `OnResponseCompletedAsync` (DR-4). State is untouched. | Harden the streaming handler; do not let observer callbacks throw. |

`AGAG008` is reserved (pending a build-time validation case) and is not
currently emitted.

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
