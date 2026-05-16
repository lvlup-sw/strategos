---
title: "Agents API"
---

# Agents API

The `Strategos.Agents` package provides integration with Microsoft.Extensions.AI for LLM-powered workflow steps.

## IAgentStep\<TState\>

Base interface for LLM-powered workflow steps. Extends `IWorkflowStep<TState>` with agent-specific context.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `ExecuteAsync` | `TState state`, `AgentStepContext context`, `CancellationToken ct` | `Task<StepResult<TState>>` | Executes the agent step |

### Example

```csharp
public class AnalyzeDocumentStep : IAgentStep<DocumentState>
{
    private readonly IChatClient _chatClient;

    public AnalyzeDocumentStep(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var response = await _chatClient.GetResponseAsync(
            $"Analyze this document: {state.Content}",
            ct);

        return state
            .With(s => s.Analysis, response)
            .AsResult();
    }
}
```

---

## AgentStepContext

Extended execution context for agent steps. Inherits all properties from `StepContext`.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `WorkflowId` | `Guid` | Workflow instance identifier |
| `CorrelationId` | `string` | Correlation ID for tracing |
| `Timestamp` | `DateTimeOffset` | When the step execution started |
| `Phase` | `string` | Current workflow phase name |
| `StepName` | `string` | Current step name |
| `Metadata` | `IReadOnlyDictionary<string, object>` | Additional context data |
| `ConversationThread` | `IConversationThread?` | Conversation history access |
| `StreamingCallback` | `IStreamingCallback?` | Real-time token streaming |
| `BudgetStatus` | `BudgetStatus` | Current resource budget |

### Example

```csharp
public async Task<StepResult<ChatState>> ExecuteAsync(
    ChatState state,
    AgentStepContext context,
    CancellationToken ct)
{
    // Access conversation history
    var history = context.ConversationThread?.GetMessages();

    // Check budget before expensive operation
    if (context.BudgetStatus.Level == ScarcityLevel.Critical)
    {
        return state.With(s => s.Response, "Budget exhausted").AsResult();
    }

    // Stream response tokens
    await foreach (var chunk in _chatClient.GetStreamingResponseAsync(...))
    {
        context.StreamingCallback?.OnToken(chunk.Text);
    }

    // ...
}
```

---

## IConversationalState

Interface for workflow state that includes conversation history.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Messages` | `IReadOnlyList<ChatMessage>` | Conversation history |

### Example

```csharp
[WorkflowState]
public record ChatState : IConversationalState
{
    public string Query { get; init; }
    public string Response { get; init; }

    [Append]
    public List<ChatMessage> Messages { get; init; } = new();

    IReadOnlyList<ChatMessage> IConversationalState.Messages => Messages;
}
```

---

## IConversationThread

Interface for accessing and managing conversation history.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `GetMessages` | - | `IReadOnlyList<ChatMessage>` | Gets full conversation history |
| `GetRecentMessages` | `int count` | `IReadOnlyList<ChatMessage>` | Gets N most recent messages |
| `AddMessage` | `ChatMessage message` | `void` | Appends message to history |

---

## IStreamingCallback

Callback interface for real-time token streaming.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `OnToken` | `string token` | `void` | Called for each streamed token |
| `OnComplete` | - | `void` | Called when streaming completes |
| `OnError` | `Exception error` | `void` | Called on streaming error |

### Example

```csharp
public class StreamingStep : IAgentStep<ChatState>
{
    private readonly IChatClient _chatClient;

    public async Task<StepResult<ChatState>> ExecuteAsync(
        ChatState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var fullResponse = new StringBuilder();

        await foreach (var chunk in _chatClient.GetStreamingResponseAsync(
            state.Query, ct))
        {
            fullResponse.Append(chunk.Text);
            context.StreamingCallback?.OnToken(chunk.Text);
        }

        context.StreamingCallback?.OnComplete();

        return state
            .With(s => s.Response, fullResponse.ToString())
            .AsResult();
    }
}
```

---

## ChatMessage

Represents a single message in a conversation.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Role` | `ChatRole` | Message role (User, Assistant, System) |
| `Content` | `string` | Message content |
| `Timestamp` | `DateTimeOffset` | When message was created |
| `Metadata` | `Dictionary<string, object>` | Additional message data |

---

## ChatRole

Enumeration of message roles.

| Value | Description |
|-------|-------------|
| `System` | System/instruction message |
| `User` | User input message |
| `Assistant` | LLM response message |
| `Tool` | Tool/function result message |

---

## IChatClient Integration

The package integrates with `Microsoft.Extensions.AI.IChatClient`.

### Supported Providers

| Provider | Package | Registration |
|----------|---------|--------------|
| OpenAI | `Microsoft.Extensions.AI.OpenAI` | `new OpenAIChatClient(model, apiKey)` |
| Azure OpenAI | `Microsoft.Extensions.AI.AzureOpenAI` | `new AzureOpenAIChatClient(endpoint, key)` |
| Ollama | `OllamaChatClient` | `new OllamaChatClient(model)` |

### Example Registration

```csharp
// OpenAI
services.AddSingleton<IChatClient>(
    new OpenAIChatClient("gpt-4o", apiKey));

// Azure OpenAI
services.AddSingleton<IChatClient>(
    new AzureOpenAIChatClient(
        new Uri("https://your-resource.openai.azure.com"),
        new AzureKeyCredential(apiKey),
        "gpt-4o"));

// Ollama (local)
services.AddSingleton<IChatClient>(
    new OllamaChatClient("llama2"));
```

---

## Agent Step Patterns

### Simple Chat Step

```csharp
public class SimpleChatStep : IAgentStep<ChatState>
{
    private readonly IChatClient _chatClient;

    public async Task<StepResult<ChatState>> ExecuteAsync(
        ChatState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var response = await _chatClient.GetResponseAsync(state.Query, ct);
        return state.With(s => s.Response, response).AsResult();
    }
}
```

### Step with History

```csharp
public class ConversationalStep : IAgentStep<ChatState>
{
    private readonly IChatClient _chatClient;

    public async Task<StepResult<ChatState>> ExecuteAsync(
        ChatState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var messages = state.Messages
            .Select(m => new ChatMessage(m.Role, m.Content))
            .Append(new ChatMessage(ChatRole.User, state.Query))
            .ToList();

        var response = await _chatClient.GetResponseAsync(messages, ct);

        return state
            .With(s => s.Response, response)
            .With(s => s.Messages, state.Messages
                .Append(new ChatMessage { Role = ChatRole.User, Content = state.Query })
                .Append(new ChatMessage { Role = ChatRole.Assistant, Content = response })
                .ToList())
            .AsResult();
    }
}
```

### Step with Streaming

```csharp
public class StreamingChatStep : IAgentStep<ChatState>
{
    private readonly IChatClient _chatClient;

    public async Task<StepResult<ChatState>> ExecuteAsync(
        ChatState state,
        AgentStepContext context,
        CancellationToken ct)
    {
        var response = new StringBuilder();

        await foreach (var chunk in _chatClient.GetStreamingResponseAsync(
            state.Query, ct))
        {
            response.Append(chunk.Text);
            context.StreamingCallback?.OnToken(chunk.Text);
        }

        context.StreamingCallback?.OnComplete();

        return state.With(s => s.Response, response.ToString()).AsResult();
    }
}
```
