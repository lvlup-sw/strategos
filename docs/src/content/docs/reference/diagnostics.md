---
title: "Compiler Diagnostics"
---

# Compiler Diagnostics

The Strategos source generator includes compile-time diagnostics that catch workflow definition errors before runtime.

## Diagnostic Namespaces

| Prefix | Category | Description |
|--------|----------|-------------|
| AGWF | Workflow | Workflow definition validation |
| AGSR | State Reducer | State reducer attribute validation |

## Quick Reference

| Code | Severity | Description |
|------|----------|-------------|
| AGWF001 | Error | Workflow name cannot be empty |
| AGWF002 | Warning | No steps found in workflow |
| AGWF003 | Error | Duplicate step name |
| AGWF004 | Error | Invalid namespace (global namespace not supported) |
| AGWF009 | Error | Workflow must begin with `StartWith<T>()` |
| AGWF010 | Warning | Workflow should end with `Finally<T>()` |
| AGWF012 | Error | Fork must be followed by `Join<T>()` |
| AGWF014 | Error | Loop body cannot be empty |
| AGSR001 | Error | `[Append]` can only be applied to collection types |
| AGSR002 | Error | `[Merge]` can only be applied to dictionary types |

---

## Workflow Diagnostics (AGWF)

### AGWF001: Empty Workflow Name

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | DSL Completeness |

The `[Workflow]` attribute requires a non-empty workflow name.

**Invalid:**
```csharp
[Workflow("")]  // Error: Empty name
[Workflow("   ")]  // Error: Whitespace only
```

**Valid:**
```csharp
[Workflow("process-order")]
```

---

### AGWF002: No Steps Found

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | DSL Completeness |

The workflow definition doesn't contain any recognizable step methods.

**Trigger:**
```csharp
[Workflow("empty-workflow")]
public static partial class EmptyWorkflow
{
    public static WorkflowDefinition<State> Definition =>
        Workflow<State>.Create("empty-workflow");  // Warning: No steps
}
```

**Resolution:**
```csharp
Workflow<State>.Create("my-workflow")
    .StartWith<FirstStep>()
    .Then<SecondStep>()
    .Finally<LastStep>();
```

---

### AGWF003: Duplicate Step Name

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | Structural Validation |

The same step type appears multiple times where each step must be unique (linear flow, fork paths).

**Invalid:**
```csharp
.StartWith<ValidateStep>()
.Then<ProcessStep>()
.Then<ValidateStep>()  // Error: Duplicate
```

**Resolution:** Use instance names to disambiguate:
```csharp
.StartWith<ValidateStep>()
.Then<ProcessStep>()
.Then<ValidateStep>("FinalValidation")  // OK: Different instance
```

::: info
Duplicates in mutually exclusive branch paths are allowed since only one path executes.
:::

---

### AGWF004: Invalid Namespace

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | DSL Completeness |

Workflows must be declared in a namespace. Global namespace is not supported.

**Invalid:**
```csharp
// No namespace declaration
[Workflow("orphan-workflow")]
public static partial class OrphanWorkflow { }  // Error
```

**Valid:**
```csharp
namespace MyApp.Workflows;

[Workflow("my-workflow")]
public static partial class MyWorkflow { }
```

---

### AGWF009: Missing StartWith

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | DSL Completeness |

Every workflow must begin with `StartWith<T>()` to define the entry point.

**Invalid:**
```csharp
Workflow<State>.Create("bad-workflow")
    .Then<FirstStep>()  // Error: Should be StartWith
    .Finally<LastStep>();
```

**Valid:**
```csharp
Workflow<State>.Create("good-workflow")
    .StartWith<FirstStep>()
    .Finally<LastStep>();
```

---

### AGWF010: Missing Finally

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | DSL Completeness |

Workflows should end with `Finally<T>()` to mark completion.

**Trigger:**
```csharp
Workflow<State>.Create("incomplete-workflow")
    .StartWith<FirstStep>()
    .Then<SecondStep>();  // Warning: No Finally
```

**Resolution:**
```csharp
Workflow<State>.Create("complete-workflow")
    .StartWith<FirstStep>()
    .Then<SecondStep>()
    .Finally<CompletionStep>();
```

::: info Why Warning (not Error)
Some patterns intentionally short-circuit via `Complete()` in branches. The DSL allows this, so we warn rather than block.
:::

---

### AGWF012: Fork without Join

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | Structural Validation |

Every `Fork()` construct must be followed by `Join<T>()` to merge parallel paths.

**Invalid:**
```csharp
.StartWith<PrepareStep>()
.Fork(
    path => path.Then<PathA>(),
    path => path.Then<PathB>())
.Then<NextStep>()  // Error: Fork not followed by Join
.Finally<EndStep>();
```

**Valid:**
```csharp
.StartWith<PrepareStep>()
.Fork(
    path => path.Then<PathA>(),
    path => path.Then<PathB>())
.Join<MergeResultsStep>()  // Correct: Join after Fork
.Finally<EndStep>();
```

---

### AGWF014: Loop without Body

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | Structural Validation |

`RepeatUntil` loops must contain at least one step in their body.

**Invalid:**
```csharp
.StartWith<InitStep>()
.RepeatUntil(s => s.Done, "process", loop => { })  // Error: Empty body
.Finally<EndStep>();
```

**Valid:**
```csharp
.StartWith<InitStep>()
.RepeatUntil(s => s.Done, "process", loop => loop
    .Then<ProcessItemStep>()
    .Then<CheckProgressStep>())
.Finally<EndStep>();
```

---

## State Reducer Diagnostics (AGSR)

### AGSR001: Invalid Append Target

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | State Reducer |

The `[Append]` attribute can only be applied to collection types (e.g., `List<T>`, `IList<T>`).

**Invalid:**
```csharp
[WorkflowState]
public record State
{
    [Append]
    public string Name { get; init; }  // Error: Not a collection
}
```

**Valid:**
```csharp
[WorkflowState]
public record State
{
    [Append]
    public List<string> Items { get; init; }
}
```

---

### AGSR002: Invalid Merge Target

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | State Reducer |

The `[Merge]` attribute can only be applied to dictionary types (e.g., `Dictionary<TKey, TValue>`).

**Invalid:**
```csharp
[WorkflowState]
public record State
{
    [Merge]
    public List<string> Items { get; init; }  // Error: Not a dictionary
}
```

**Valid:**
```csharp
[WorkflowState]
public record State
{
    [Merge]
    public Dictionary<string, int> Scores { get; init; }
}
```

---

## Diagnostic Categories

### DSL Completeness

Ensures the workflow definition has all required components:

- Valid name (AGWF001)
- Proper namespace (AGWF004)
- Entry point via `StartWith` (AGWF009)
- Exit point via `Finally` (AGWF010)
- At least one step (AGWF002)

### Structural Validation

Validates the logical structure of the workflow:

- No ambiguous step references (AGWF003)
- Proper construct pairing like Fork/Join (AGWF012)
- Non-empty constructs like loop bodies (AGWF014)

### State Reducer

Validates state attribute usage:

- `[Append]` on collections (AGSR001)
- `[Merge]` on dictionaries (AGSR002)

---

## Error vs Warning

| Severity | Meaning | Code Generation |
|----------|---------|-----------------|
| Error | Workflow cannot execute correctly | Blocked |
| Warning | Pattern may be intentional but warrants review | Allowed |
