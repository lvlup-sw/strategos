# Getting Started

Welcome to Strategos! This guide walks you through building deterministic, auditable AI agent workflows in .NET. By the end, you will understand how to create workflows that are reliable, testable, and production-ready.

## What You Will Learn

- **Installation** - Add Strategos packages to your project
- **Basic Workflows** - Create your first workflow with state and steps
- **Branching** - Route execution based on conditions
- **Parallel Execution** - Run independent tasks concurrently
- **Loops** - Iterate until quality thresholds are met
- **Approvals** - Pause workflows for human review
- **Agent Selection** - Choose the best agent for each task

## Prerequisites

Before starting, ensure you have:

- **.NET 10 SDK** or later
- **PostgreSQL 14+** for workflow persistence
- Basic familiarity with C# and async/await

## Quick Start

If you prefer to jump straight into code, here is the minimal setup:

```csharp
// 1. Install packages
// dotnet add package LevelUp.Strategos
// dotnet add package LevelUp.Strategos.Marten

// 2. Configure services
services.AddStrategos()
    .AddMartenPersistence(connectionString);

// 3. Define a workflow
var workflow = Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Finally<SendConfirmation>();

// 4. Start the workflow
await workflowStarter.StartAsync("process-order", initialState);
```

## Guide Contents

Work through these tutorials in order for the best learning experience:

| Tutorial | Description |
|----------|-------------|
| [Installation](./installation) | Package setup and configuration |
| [First Workflow](./first-workflow) | Build an order processing workflow |
| [Branching](./branching) | Conditional routing with Branch DSL |
| [Parallel Execution](./parallel) | Fork/Join for concurrent execution |
| [Loops](./loops) | RepeatUntil for iterative refinement |
| [Approvals](./approvals) | Human-in-the-loop with AwaitApproval |
| [Agent Selection](./agents) | Thompson Sampling for agent routing |

## Next Steps

Ready to begin? Start with [Installation](./installation) to set up your development environment.

For conceptual background, see [Core Concepts](/learn/core-concepts) in the Learn section. For API details, consult the [Reference](/reference/) documentation.
