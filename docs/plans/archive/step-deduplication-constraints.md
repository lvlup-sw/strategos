# Step Deduplication Architectural Constraints

**Status:** Implemented
**Date:** 2024-12-26
**Category:** Source Generator Architecture

## Executive Summary

The Strategos source generators implement step deduplication during DSL parsing to support branch constructs where the same step type legitimately appears in multiple exclusive paths. However, this deduplication masks a class of developer errors where the same step type is incorrectly used multiple times in contexts where Wolverine's message-based routing cannot distinguish between occurrences.

This document describes the architectural constraint, its implications, and potential remediation strategies.

---

## Background

### Wolverine Saga Message Routing

Wolverine sagas route messages to handlers based on **message type**, not instance identity. When a saga defines a handler:

```csharp
public StartNextStepCommand Handle(ValidateStepCompleted evt)
{
    // Route to next step after validation completes
}
```

There is exactly ONE handler for `ValidateStepCompleted` events. The saga cannot distinguish between:
- A first occurrence of `ValidateStep` completing
- A second occurrence of `ValidateStep` completing
- `ValidateStep` completing in fork path A vs fork path B

This is a fundamental constraint of message-passing architectures.

### Step Name as Phase Identity

In the generated saga, each step corresponds to a **phase** in the workflow state machine. The phase name is derived from the step type name (with loop prefixes for nested contexts):

| DSL Pattern | Phase Name | Event Type |
|-------------|------------|------------|
| `.Then<ValidateStep>()` | `ValidateStep` | `ValidateStepCompleted` |
| `.RepeatUntil("Refine", ...).Then<ValidateStep>()` | `Refine_ValidateStep` | `ValidateStepCompleted` |

Note that while the **phase name** includes the loop prefix, the **event type** is always based on the base step type name. This is because workers are generated per step TYPE, not per phase.

---

## Current Behavior

### Deduplication Location

Step deduplication occurs in `StepExtractor.cs` at lines 67-68:

```csharp
// Deduplicate by PhaseName - same step may appear in multiple branch paths
return steps.GroupBy(s => s.PhaseName).Select(g => g.First()).ToList();
```

### Why Deduplication Exists

Deduplication was introduced to support **Branch constructs** where the same step type legitimately appears in different exclusive paths:

```csharp
.Branch(state => state.Priority,
    high => high.Then<ValidateStep>().Then<ProcessStep>(),
    low => low.Then<ValidateStep>().Then<QueueStep>())
.Rejoin<FinalizeStep>()
```

In this example, `ValidateStep` appears in both the `high` and `low` paths. However, at runtime, only ONE path executes based on the discriminator value. Therefore:
- Only one `ValidateStepCompleted` event will ever be produced per workflow execution
- The saga handler for `ValidateStepCompleted` will work correctly
- Deduplication is appropriate to avoid generating duplicate handlers

### The Problem

The same deduplication logic is applied globally, affecting scenarios where duplicates are **NOT acceptable**:

1. **Linear Flow Duplicates:**
   ```csharp
   .StartWith<ValidateStep>()
   .Then<ProcessStep>()
   .Then<ValidateStep>()  // Second occurrence - ERROR
   .Finally<CompleteStep>()
   ```

2. **Fork Path Duplicates:**
   ```csharp
   .Fork(
       path => path.Then<AnalyzeStep>(),      // Path 0
       path => path.Then<AnalyzeStep>())      // Path 1 - same step type!
   .Join<SynthesizeStep>()
   ```

In both cases, the second occurrence is **silently dropped** rather than reported as an error.

---

## Impact Analysis

### Silent Correctness Bug

When a developer writes:
```csharp
.StartWith<ValidateStep>()
.Then<TransformStep>()
.Then<ValidateStep>()     // Intended: re-validate after transformation
.Finally<PersistStep>()
```

The generated workflow executes:
```
ValidateStep → TransformStep → PersistStep
```

The second `ValidateStep` is never executed. The workflow does not match the developer's intent.

### Diagnostic Infrastructure Exists But Never Triggers

The generator includes `AGWF003` diagnostic for duplicate step detection:

```csharp
// WorkflowDiagnostics.cs
public static readonly DiagnosticDescriptor DuplicateStepName = new(
    id: "AGWF003",
    title: "Duplicate step name",
    messageFormat: "Step '{0}' appears multiple times in workflow '{1}'",
    category: Category,
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true);
```

However, the duplicate check in `WorkflowIncrementalGenerator.cs` runs AFTER deduplication:

```csharp
var stepNames = FluentDslParser.ExtractStepNames(...);  // Already deduplicated

// This check will NEVER find duplicates
var duplicateSteps = stepNames
    .GroupBy(s => s)
    .Where(g => g.Count() > 1)
    .ToList();
```

---

## Scenario Analysis

| Scenario | Runtime Behavior | Current Handling | Correct Handling |
|----------|------------------|------------------|------------------|
| Same step twice in linear flow | Non-deterministic routing | Silent deduplication | **AGWF003 error** |
| Same step in different fork paths | Both paths produce same event, saga confused | Silent deduplication | **AGWF003 error** |
| Same step in different branch paths | Only one path executes | Silent deduplication | OK (deduplicate) |
| Same step in loop body (each iteration) | Each iteration produces same event | OK (single handler) | OK |

### Why Fork Paths Differ from Branch Paths

- **Branch paths** are mutually exclusive - only ONE path executes per workflow run
- **Fork paths** execute in PARALLEL - ALL paths execute concurrently

When fork paths complete:
```
Path 0: AnalyzeStep → (produces AnalyzeStepCompleted)
Path 1: AnalyzeStep → (produces AnalyzeStepCompleted)  // Same event type!
```

The saga receives two `AnalyzeStepCompleted` events but has one handler. The handler executes twice, but path status tracking becomes corrupted because both events update the same path's status (whichever the handler is coded for).

---

## Risk Assessment

### Likelihood: Low to Medium

The fluent DSL syntax makes duplicate steps visually obvious:

```csharp
// Very visible - developer likely to notice
.Then<ValidateStep>()
.Then<ProcessStep>()
.Then<ValidateStep>()  // Immediately follows similar pattern
```

However, in complex workflows with many steps, duplicates may be less obvious.

### Severity: High

When duplicates occur, the workflow silently executes incorrectly. There is no error, no warning, and no indication that the behavior differs from the DSL definition. This violates the principle of least surprise and can cause hard-to-debug production issues.

### Overall Risk: Medium

Low likelihood × High severity = Medium overall risk.

---

## Remediation Options

### Option 1: Context-Aware Duplicate Detection (Recommended)

Refactor `StepExtractor` to track step context during collection:

```csharp
internal sealed record StepInfo(
    string StepName,
    string? LoopName = null,
    StepContext Context = StepContext.Linear)
{
    public string PhaseName => LoopName is null ? StepName : $"{LoopName}_{StepName}";
}

internal enum StepContext
{
    Linear,      // Main workflow flow
    ForkPath,    // Inside a Fork path
    BranchPath   // Inside a Branch path (exclusive)
}
```

Detection logic:
1. Collect all steps with their context
2. Group by `(StepName, Context)` combinations
3. Report AGWF003 for duplicates where context is `Linear` or `ForkPath`
4. Deduplicate only for `BranchPath` duplicates

**Complexity:** Medium
**Breaking Changes:** None (only adds error reporting)

### Option 2: Unique Step Type Requirement

Enforce that each step type appears AT MOST ONCE across the entire workflow:

- Simplest implementation
- May be overly restrictive for valid branch patterns
- Would require developers to create wrapper step types for reuse

**Complexity:** Low
**Breaking Changes:** Would break existing workflows using same step in different branches

### Option 3: Instance-Based Step Naming

Allow developers to specify instance names:

```csharp
.Then<ValidateStep>(name: "PreValidation")
.Then<ProcessStep>()
.Then<ValidateStep>(name: "PostValidation")
```

This would generate distinct phase names and event types:
- `PreValidationCompleted`
- `PostValidationCompleted`

**Complexity:** High (requires DSL changes, worker generation changes)
**Breaking Changes:** API additions, not breaking

### Option 4: Documentation Only (Current State)

Document the limitation and rely on developers to avoid the pattern.

**Complexity:** None
**Breaking Changes:** None
**Risk:** Developers may hit the issue without understanding it

---

## Decision

**Selected: Option 1 (Context-Aware Duplicate Detection) + Option 3 (Instance-Based Step Naming)**

Both phases implemented as of 2024-12-26.

### Phase 1: Context-Aware Detection

The source generator now implements context-aware duplicate detection:

1. **Linear flow duplicates** → AGWF003 error reported
2. **Fork path duplicates** → AGWF003 error reported
3. **Branch path duplicates** → Allowed (exclusive execution - only one path runs)

**Implementation Details:**
- Added `StepContext` enum with `Linear`, `ForkPath`, and `BranchPath` values
- Added `ExtractRawStepInfos()` method that returns steps WITHOUT deduplication
- Updated `WorkflowIncrementalGenerator.cs` to use context-aware duplicate detection
- Steps in non-`BranchPath` contexts are checked for duplicates; duplicates trigger AGWF003

**Test Coverage (Phase 1):**
- `Diagnostic_DuplicateInLinearFlow_ReportsAGWF003` - Linear duplicates are errors
- `Diagnostic_DuplicateInForkPaths_ReportsAGWF003` - Fork path duplicates are errors
- `Diagnostic_DuplicateAcrossLinearAndFork_ReportsAGWF003` - Cross-context duplicates are errors
- `Diagnostic_DuplicateInBranchPaths_NoDiagnostic` - Branch duplicates are allowed

### Phase 2: Instance-Based Step Naming

Developers can now reuse the same step type with different instance names:

```csharp
.Fork(
    path => path.Then<AnalyzeStep>("Technical"),
    path => path.Then<AnalyzeStep>("Fundamental"))
.Join<SynthesizeStep>()
```

This generates:
- **Phases:** `Technical`, `Fundamental` (distinct identities)
- **Handler:** ONE `AnalyzeStepHandler` (shared by step TYPE)
- **Commands:** ONE `ExecuteAnalyzeStepWorkerCommand` (shared by step TYPE)
- **Events:** ONE `AnalyzeStepCompleted` (shared by step TYPE)

**Implementation Details:**
- Added `InstanceName` property to `StepDefinition` (runtime library)
- Added `Then<T>(string? instanceName)` overloads to all builder interfaces
- Added `InstanceName` and `EffectiveName` to `StepInfo` and `StepModel`
- `EffectiveName` = `InstanceName ?? StepName` - used for duplicate detection and phase naming
- `StepName` - used for handler/command/event deduplication (shared by type)
- Added Fork handling to `WalkInvocationChainForStepModels` for proper handler generation

**Test Coverage (Phase 2):**
- `Diagnostic_InstanceNamedSteps_NoDuplicate` - Different instance names bypass AGWF003
- `Diagnostic_SameInstanceName_StillReportsAGWF003` - Same instance names still error
- `Diagnostic_InstanceNameMatchesStepName_ReportsAGWF003` - Matching names still error
- `Generator_WorkflowWithInstanceNames_*` - 6 integration tests for end-to-end behavior
- `Emit_WithInstanceNames_*` - Unit tests for emitters

---

## Implementation Notes

### Files Modified

1. **`StepExtractor.cs`**
   - Added `StepContext` enum with `Linear`, `ForkPath`, `BranchPath` values
   - Added `Context` property to `StepInfo` record
   - Added `ExtractRawStepInfos()` method (returns steps WITHOUT deduplication)
   - Added `WalkInvocationChainWithLoopsAndContext()` for context-aware traversal
   - Added `ParseForkPathStepsWithContext()` for fork path step extraction
   - Added `ParseBranchPathStepsWithContext()` for branch path step extraction

2. **`FluentDslParser.cs`**
   - Added `ExtractRawStepInfos()` facade method
   - Existing `ExtractStepNames()` unchanged for backwards compatibility

3. **`WorkflowIncrementalGenerator.cs`**
   - Updated duplicate detection to use `ExtractRawStepInfos()`
   - Filters to non-`BranchPath` contexts before duplicate check
   - Reports AGWF003 for duplicates in Linear and ForkPath contexts

### Test Coverage

See `src/Strategos.Generators.Tests/`:
- `DiagnosticTests.cs` - Four new duplicate detection tests
- `Helpers/StepExtractorContextTests.cs` - Six context tracking tests

---

## References

- `src/Strategos.Generators/Helpers/StepExtractor.cs` - StepContext enum, ExtractRawStepInfos()
- `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs` - AGWF003 definition
- `src/Strategos.Generators/WorkflowIncrementalGenerator.cs:283-315` - Context-aware duplicate detection
- `src/Strategos.Generators/FluentDslParser.cs:89-99` - ExtractRawStepInfos() facade
- `src/Strategos.Generators.Tests/DiagnosticTests.cs` - Duplicate detection tests
- `src/Strategos.Generators.Tests/Helpers/StepExtractorContextTests.cs` - Context tracking tests

---

## Changelog

| Date | Author | Change |
|------|--------|--------|
| 2024-12-26 | Claude | Initial documentation of architectural constraint |
| 2024-12-26 | Claude | Implemented Option 1 (Context-Aware Detection) with full test coverage |
| 2024-12-26 | Claude | Implemented Option 3 (Instance-Based Step Naming) with full test coverage |
