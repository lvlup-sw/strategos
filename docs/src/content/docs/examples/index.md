---
title: "Examples & Learning Paths"
---

# Examples & Learning Paths

Learn to build agentic workflows through structured tutorials that teach you to **think** about workflow design, not just copy code.

---

## How to Use This Documentation

Each example in this section teaches concepts before showing code:

| Section | What You'll Find |
|---------|------------------|
| **Problem Narrative** | Real-world scenario motivating the pattern |
| **Learning Objectives** | What you'll understand after reading |
| **Conceptual Foundation** | Design decisions, trade-offs, anti-patterns |
| **Progressive Code Reveal** | Shape first, then implementation details |
| **"Aha Moment"** | Core insight crystallized |
| **Extension Exercises** | Guided practice to deepen understanding |

---

## Learning Paths

Choose a path based on your goals and available time.

### Path 1: First Workflow (30 minutes)

**Goal**: Understand the foundational patterns that all workflows build on.

| Step | Read | Learn |
|------|------|-------|
| 1 | [Basic Workflow](./basic-workflow.md) | Sequential steps, immutable state, saga pattern |
| 2 | Run the [ContentPipeline sample](../../samples/ContentPipeline/) | See a workflow in action |
| 3 | **Exercise**: Add a `ReserveInventory` step to the order workflow |

**After this path**: You can build simple sequential workflows with proper state management.

---

### Path 2: AI Agent Patterns (2 hours)

**Goal**: Learn patterns for building intelligent, adaptive AI systems.

| Step | Read | Learn |
|------|------|-------|
| 1 | [Thompson Sampling](./thompson-sampling.md) | Multi-armed bandit, exploration vs exploitation |
| 2 | Run the [MultiModelRouter sample](../../samples/MultiModelRouter/) | See adaptive model selection |
| 3 | [Iterative Refinement](./iterative-refinement.md) | Quality loops, `[Append]` attribute, maxIterations |
| 4 | Run the [AgenticCoder sample](../../samples/AgenticCoder/) | See test-driven refinement |
| 5 | **Exercise**: Add a custom task category to MultiModelRouter |

**After this path**: You can build AI systems that learn and improve over time.

---

### Path 3: Production Patterns (3 hours)

**Goal**: Master patterns for production-ready workflows with human oversight.

| Step | Read | Learn |
|------|------|-------|
| 1 | [Approval Flow](./approval-flow.md) | Human checkpoints, timeout handling, escalation |
| 2 | [Branching](./branching.md) | Conditional routing, transition tables |
| 3 | [Fork/Join](./fork-join.md) | Parallel execution, state merging |
| 4 | Run the [ContentPipeline sample](../../samples/ContentPipeline/) | See approval gates and compensation |
| 5 | **Exercise**: Add multi-approver workflow to ContentPipeline |

**After this path**: You can build workflows with human-AI collaboration, parallel processing, and complex routing.

---

### Path 4: Complete Mastery (Full Day)

**Goal**: Understand all patterns and how they compose together.

1. Complete Path 1 (Foundation)
2. Complete Path 2 (AI Patterns)
3. Complete Path 3 (Production Patterns)
4. **Capstone Project**: Design a workflow that combines:
   - Thompson Sampling for agent selection
   - Iterative refinement for quality
   - Human approval before final action
   - Fork/Join for parallel analysis

---

## Pattern Quick Reference

| Pattern | When to Use | Key Concept |
|---------|-------------|-------------|
| [Basic Workflow](./basic-workflow.md) | Sequential operations with dependencies | Saga pattern, immutable state |
| [Branching](./branching.md) | Different logic for different inputs | Declarative routing, transition tables |
| [Fork/Join](./fork-join.md) | Independent operations that can parallelize | State merging, fail-fast vs continue |
| [Iterative Refinement](./iterative-refinement.md) | Quality improvement through feedback | `[Append]`, maxIterations circuit breaker |
| [Approval Flow](./approval-flow.md) | Human decisions in automated workflows | Timeout handling, escalation, audit |
| [Thompson Sampling](./thompson-sampling.md) | Adaptive selection from multiple options | Beta distributions, exploration/exploitation |

---

## Sample Applications

Runnable projects demonstrating complete implementations.

| Sample | Run Command | What It Demonstrates |
|--------|-------------|---------------------|
| [ContentPipeline](../../samples/ContentPipeline/) | `dotnet run --project samples/ContentPipeline` | Human approval gates, compensation, audit trails |
| [MultiModelRouter](../../samples/MultiModelRouter/) | `dotnet run --project samples/MultiModelRouter` | Thompson Sampling, intelligent model selection |
| [AgenticCoder](../../samples/AgenticCoder/) | `dotnet run --project samples/AgenticCoder` | Iterative refinement loops, human checkpoints |

Each sample README includes:
- Problem narrative and solution approach
- Conceptual explanation of the patterns used
- Step-by-step walkthrough of the implementation
- Extension exercises for practice

---

## Prerequisites

Before running examples, ensure you have:

1. **.NET 9.0 or later** installed
2. **PostgreSQL** running (for Marten event store)
3. **Strategos packages** installed:

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
```

---

## Quick Start Template

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add Wolverine for message handling
builder.Host.UseWolverine(opts =>
{
    opts.Durability.Mode = DurabilityMode.Solo;
});

// Add Marten for persistence
builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Marten")!);
})
.IntegrateWithWolverine();

// Add workflow services
builder.Services.AddStrategos()
    .AddWorkflow<YourWorkflow>();

// Register step dependencies
builder.Services.AddScoped<IYourService, YourServiceImpl>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

---

## Key Principles Across All Patterns

| Principle | Why It Matters |
|-----------|----------------|
| **State is immutable** | Enables replay, debugging, concurrency safety |
| **Steps are resolved via DI** | Testability, loose coupling |
| **Failures are explicit** | `StepResult.Fail()` not exceptions |
| **Workflows survive restarts** | Durability via Wolverine saga persistence |
| **Everything is audited** | Events capture all state transitions |

---

## What's Next?

After completing these examples:

1. **Read the [Learn section](../learn/)** for deeper conceptual understanding
2. **Explore the [API Reference](../api/)** for complete interface documentation
3. **Check the [samples directory](../../samples/)** for production-quality implementations
4. **Join the community** to share your workflows and learn from others
