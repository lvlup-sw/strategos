---
title: "Conditional Workflows"
---

# Conditional Workflows

Real-world workflows rarely follow a single path. Insurance claims need different handling based on type. Order processing varies by customer tier. Content moderation escalates based on severity. This tutorial shows you how to implement conditional routing using the `Branch` DSL.

## What You Will Build

An insurance claim processing workflow that routes claims to different handlers based on claim type:

- **Auto claims** - Process through automated rules engine
- **Property claims** - Require on-site inspection first
- **Other claims** - Route to manual review queue

All paths converge to notify the claimant at the end.

## Step 1: Define the State

The state includes a `ClaimType` field that determines which branch executes:

```csharp
[WorkflowState]
public record ClaimState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public InsuranceClaim Claim { get; init; } = null!;
    public ClaimType ClaimType { get; init; }
    public ClaimAssessment? Assessment { get; init; }
    public InspectionReport? Inspection { get; init; }
    public ClaimDecision? Decision { get; init; }
    public bool ClaimantNotified { get; init; }
}

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

## Step 2: Define the Workflow with Branches

The `Branch` method accepts a selector function and multiple `when` clauses:

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

The branch selector (`state => state.ClaimType`) extracts a value from state. Each `when` clause handles a specific case. The `otherwise` clause catches any unmatched values.

After the branch completes, execution automatically continues to `NotifyClaimant`.

## Branch Patterns

### Simple Value Matching

Match against discrete values like enums:

```csharp
.Branch(state => state.ClaimType,
    when: ClaimType.Auto, then: flow => flow.Then<AutoProcessor>(),
    when: ClaimType.Property, then: flow => flow.Then<PropertyProcessor>(),
    otherwise: flow => flow.Then<DefaultProcessor>())
```

### Boolean Branching

For simple true/false decisions, use the boolean form:

```csharp
.Branch(state => state.Amount > 10000m,
    whenTrue: flow => flow
        .AwaitApproval<SeniorAdjuster>()
        .Then<HighValueProcessor>(),
    whenFalse: flow => flow
        .Then<StandardProcessor>())
```

### Multi-Condition Branching

For complex routing, extract a category from state:

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

### Multi-Step Branches

Each branch can contain multiple steps:

```csharp
.Branch(state => state.ClaimType,
    when: ClaimType.Property, then: flow => flow
        .Then<PropertyInspection>()
        .Then<PropertyClaimProcessor>(),
    // ...
```

## Step 3: Implement the Steps

### AssessClaim

The initial step that determines routing:

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
        var assessment = await _assessor.AssessAsync(state.Claim, ct);

        return state
            .With(s => s.Assessment, assessment)
            .With(s => s.ClaimType, assessment.RecommendedType)
            .AsResult();
    }
}
```

### AutoClaimProcessor

Handles auto claims through automated rules:

```csharp
public class AutoClaimProcessor : IWorkflowStep<ClaimState>
{
    private readonly IAutoClaimEngine _engine;

    public AutoClaimProcessor(IAutoClaimEngine engine)
    {
        _engine = engine;
    }

    public async Task<StepResult<ClaimState>> ExecuteAsync(
        ClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        var decision = await _engine.ProcessAsync(state.Claim, ct);

        return state
            .With(s => s.Decision, decision)
            .AsResult();
    }
}
```

### PropertyInspection

Schedules and awaits physical inspection:

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
        var report = await _inspectionService.ScheduleAndCompleteAsync(
            state.Claim,
            ct);

        return state
            .With(s => s.Inspection, report)
            .AsResult();
    }
}
```

### PropertyClaimProcessor

Processes property claims using inspection findings:

```csharp
public class PropertyClaimProcessor : IWorkflowStep<ClaimState>
{
    private readonly IPropertyClaimEngine _engine;

    public PropertyClaimProcessor(IPropertyClaimEngine engine)
    {
        _engine = engine;
    }

    public async Task<StepResult<ClaimState>> ExecuteAsync(
        ClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        var decision = await _engine.ProcessAsync(
            state.Claim,
            state.Inspection!,
            ct);

        return state
            .With(s => s.Decision, decision)
            .AsResult();
    }
}
```

### ManualReview

Queues non-standard claims for human review:

```csharp
public class ManualReview : IWorkflowStep<ClaimState>
{
    private readonly IManualReviewQueue _queue;

    public ManualReview(IManualReviewQueue queue)
    {
        _queue = queue;
    }

    public async Task<StepResult<ClaimState>> ExecuteAsync(
        ClaimState state,
        StepContext context,
        CancellationToken ct)
    {
        var decision = await _queue.SubmitAndAwaitDecisionAsync(
            state.Claim,
            ct);

        return state
            .With(s => s.Decision, decision)
            .AsResult();
    }
}
```

### NotifyClaimant

The final step that all branches converge to:

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

## Understanding Generated Artifacts

### Phase Enum

The generator creates phases for all possible steps:

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

The generated transition table shows all valid paths:

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

Use this table for visualization, validation, and debugging.

## Key Points

- **Branches automatically rejoin** at the next step after the branch block
- **Always include `otherwise`** to handle unmatched values and avoid runtime exceptions
- **Branch paths can have multiple steps** chained with `Then()`
- **Branch conditions are evaluated against current state** - update state before branching
- **Generated transition tables** show all valid paths through the workflow

## Next Steps

You have learned how to route workflows conditionally. Sometimes you need steps to run concurrently:

- [Parallel Execution](./parallel) - Run independent analyses simultaneously
- [Loops](./loops) - Repeat until quality thresholds are met
- [Approvals](./approvals) - Pause for human review and sign-off
