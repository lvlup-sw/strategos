# Workflow Compiler Diagnostics

Version 1.0 | Status: Implemented

Last Updated: 2025-12-26

---

## Table of Contents

1. [Overview](#overview)
2. [Design Philosophy](#design-philosophy)
3. [Implemented Diagnostics](#implemented-diagnostics)
4. [Deferred Diagnostics](#deferred-diagnostics)
5. [Diagnostic Categories](#diagnostic-categories)
6. [Implementation Details](#implementation-details)
7. [Testing Strategy](#testing-strategy)

---

## Overview

The Agentic Workflow source generator includes a comprehensive set of compile-time diagnostics that catch workflow definition errors before runtime. These diagnostics provide immediate feedback in the IDE, preventing common mistakes and guiding developers toward correct usage of the fluent DSL.

### Diagnostic Namespaces

| Prefix | Category | Description |
|--------|----------|-------------|
| AGWF | Workflow | Workflow definition validation |
| AGSR | State Reducer | State reducer attribute validation |

### Current Coverage

- **8 workflow diagnostics** (AGWF001-004, AGWF009-010, AGWF012, AGWF014)
- **2 state reducer diagnostics** (AGSR001-002)
- **30+ diagnostic tests** ensuring detection accuracy

---

## Design Philosophy

### 1. Fail Fast, Fail Clear

Diagnostics catch errors at compile time rather than runtime. A workflow with structural issues should never make it to production. Error messages include the workflow name and specific details to enable quick fixes.

### 2. Errors vs Warnings

- **Errors**: Block code generation. The workflow cannot execute correctly.
- **Warnings**: Allow code generation. The pattern may be intentional but warrants review.

### 3. DSL-Aware Validation

The diagnostics understand the fluent DSL structure. They validate:
- Method call order (StartWith before Then)
- Construct pairing (Fork must have Join)
- Content requirements (loops must have bodies)

### 4. Contextual Detection

Some patterns are valid in certain contexts but invalid in others:
- Duplicate step types are errors in linear/fork contexts
- Duplicate step types are allowed in branch contexts (mutually exclusive paths)

---

## Implemented Diagnostics

### AGWF001: Empty Workflow Name

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | DSL Completeness |
| Since | 1.0 |

**Description**: The `[Workflow]` attribute requires a non-empty workflow name.

**Trigger**:
```csharp
[Workflow("")]  // Error: Empty name
[Workflow("   ")]  // Error: Whitespace only
```

**Resolution**: Provide a valid kebab-case workflow name:
```csharp
[Workflow("process-order")]
```

---

### AGWF002: No Steps Found

| Property | Value |
|----------|-------|
| Severity | Warning |
| Category | DSL Completeness |
| Since | 1.0 |

**Description**: The workflow definition doesn't contain any recognizable step methods.

**Trigger**:
```csharp
[Workflow("empty-workflow")]
public static partial class EmptyWorkflow
{
    public static WorkflowDefinition<State> Definition =>
        Workflow<State>.Create("empty-workflow");  // Warning: No steps
}
```

**Resolution**: Add steps using the fluent DSL:
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
| Since | 1.0 |

**Description**: The same step type appears multiple times in contexts where each step must be unique (linear flow, fork paths).

**Trigger**:
```csharp
.StartWith<ValidateStep>()
.Then<ProcessStep>()
.Then<ValidateStep>()  // Error: Duplicate
```

**Resolution**: Use instance names to disambiguate:
```csharp
.StartWith<ValidateStep>()
.Then<ProcessStep>()
.Then<ValidateStep>("FinalValidation")  // OK: Different instance
```

**Note**: Duplicates in mutually exclusive branch paths are allowed since only one path executes.

---

### AGWF004: Invalid Namespace

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | DSL Completeness |
| Since | 1.0 |

**Description**: Workflows must be declared in a namespace. Global namespace is not supported.

**Trigger**:
```csharp
// No namespace declaration
[Workflow("orphan-workflow")]
public static partial class OrphanWorkflow { }  // Error
```

**Resolution**: Declare the workflow in a namespace:
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
| Since | 1.1 |

**Description**: Every workflow must begin with `StartWith<T>()` to define the entry point.

**Trigger**:
```csharp
Workflow<State>.Create("bad-workflow")
    .Then<FirstStep>()  // Error: Should be StartWith
    .Finally<LastStep>();
```

**Resolution**: Use `StartWith<T>()` for the first step:
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
| Since | 1.1 |

**Description**: Workflows should end with `Finally<T>()` to mark completion.

**Trigger**:
```csharp
Workflow<State>.Create("incomplete-workflow")
    .StartWith<FirstStep>()
    .Then<SecondStep>();  // Warning: No Finally
```

**Resolution**: Add a `Finally<T>()` step:
```csharp
Workflow<State>.Create("complete-workflow")
    .StartWith<FirstStep>()
    .Then<SecondStep>()
    .Finally<CompletionStep>();
```

**Why Warning (not Error)**: Some patterns intentionally short-circuit via `Complete()` in branches. The DSL allows this, so we warn rather than block.

---

### AGWF012: Fork without Join

| Property | Value |
|----------|-------|
| Severity | Error |
| Category | Structural Validation |
| Since | 1.1 |

**Description**: Every `Fork()` construct must be followed by `Join<T>()` to merge parallel paths.

**Trigger**:
```csharp
.StartWith<PrepareStep>()
.Fork(
    path => path.Then<PathA>(),
    path => path.Then<PathB>())
.Then<NextStep>()  // Error: Fork not followed by Join
.Finally<EndStep>();
```

**Resolution**: Add a `Join<T>()` after the fork:
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
| Since | 1.1 |

**Description**: `RepeatUntil` loops must contain at least one step in their body.

**Trigger**:
```csharp
.StartWith<InitStep>()
.RepeatUntil(s => s.Done, "process", loop => { })  // Error: Empty body
.Finally<EndStep>();
```

**Resolution**: Add steps to the loop body:
```csharp
.StartWith<InitStep>()
.RepeatUntil(s => s.Done, "process", loop => loop
    .Then<ProcessItemStep>()
    .Then<CheckProgressStep>())
.Finally<EndStep>();
```

---

## Deferred Diagnostics

The following diagnostics were analyzed but deferred from the initial implementation. They fall into two categories: those prevented by the type system, and those requiring complex analysis.

### DSL-Enforced (Not Needed)

These scenarios are prevented by the C# type system and the fluent API design.

#### AGWF011: Invalid Step Type

**Proposed**: Error when step type doesn't implement `IWorkflowStep<TState>`.

**Status**: Deferred - The generic constraints on `StartWith<T>()`, `Then<T>()`, and `Finally<T>()` enforce this at compile time. The C# compiler already reports an error.

#### AGWF013: Branch without Cases

**Proposed**: Warning when `Branch()` has no `BranchCase` arguments.

**Status**: Deferred - The `Branch()` method signature requires at least one `BranchCase` parameter. The C# compiler prevents calling it without arguments.

---

### Require Graph Analysis (Future Consideration)

These diagnostics require building and traversing a workflow graph, which adds significant complexity.

#### AGWF005: Unreachable Steps

**Proposed**: Error when steps can never be reached due to control flow.

**Example**:
```csharp
.Branch(s => s.Type,
    when: TypeA, then: path => path.Complete(),
    otherwise: path => path.Complete())
.Then<UnreachableStep>()  // Never executes
```

**Complexity**: Requires graph traversal to identify nodes with no incoming edges (except entry). The current DSL structure makes this scenario rare, as the fluent API enforces chaining.

**Status**: Deferred - Low priority given DSL design. Consider for future versions if users report issues.

#### AGWF006: Missing Transitions

**Proposed**: Error when a step has no outgoing transition.

**Complexity**: Similar to AGWF005, requires graph analysis. The fluent DSL largely prevents this by design.

**Status**: Deferred - DSL structure makes this nearly impossible to create.

#### AGWF007: Invalid Branch Targets (Enum Exhaustiveness)

**Proposed**: Warning when enum-based branches don't cover all enum values.

**Example**:
```csharp
enum Status { Pending, Approved, Rejected }

.Branch(s => s.Status,
    when: Status.Pending, then: ...,
    when: Status.Approved, then: ...)
    // Missing: Status.Rejected
```

**Complexity**: Requires:
1. Detecting enum discriminator types
2. Extracting all enum values
3. Comparing against provided cases
4. Accounting for `Otherwise` as catch-all

**Status**: Deferred - Nice to have but complex to implement correctly. The `Otherwise` case typically serves as catch-all.

#### AGWF008: Cycle Detection

**Proposed**: Error when non-loop cycles exist in the workflow.

**Complexity**: Requires Tarjan's algorithm or DFS with visited tracking. Must distinguish intentional loops (`RepeatUntil`) from erroneous cycles.

**Status**: Deferred - The fluent DSL doesn't easily allow creating arbitrary cycles. `RepeatUntil` is the only sanctioned looping mechanism.

---

## Diagnostic Categories

### DSL Completeness (AGWF001, AGWF002, AGWF004, AGWF009, AGWF010)

Ensures the workflow definition has all required components:
- Valid name
- Proper namespace
- Entry point (StartWith)
- Exit point (Finally)
- At least one step

### Structural Validation (AGWF003, AGWF012, AGWF014)

Validates the logical structure of the workflow:
- No ambiguous step references (duplicates)
- Proper construct pairing (Fork/Join)
- Non-empty constructs (loop bodies)

---

## Implementation Details

### Detection Architecture

```
WorkflowIncrementalGenerator
    └── TransformToResult()
            ├── FluentDslParser.ValidateStartsWith()    → AGWF009
            ├── FluentDslParser.ValidateEndsWith()      → AGWF010
            ├── FluentDslParser.FindEmptyLoops()        → AGWF014
            ├── ForkModel.JoinStepName check            → AGWF012
            ├── Duplicate detection (context-aware)     → AGWF003
            └── Namespace/name validation               → AGWF001, AGWF004
```

### Key Implementation Files

| File | Responsibility |
|------|----------------|
| `WorkflowDiagnostics.cs` | Diagnostic descriptors (ID, severity, message format) |
| `FluentDslParser.cs` | Validation methods for DSL analysis |
| `WorkflowIncrementalGenerator.cs` | Orchestrates validation and reports diagnostics |
| `InvocationChainWalker.cs` | Traverses fluent method chains |
| `FluentDslParseContext.cs` | Caches parsed syntax for efficiency |

### Error vs Warning Decision Tree

```
Is the workflow executable without this?
├── No  → Error (blocks code generation)
│   Examples: Missing StartWith, Fork without Join
│
└── Yes → Warning (allows code generation)
    Examples: Missing Finally (may use Complete() in branch)
```

---

## Testing Strategy

### Test Pattern

Each diagnostic has 2-3 tests:

1. **Positive Test**: Verify diagnostic IS reported for invalid input
2. **Negative Test**: Verify diagnostic is NOT reported for valid input
3. **Message Test**: Verify diagnostic message contains expected details

### Example Test Structure

```csharp
[Test]
public async Task Diagnostic_ForkWithoutJoin_ReportsAGWF012()
{
    // Arrange - Invalid workflow
    var source = SourceTexts.WorkflowForkWithoutJoin;

    // Act
    var result = GeneratorTestHelper.RunGenerator(source);

    // Assert
    var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == "AGWF012");
    await Assert.That(diagnostic).IsNotNull();
    await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Error);
}
```

### Test Sources

Test source texts are defined in `SourceTexts.cs` with clear naming:
- `WorkflowForkWithoutJoin` - Fork construct missing Join
- `WorkflowEmptyLoopBody` - RepeatUntil with empty body
- `WorkflowMissingFinally` - No Finally step
- `WorkflowMissingStartWith` - Starts with Then instead of StartWith

---

## Future Considerations

### Potential Enhancements

1. **Location Precision**: Currently diagnostics point to the `[Workflow]` attribute. Could point to specific method calls.

2. **Quick Fixes**: IDE-integrated code fixes that auto-correct common issues.

3. **Configurable Severity**: Allow users to promote warnings to errors or suppress specific diagnostics.

4. **Graph-Based Validation**: If demand exists, implement AGWF005/AGWF006/AGWF008 using a `WorkflowGraphAnalyzer`.

### Adding New Diagnostics

1. Add descriptor to `WorkflowDiagnostics.cs`
2. Add detection logic (preferably in `FluentDslParser.cs`)
3. Call detection from `WorkflowIncrementalGenerator.TransformToResult()`
4. Update `hasErrors` if it should block code generation
5. Add tests to `DiagnosticTests.cs`
6. Update this document

---

## References

- [Agentic Workflow Library Design](./design.md) - Parent architecture document
- [Step Deduplication Constraints](./archive/step-deduplication-constraints.md) - AGWF003 context
- [Roslyn Analyzer Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix)
