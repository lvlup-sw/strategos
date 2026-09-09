---
title: "Agents API"
---

# Agents API

The `Strategos.Agents` package provides integration with Microsoft.Extensions.AI for LLM-powered workflow steps.

## IAgentStep\<TState, TResult\>

Marker interface for LLM-powered workflow steps that produce a typed structured
result. It extends `IWorkflowStep<TState>`; use `string` as `TResult` for
unstructured output.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `ExecuteAsync` | `TState state`, `StepContext context`, `CancellationToken ct` | `Task<StepResult<TState>>` | Executes the inherited workflow-step contract |

### Example

```csharp
IAgentStep<DocumentState, string> step =
    new AgentStepBuilder<DocumentState, string>()
        .WithSystemPrompt(_ => "You are a document analyst.")
        .WithUserPrompt(state => $"Analyze this document: {state.Content}")
        .WithApplyResult((state, result, _) =>
            Task.FromResult((state with { Analysis = result }).AsResult()))
        .Build(chatClient);
```

---

## AgentStepContext

Optional agent-services value used by integrations that assemble chat execution.
It is a separate record and does not inherit `StepContext`; workflow-step
implementations receive `StepContext` through `ExecuteAsync`.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `WorkflowId` | `Guid` | Workflow instance identifier |
| `StepName` | `string` | Current step name |
| `StepExecutionId` | `Guid` | Unique identity for this step execution |
| `ChatClient` | `IChatClient` | Chat client used for LLM interaction |
| `ConversationThreadManager` | `IConversationThreadManager?` | Optional conversation-continuity service |
| `StreamingCallback` | `IStreamingCallback?` | Real-time token streaming |

### Example

```csharp
var agentContext = new AgentStepContext(
    chatClient,
    workflowId,
    stepName,
    stepExecutionId,
    streamingCallback,
    conversationThreadManager);
```

---

## IConversationalState

Interface for workflow state that persists one serialized conversation thread
per agent type.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SerializedThreads` | `ImmutableDictionary<string, string>` | Serialized conversation history keyed by agent type |

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `WithSerializedThread` | `string agentType`, `string serializedThread` | `IConversationalState` | Returns state with one agent's serialized thread replaced |

### Example

```csharp
[WorkflowState]
public record ChatState : IWorkflowState, IConversationalState
{
    public Guid WorkflowId { get; init; }
    public string Query { get; init; } = "";
    public string Response { get; init; } = "";
    public ImmutableDictionary<string, string> SerializedThreads { get; init; }
        = ImmutableDictionary<string, string>.Empty;

    public IConversationalState WithSerializedThread(
        string agentType,
        string serializedThread) =>
        this with
        {
            SerializedThreads = SerializedThreads.SetItem(agentType, serializedThread),
        };
}
```

---

## IConversationThreadManager

Port for restoring an agent chat client from serialized history and saving its
current conversation thread.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CreateAgentWithThreadAsync` | `string agentType`, `string? serializedThread`, `CancellationToken ct` | `Task<IChatClient>` | Restores a chat client or creates a new thread |
| `SerializeThreadAsync` | `string agentType`, `CancellationToken ct` | `Task<string>` | Serializes the current thread for persistence |

---

## Streaming observers

`AgentStepBuilder<TState, TResult>.WithStreaming(...)` accepts an
`IStreamingHandler`. `IStreamingCallback` has the same callback shape but belongs
to the legacy specialist-agent surface exposed through `AgentStepContext`; it is
not the observer configured by `WithStreaming`.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `OnTokenReceivedAsync` | `string token`, `Guid workflowId`, `string stepName`, `CancellationToken ct` | `Task` | Called for each non-empty streamed token |
| `OnResponseCompletedAsync` | `string fullResponse`, `Guid workflowId`, `string stepName`, `CancellationToken ct` | `Task` | Called once after the response stream completes |

### Example

```csharp
var streamingStep = new AgentStepBuilder<ChatState, string>()
    .WithSystemPrompt(_ => "You are a concise assistant.")
    .WithUserPrompt(state => state.Query)
    .WithApplyResult((state, result, _) =>
        Task.FromResult((state with { Response = result }).AsResult()))
    .WithStreaming(streamingHandler) // IStreamingHandler
    .Build(chatClient);
```

---

## IChatClient Integration

The builder accepts any `Microsoft.Extensions.AI.IChatClient`; provider setup is
owned by the host. Strategos composes its bounded function-invocation pipeline
around that client when `Build(chatClient)` runs. `WithSystemPrompt`,
`WithUserPrompt`, and `WithApplyResult` are required. Optional configuration
includes `WithTool`, `WithToolSource`, `WithChatOptions`, `WithStreaming`,
`WithMaxToolIterations`, and `ConfigureChatClient`.
