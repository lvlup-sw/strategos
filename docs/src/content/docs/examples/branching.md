---
title: "Branching: Conditional Routing in Workflows"
---

# Branching: Conditional Routing in Workflows

## The Problem: One Size Doesn't Fit All

Your insurance company processes claims. Auto claims need vehicle inspection. Property claims need on-site assessment. Health claims need medical review. Each type follows a different path.

**The naive approach**: Giant switch statement in a single handler:

```csharp
// This quickly becomes unmaintainable
if (claimType == "Auto") { /* 50 lines of auto logic */ }
else if (claimType == "Property") { /* 60 lines of property logic */ }
else if (claimType == "Health") { /* 40 lines of health logic */ }
else { /* fallback */ }
```

**Problems**:
- All logic in one place (no separation of concerns)
- Hard to test individual paths
- No visibility into which path was taken
- Adding new claim types means modifying existing code

**What you need**: A workflow that:
1. Routes to different step sequences based on state
2. Keeps each path as a separate, testable unit
3. Automatically rejoins after the branch
4. Generates a transition table showing all valid paths

This is the **Branch** pattern—conditional routing with automatic rejoining.

---

## Learning Objectives

After this example, you will understand:

- **Branch routing** based on state values
- **Value matching** with `when` clauses
- **Boolean branching** for true/false decisions
- **Otherwise clauses** for handling unmatched values
- **Multi-step branches** for complex paths
- **Generated transition tables** showing valid paths

---

## Conceptual Foundation

### Branching vs. If-Else

Traditional branching uses if-else or switch statements:

```csharp
// Traditional - all in one place
switch (claimType)
{
    case "Auto": return ProcessAutoClaim(claim);
    case "Property": return ProcessPropertyClaim(claim);
    default: return ProcessManualReview(claim);
}
```

Workflow branching is declarative:

```csharp
// Declarative - separate paths, automatic rejoining
.Branch(state => state.ClaimType,
    when: ClaimType.Auto, then: flow => flow.Then<AutoClaimProcessor>(),
    when: ClaimType.Property, then: flow => flow
        .Then<PropertyInspection>()
        .Then<PropertyClaimProcessor>(),
    otherwise: flow => flow.Then<ManualReview>())
.Finally<NotifyClaimant>()  // All paths rejoin here
```

**Why declarative branching?**

| Aspect | Traditional | Declarative |
|--------|-------------|-------------|
| **Visibility** | Hidden in code | Visible in workflow definition |
| **Testing** | Test entire switch | Test each path independently |
| **Rejoining** | Manual bookkeeping | Automatic |
| **Valid paths** | Not explicit | Generated transition table |

### The Branch Selector

The branch selector extracts a value from state to determine routing:

```csharp
.Branch(state => state.ClaimType,  // Selector extracts ClaimType
    when: ClaimType.Auto, then: ...,
    when: ClaimType.Property, then: ...,
    otherwise: ...)
```

The selector can return:
- **Enum values** (most common)
- **Booleans** (for simple true/false)
- **Strings** (for dynamic routing)
- **Any type** with equality comparison

### Otherwise: The Safety Net

Always include an `otherwise` clause:

```csharp
.Branch(state => state.ClaimType,
    when: ClaimType.Auto, then: flow => flow.Then<AutoProcessor>(),
    when: ClaimType.Property, then: flow => flow.Then<PropertyProcessor>(),
    otherwise: flow => flow.Then<ManualReview>())  // Catches unexpected values
```

**Why?**
- New enum values won't crash the workflow
- Explicit fallback behavior is documented
- No silent failures

---

## Design Decisions

| Decision | Why This Approach | Alternative | Trade-off |
|----------|-------------------|-------------|-----------|
| **Enum-based selectors** | Type-safe, exhaustive | Strings | Compile-time checking |
| **Mandatory otherwise** | No runtime exceptions | Optional | More code, but safer |
| **Auto-rejoin after branch** | Simpler mental model | Manual rejoin | Less flexible, but clearer |
| **Multi-step paths** | Complex paths supported | Single step per branch | More power |

### When to Use This Pattern

**Good fit when**:
- Different inputs need different processing
- Paths are mutually exclusive
- Paths should rejoin after branch-specific logic
- You want visibility into routing decisions

**Poor fit when**:
- Routing logic is highly dynamic (use custom step)
- Paths don't rejoin (use separate workflows)
- Simple transformation (use single step with logic)

### Anti-Patterns to Avoid

| Anti-Pattern | Problem | Correct Approach |
|--------------|---------|------------------|
| **No otherwise** | Runtime exceptions on new values | Always provide otherwise |
| **Overlapping conditions** | Unpredictable routing | Use mutually exclusive values |
| **Giant branch paths** | Hard to test | Keep paths focused, use composition |
| **Dynamic string selectors** | No compile-time safety | Prefer enums |
| **Nested branches** | Complex, hard to follow | Flatten or use sub-workflows |

---

## Building the Workflow

### The Shape First

```text
                              ┌─────────────────────┐
                              │ AutoClaimProcessor  │
                         ┌───▶│                     │────┐
                         │    └─────────────────────┘    │
                         │                               │
┌─────────────┐    [Auto]│    ┌──────────────────────┐   │   ┌────────────────┐
│ AssessClaim │──────────┤    │ PropertyInspection   │   ├──▶│ NotifyClaimant │
│             │          │    │         ↓            │   │   │                │
│ Classify    │ [Property]───▶│ PropertyClaimProcessor│──┘   │ Send decision  │
│ the claim   │          │    └──────────────────────┘       │ to claimant    │
│             │          │                                   └────────────────┘
└─────────────┘  [Other] │    ┌─────────────────────┐
                         └───▶│ ManualReview        │────┘
                              │                     │
                              └─────────────────────┘
```

### State: What We Track

```csharp
[WorkflowState]
public record ClaimState : IWorkflowState
{
    // Identity
    public Guid WorkflowId { get; init; }

    // Input claim
    public InsuranceClaim Claim { get; init; } = null!;

    // Classification result (determines routing)
    public ClaimType ClaimType { get; init; }

    // Branch-specific outputs
    public ClaimAssessment? Assessment { get; init; }
    public InspectionReport? Inspection { get; init; }  // Property only

    // Final decision (set by all paths)
    public ClaimDecision? Decision { get; init; }

    // Notification status
    public bool ClaimantNotified { get; init; }
}
```

**Why this design?**

- `ClaimType`: The routing discriminator, set by `AssessClaim`
- `Inspection`: Only populated by the Property path
- `Decision`: Set by all paths—the common output

### The Supporting Records

```csharp
public record InsuranceClaim(
    string ClaimantId,
    string PolicyNumber,
    decimal Amount,
    string Description);

public record ClaimAssessment(
    ClaimType RecommendedType,
    decimal Confidence,
    string Rationale);

public record InspectionReport(
    string InspectorId,
    DateOnly InspectionDate,
    string Findings);

public record ClaimDecision(
    bool Approved,
    decimal ApprovedAmount,
    string Reason);

public enum ClaimType { Auto, Property, Health, Other }
```

### The Workflow Definition

```csharp
var workflow = Workflow<ClaimState>
    .Create("process-claim")
    .StartWith<AssessClaim>()
    .Branch(state => state.ClaimType,
        when: ClaimType.Auto, then: flow => flow
            .Then<AutoClaimProcessor>(),
        when: ClaimType.Property, then: flow => flow
            .Then<PropertyInspection>()
            .Then<PropertyClaimProcessor>(),
        otherwise: flow => flow
            .Then<ManualReview>())
    .Finally<NotifyClaimant>();
```

**Reading this definition**:
1. Assess the claim (classify it)
2. Branch based on claim type:
   - Auto claims → process automatically
   - Property claims → inspect, then process
   - Everything else → manual review
3. All paths rejoin at NotifyClaimant

### Branch Patterns

**Simple value matching**:
```csharp
.Branch(state => state.ClaimType,
    when: ClaimType.Auto, then: flow => flow.Then<AutoProcessor>(),
    when: ClaimType.Property, then: flow => flow.Then<PropertyProcessor>(),
    otherwise: flow => flow.Then<DefaultProcessor>())
```

**Boolean branching** (true/false decisions):
```csharp
.Branch(state => state.Amount > 10000m,
    whenTrue: flow => flow
        .AwaitApproval<SeniorAdjuster>()
        .Then<HighValueProcessor>(),
    whenFalse: flow => flow
        .Then<StandardProcessor>())
```

**Complex routing logic** (using a helper method):
```csharp
.Branch(state => ClassifyRisk(state),
    when: RiskLevel.Low, then: flow => flow.Then<AutoApprove>(),
    when: RiskLevel.Medium, then: flow => flow.Then<StandardReview>(),
    when: RiskLevel.High, then: flow => flow
        .Then<DetailedAnalysis>()
        .AwaitApproval<RiskCommittee>(),
    otherwise: flow => flow.Then<EscalateToManagement>())

private static RiskLevel ClassifyRisk(ClaimState state)
{
    if (state.Amount < 1000m) return RiskLevel.Low;
    if (state.Amount < 10000m) return RiskLevel.Medium;
    return RiskLevel.High;
}
```

### The Classification Step

```csharp
public class AssessClaim : IWorkflowStep<ClaimState>
{
    private readonly IClaimAssessor _assessor;

    public AssessClaim(IClaimAssessor assessor)
    {
        _assessor = assessor;
    }

    public async Task<StepResult<ClaimState>> ExecuteAsync(
        ClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        // AI or rules engine classifies the claim
        var assessment = await _assessor.AssessAsync(state.Claim, ct);

        // Set the routing discriminator
        return state
            .With(s => s.Assessment, assessment)
            .With(s => s.ClaimType, assessment.RecommendedType)
            .AsResult();
    }
}
```

**The key insight**: This step sets `ClaimType`, which determines which branch path executes.

### A Branch Path Step: PropertyInspection

```csharp
public class PropertyInspection : IWorkflowStep<ClaimState>
{
    private readonly IInspectionService _inspectionService;

    public PropertyInspection(IInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    public async Task<StepResult<ClaimState>> ExecuteAsync(
        ClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        // Schedule and complete inspection (might be async over days)
        var report = await _inspectionService.ScheduleAndCompleteAsync(
            state.Claim,
            ct);

        return state
            .With(s => s.Inspection, report)
            .AsResult();
    }
}
```

### The Rejoin Step

```csharp
public class NotifyClaimant : IWorkflowStep<ClaimState>
{
    private readonly INotificationService _notifications;

    public NotifyClaimant(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<StepResult<ClaimState>> ExecuteAsync(
        ClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        // All paths have set Decision by now
        await _notifications.SendClaimDecisionAsync(
            state.Claim.ClaimantId,
            state.Decision!,
            ct);

        return state
            .With(s => s.ClaimantNotified, true)
            .AsResult();
    }
}
```

**Note**: This step assumes `Decision` is set. All branch paths must populate it.

---

## Generated Artifacts

### Phase Enum

```csharp
public enum ProcessClaimPhase
{
    NotStarted,
    AssessClaim,
    AutoClaimProcessor,
    PropertyInspection,
    PropertyClaimProcessor,
    ManualReview,
    NotifyClaimant,
    Completed,
    Failed
}
```

### Transition Table

The generator produces a transition table showing valid paths:

```csharp
public static class ProcessClaimTransitions
{
    public static readonly IReadOnlyDictionary<ProcessClaimPhase, ProcessClaimPhase[]> Valid =
        new Dictionary<ProcessClaimPhase, ProcessClaimPhase[]>
        {
            [ProcessClaimPhase.AssessClaim] = [
                ProcessClaimPhase.AutoClaimProcessor,
                ProcessClaimPhase.PropertyInspection,
                ProcessClaimPhase.ManualReview
            ],
            [ProcessClaimPhase.AutoClaimProcessor] = [ProcessClaimPhase.NotifyClaimant],
            [ProcessClaimPhase.PropertyInspection] = [ProcessClaimPhase.PropertyClaimProcessor],
            [ProcessClaimPhase.PropertyClaimProcessor] = [ProcessClaimPhase.NotifyClaimant],
            [ProcessClaimPhase.ManualReview] = [ProcessClaimPhase.NotifyClaimant],
            [ProcessClaimPhase.NotifyClaimant] = [ProcessClaimPhase.Completed],
        };
}
```

This table makes routing decisions explicit and verifiable.

---

## The "Aha Moment"

> **Branches are declarative routing, not imperative control flow.**
>
> When you write `Branch(state => state.ClaimType, ...)`, you're not writing if-else logic—you're declaring a routing table. The generated transition table proves what paths are valid. The workflow definition documents what happens to each claim type.
>
> Six months from now, when someone asks "what happens to a property claim?", you can point to the workflow definition—not grep through hundreds of lines of code.

---

## Extension Exercises

### Exercise 1: Add Health Claims Path

Add a dedicated path for health claims:

1. Add `ClaimType.Health` to the enum
2. Create `HealthClaimReview` step
3. Create `MedicalRecordsVerification` step
4. Add the path: `Health → HealthClaimReview → MedicalRecordsVerification`

### Exercise 2: Add Risk-Based Approval

Require approval for high-value claims:

1. Add `RequiresApproval` computed property
2. Create nested branch: `if (Amount > 50000m) → AwaitApproval`
3. Otherwise continue to processor

### Exercise 3: Add Fraud Detection

Add a fraud check that can short-circuit any path:

1. Create `FraudCheck` step that runs before branching
2. If fraud detected, route to `InvestigationQueue` (skip all normal paths)
3. Use a boolean branch around the main claim type branch

---

## Key Takeaways

1. **Branch routing is declarative**—visible in workflow definition
2. **Always include otherwise**—handles unexpected values safely
3. **Paths automatically rejoin**—no manual bookkeeping
4. **Multi-step paths supported**—complex branch logic is fine
5. **Transition tables are generated**—valid paths are explicit
6. **The selector determines routing**—keep it simple and testable

---

## Related

- [Basic Workflow](./basic-workflow.md) - Sequential steps without branching
- [Fork/Join Pattern](./fork-join.md) - Parallel execution (not exclusive like branching)
- [Approval Flow Pattern](./approval-flow.md) - Human checkpoints within branches
