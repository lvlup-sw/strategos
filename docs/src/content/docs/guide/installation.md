---
title: "Installation"
---

# Installation

This guide covers installing Strategos packages and configuring your .NET application for workflow development.

## Package Options

Strategos is distributed as several NuGet packages. Choose the combination that fits your needs:

### Minimal Setup

For basic workflow functionality without persistence:

```bash
dotnet add package LevelUp.Strategos
```

This includes the core workflow DSL, step interfaces, and in-memory execution. Suitable for prototyping and testing.

### Standard Setup (Recommended)

For production workflows with durable persistence:

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Marten
```

This adds PostgreSQL persistence via [Marten](https://martendb.io/) and [Wolverine](https://wolverinefx.net/), enabling workflows that survive process restarts.

### Full Setup with RAG

For AI agent workflows with retrieval-augmented generation:

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Marten
dotnet add package LevelUp.Strategos.Agents
dotnet add package LevelUp.Strategos.RAG
```

This adds Thompson Sampling agent selection and vector-based document retrieval.

## Dependencies

### Required

- **.NET 10 SDK** - Strategos targets .NET 10
- **PostgreSQL 14+** - Required for Marten persistence

### Included Automatically

The following packages are pulled in as transitive dependencies:

- **Wolverine** - Message-based workflow orchestration
- **Marten** - Document database and event sourcing on PostgreSQL
- **JasperFx** - Core runtime components

## Service Registration

Configure Strategos in your `Program.cs` or startup class.

### Minimal Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add workflow services with in-memory execution
builder.Services.AddStrategos();

var app = builder.Build();
app.Run();
```

### Standard Configuration with Persistence

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");

// Add workflow services with Marten persistence
builder.Services.AddStrategos()
    .AddMartenPersistence(connectionString);

// Optional: Configure Wolverine for advanced scenarios
builder.Host.UseWolverine(opts =>
{
    opts.Durability.Mode = DurabilityMode.Solo;
});

var app = builder.Build();
app.Run();
```

### Full Configuration with Agents

```csharp
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");

builder.Services.AddStrategos()
    .AddMartenPersistence(connectionString)
    .AddAgentSelection(options => options
        .WithPrior(alpha: 2, beta: 2)
        .WithCategories(
            TaskCategory.Analysis,
            TaskCategory.Coding,
            TaskCategory.Research));

// Register agents
builder.Services.AddAgent("analyst", new AgentConfig
{
    Name = "Data Analyst",
    Capabilities = ["data-analysis", "visualization"]
});

var app = builder.Build();
app.Run();
```

## PostgreSQL Setup

Marten automatically creates database schemas on startup. Ensure your PostgreSQL user has the necessary permissions.

### Connection String

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=workflows;Username=app;Password=secret"
  }
}
```

### Docker Quick Start

For local development:

```bash
docker run -d \
  --name workflow-postgres \
  -e POSTGRES_USER=app \
  -e POSTGRES_PASSWORD=secret \
  -e POSTGRES_DB=workflows \
  -p 5432:5432 \
  postgres:16
```

### Schema Migrations

Marten handles schema creation automatically. For production deployments, you can generate migration scripts:

```csharp
// During development
builder.Services.AddMarten(connectionString)
    .ApplyAllDatabaseChangesOnStartup();

// For production - generate scripts instead
var store = app.Services.GetRequiredService<IDocumentStore>();
await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
```

## Verify Installation

Create a simple workflow to verify your setup works correctly.

### 1. Define State

```csharp
[WorkflowState]
public record HelloState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public string Message { get; init; } = string.Empty;
}
```

### 2. Create a Step

```csharp
public class SayHello : IWorkflowStep<HelloState>
{
    public Task<StepResult<HelloState>> ExecuteAsync(
        HelloState state,
        StepContext context,
        CancellationToken ct)
    {
        var result = state
            .With(s => s.Message, "Hello, Strategos!")
            .AsResult();

        return Task.FromResult(result);
    }
}
```

### 3. Define the Workflow

```csharp
var workflow = Workflow<HelloState>
    .Create("hello-world")
    .StartWith<SayHello>();
```

### 4. Register and Run

```csharp
// In Program.cs
builder.Services.AddStrategos()
    .AddWorkflow<HelloWorldWorkflow>();

// In a controller or service
public class TestController : ControllerBase
{
    private readonly IWorkflowStarter _starter;

    public TestController(IWorkflowStarter starter)
    {
        _starter = starter;
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        var state = new HelloState { WorkflowId = Guid.NewGuid() };
        await _starter.StartAsync("hello-world", state);
        return Ok("Workflow started");
    }
}
```

### 5. Verify Output

Start your application and call the test endpoint. Check your logs for:

```plaintext
info: Strategos[0]
      Workflow hello-world started with ID: a1b2c3d4-...
      Executing step: SayHello
      Step completed: SayHello
      Workflow hello-world completed
```

## Troubleshooting

### Common Issues

#### Missing package reference

```plaintext
error CS0246: The type or namespace name 'IWorkflowState' could not be found
```

Ensure `Strategos` is referenced and you have `using Strategos;`

#### PostgreSQL connection failed

```plaintext
Npgsql.NpgsqlException: Failed to connect to host
```

Verify PostgreSQL is running and the connection string is correct.

#### Schema creation failed

```plaintext
42501: permission denied for schema public
```

Grant your database user permission to create tables, or run migrations as a superuser.

## Key Points

- Start with the **minimal setup** for prototyping, add persistence for production
- **Marten** handles schema management automatically
- **Wolverine** provides durable message processing
- Verify installation with a simple hello-world workflow
- Connection strings go in `appsettings.json`

## Next Steps

Now that you have Strategos installed, continue to [Your First Workflow](./first-workflow) to build a complete order processing example.
