---
title: "Approval Flow: Human-in-the-Loop Workflows"
---

# Approval Flow: Human-in-the-Loop Workflows

## The Problem: AI Shouldn't Act Alone

Your AI system generates a legal document, makes a financial decision, or prepares content for publication. Do you just let it act?

**The dangerous approach**: AI generates → auto-execute. Problems:
- No human verification of AI judgment
- Legal liability when AI makes mistakes
- No audit trail showing who approved what
- Impossible to catch errors before they cause harm

**What you need**: A workflow that:
1. Lets AI do the heavy lifting (drafting, analysis)
2. Pauses for human review at critical decision points
3. Handles timeouts gracefully (humans forget)
4. Supports rejection and re-work cycles
5. Records who approved what, when, and why

This is the **AwaitApproval** pattern—human checkpoints in automated workflows.

---

## Learning Objectives

After this example, you will understand:

- **Human approval gates** that pause workflow execution
- **Timeout handling** for unresponsive approvers
- **Escalation paths** when approvals don't arrive
- **Rejection cycles** that route back for re-work
- **State persistence** across hours or days of waiting

---

## Conceptual Foundation

### The Human-AI Collaboration Spectrum

Where should humans be in the loop?

| Approach | Human Role | Risk Level | Speed |
|----------|------------|------------|-------|
| **Human-only** | Does everything | Lowest | Slowest |
| **AI-assisted** | Reviews AI output before action | Balanced | Moderate |
| **AI-monitored** | Notified after AI acts | Higher | Fast |
| **AI-autonomous** | No human involvement | Highest | Fastest |

Approval flows implement **AI-assisted**: AI does the work, humans make the final call.

### Why Pause for Humans?

Human approval isn't bureaucracy—it's risk management:

| Concern | Why Pause? |
|---------|------------|
| **Liability** | Who's responsible when AI makes mistakes? The approver. |
| **Brand safety** | AI might miss tone, context, or cultural sensitivities |
| **Compliance** | Regulations may require human review (financial, medical, legal) |
| **Judgment** | Some decisions require human values, not just optimization |

### The Persistence Challenge

Unlike instant operations, approvals can take hours or days:

```text
10:00 AM - Workflow reaches approval step
10:01 AM - Notification sent to approver
           [Workflow pauses, state persisted]
           ...
           [Approver at lunch, in meetings, on vacation...]
           ...
3:15 PM  - Approver reviews and approves
3:15 PM  - Workflow resumes exactly where it paused
```

**Key insight**: The workflow state must survive process restarts, deployments, and server crashes while waiting for human input.

### Timeout: What If They Never Respond?

Humans are unreliable. They forget, go on vacation, or leave the company. Workflows need timeout strategies:

| Strategy | When | Outcome |
|----------|------|---------|
| **Fail** | Approval is critical | Workflow fails, manual intervention required |
| **Escalate** | Someone else can approve | Routes to backup approver |
| **Auto-approve** | Low-risk decisions | Proceeds automatically (with logging) |
| **Remind** | Approver needs nudging | Sends reminders before timeout |

### Rejection: The Re-Work Cycle

Approval isn't always "yes." Rejections route back for revision:

```text
┌──────────────────────────────────────────────────────────────────┐
│                                                                  │
│    ┌─────────┐    ┌──────────┐    ┌────────────┐    ┌─────────┐ │
│    │  Draft  │───▶│  Review  │───▶│   Await    │───▶│ Publish │ │
│    └─────────┘    └──────────┘    │  Approval  │    └─────────┘ │
│                                   └────────────┘                 │
│                                         │                        │
│                                   [Rejected]                     │
│                                         │                        │
│                                         ▼                        │
│                                  ┌────────────┐                  │
│                                  │  Address   │                  │
│                                  │  Concerns  │──────────────────┘
│                                  └────────────┘
│                                        │
└────────────────────────────────────────┘ (loop until approved)
```

---

## Design Decisions

| Decision | Why This Approach | Alternative | Trade-off |
|----------|-------------------|-------------|-----------|
| **Explicit approval step** | Clear checkpoint | Implicit approval via no-response | Slower, but explicit accountability |
| **Timeout with escalation** | Business continuity | Fail on timeout | May bypass original approver |
| **Rejection loops back** | Iterative improvement | Fail on rejection | May loop indefinitely |
| **State persists during wait** | Reliability | In-memory wait | Survives restarts |

### When to Use This Pattern

**Good fit when**:
- Actions have external consequences (publishing, payments, contracts)
- Regulatory compliance requires human review
- Decisions involve judgment, not just computation
- Audit trails are required

**Poor fit when**:
- Speed matters more than review (real-time systems)
- Decisions are fully deterministic (no judgment needed)
- No humans are available in the workflow
- Actions are easily reversible

### Anti-Patterns to Avoid

| Anti-Pattern | Problem | Correct Approach |
|--------------|---------|------------------|
| **No timeout** | Workflows wait forever | Always configure timeout with handling |
| **Silent failure** | No one knows workflow is stuck | Send notifications, log state |
| **Approval without context** | Approver can't make informed decision | Include all relevant information |
| **No rejection path** | Rejection = workflow death | Route rejections to revision step |
| **Mutable approval state** | Can't prove what was approved | Immutable approval records |

---

## Building the Workflow

### The Shape First

```text
┌───────────────┐    ┌─────────────┐    ┌───────────────────────────┐
│ DraftDocument │───▶│ LegalReview │───▶│    AwaitApproval          │
│               │    │             │    │    <LegalTeam>            │
│ AI generates  │    │ AI analyzes │    │                           │
│ draft         │    │ legal issues│    │ [Timeout: 2 days]         │
│               │    │             │    │ [OnTimeout: Escalate]     │
│               │    │             │    │ [OnRejection: AddressFix] │
└───────────────┘    └─────────────┘    └───────────────────────────┘
                                                    │
                     ┌──────────────────────────────┼──────────────────────────┐
                     │                              │                          │
                     ▼                              ▼                          ▼
            [Approved]                      [Rejected]                   [Timeout]
                     │                              │                          │
                     ▼                              ▼                          ▼
            ┌─────────────────┐           ┌─────────────────┐        ┌─────────────────┐
            │ PublishDocument │           │ AddressConcerns │        │   EscalateToMgr │
            └─────────────────┘           └─────────────────┘        └─────────────────┘
                     │                              │                          │
                     ▼                              ▼                          │
            ┌─────────────────┐           (back to LegalReview)                │
            │NotifyStakeholder│                                                │
            └─────────────────┘                                                │
                                                                               │
                                                    ┌──────────────────────────┘
                                                    ▼
                                           (continues to Publish)
```

### State: What We Track

```csharp
[WorkflowState]
public record DocumentState : IWorkflowState
{
    // Identity
    public Guid WorkflowId { get; init; }

    // Input document
    public Document Document { get; init; } = null!;

    // Draft content (AI-generated)
    public string? DraftContent { get; init; }

    // Legal analysis (AI-generated)
    public LegalReviewResult? LegalReview { get; init; }

    // Human approval decision
    public ApprovalDecision? Approval { get; init; }

    // Workflow status flags
    public bool IsPublished { get; init; }
    public bool StakeholdersNotified { get; init; }
    public bool IsEscalated { get; init; }
}
```

**Why this design?**

- `LegalReview`: AI's analysis, shown to human approver for context
- `Approval`: Human decision, captures who/when/why
- `IsEscalated`: Flag for timeout handling path

### The Supporting Records

```csharp
public record Document(
    string Title,
    string Author,
    DocumentType Type,
    string Content);

public record LegalReviewResult(
    bool HasIssues,                    // Did AI find problems?
    IReadOnlyList<string> Issues,      // What problems?
    string ReviewerComments);          // AI's analysis

public record ApprovalDecision(
    bool Approved,                     // Yes or no
    string ApproverId,                 // WHO approved
    DateTimeOffset DecisionTime,       // WHEN they approved
    string? Comments);                 // WHY (for rejections)

public enum DocumentType { Contract, Policy, Procedure, Marketing }
```

### The Workflow Definition

```csharp
var workflow = Workflow<DocumentState>
    .Create("document-approval")
    .StartWith<DraftDocument>()
    .Then<LegalReview>()
    .AwaitApproval<LegalTeam>(options => options
        .WithTimeout(TimeSpan.FromDays(2))
        .OnTimeout(flow => flow.Then<EscalateToManager>())
        .OnRejection(flow => flow
            .Then<AddressLegalConcerns>()
            .Then<LegalReview>()))
    .Then<PublishDocument>()
    .Finally<NotifyStakeholders>();
```

**Reading this definition**:
1. Draft the document (AI)
2. Legal review (AI analysis)
3. Await approval from LegalTeam
   - If approved: continue to publish
   - If timeout (2 days): escalate to manager, then publish
   - If rejected: address concerns, re-review, request approval again
4. Publish approved document
5. Notify stakeholders

### Approval Options

**Basic approval** (wait indefinitely):
```csharp
.AwaitApproval<LegalTeam>()
```

**With timeout** (fail if no response):
```csharp
.AwaitApproval<LegalTeam>(options => options
    .WithTimeout(TimeSpan.FromDays(2)))
```

**Timeout with escalation**:
```csharp
.AwaitApproval<LegalTeam>(options => options
    .WithTimeout(TimeSpan.FromDays(2))
    .OnTimeout(flow => flow.Then<EscalateToManager>()))
```

**With rejection handling**:
```csharp
.AwaitApproval<LegalTeam>(options => options
    .OnRejection(flow => flow
        .Then<AddressLegalConcerns>()
        .Then<LegalReview>()))
```

**Multiple approvers**:
```csharp
.AwaitApproval<LegalTeam>(options => options
    .RequireAll()   // All team members must approve
    .WithQuorum(2)) // OR: at least 2 must approve
```

### The Approver Interface

```csharp
public class LegalTeam : IApprover<DocumentState>
{
    public string Role => "legal-team";

    // Create the approval request with all context needed for decision
    public ApprovalRequest CreateRequest(DocumentState state)
    {
        return new ApprovalRequest
        {
            Title = $"Legal Approval Required: {state.Document.Title}",
            Description = "Please review the document and legal analysis.",
            Context = new Dictionary<string, object>
            {
                ["DocumentTitle"] = state.Document.Title,
                ["DocumentType"] = state.Document.Type.ToString(),
                ["LegalIssues"] = state.LegalReview?.Issues ?? [],
                ["ReviewerComments"] = state.LegalReview?.ReviewerComments ?? ""
            }
        };
    }

    // Apply the decision to state
    public DocumentState ApplyApproval(DocumentState state, ApprovalDecision decision)
    {
        return state.With(s => s.Approval, decision);
    }
}
```

**The key insight**: The approval request includes ALL context needed for the human to make an informed decision. Don't make them dig for information.

### The Escalation Step

```csharp
public class EscalateToManager : IWorkflowStep<DocumentState>
{
    private readonly IEscalationService _escalation;

    public EscalateToManager(IEscalationService escalation)
    {
        _escalation = escalation;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        await _escalation.EscalateAsync(
            $"Document approval timeout: {state.Document.Title}",
            state.WorkflowId,
            ct);

        return state
            .With(s => s.IsEscalated, true)
            .AsResult();
    }
}
```

**Escalation records the fact** that normal approval didn't happen. This is important for audit.

### The Rejection Handler

```csharp
public class AddressLegalConcerns : IWorkflowStep<DocumentState>
{
    private readonly IDocumentReviser _reviser;

    public AddressLegalConcerns(IDocumentReviser reviser)
    {
        _reviser = reviser;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        // Use AI to address the rejection feedback
        var revisedContent = await _reviser.AddressIssuesAsync(
            state.DraftContent!,
            state.LegalReview!.Issues,
            state.Approval?.Comments,  // Include rejection reason
            ct);

        return state
            .With(s => s.DraftContent, revisedContent)
            .With(s => s.LegalReview, null)  // Clear for re-review
            .With(s => s.Approval, null)     // Clear previous decision
            .AsResult();
    }
}
```

**After addressing concerns**, the workflow loops back to `LegalReview`, then requests approval again.

---

## Submitting Approvals

Approvals come from an external system (UI, API, email, etc.):

```csharp
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvals;

    [HttpPost("{workflowId}/approve")]
    public async Task<IActionResult> Approve(
        Guid workflowId,
        [FromBody] ApprovalRequest request)
    {
        await _approvals.SubmitDecisionAsync(workflowId, new ApprovalDecision(
            Approved: true,
            ApproverId: User.Identity!.Name!,
            DecisionTime: DateTimeOffset.UtcNow,
            Comments: request.Comments));

        return Ok();
    }

    [HttpPost("{workflowId}/reject")]
    public async Task<IActionResult> Reject(
        Guid workflowId,
        [FromBody] RejectionRequest request)
    {
        await _approvals.SubmitDecisionAsync(workflowId, new ApprovalDecision(
            Approved: false,
            ApproverId: User.Identity!.Name!,
            DecisionTime: DateTimeOffset.UtcNow,
            Comments: request.Reason));  // Reason is required for rejections

        return Ok();
    }
}
```

**When the decision arrives**, the workflow resumes exactly where it paused.

---

## The "Aha Moment"

> **Trust in AI systems comes from transparency and control, not from the AI being perfect.**
>
> The approval step isn't a bottleneck—it's the difference between "AI error" and "approved decision with known risk." When something goes wrong at 2 AM, you'll be glad you can answer "Who approved this?" with a name, timestamp, and their reasoning.
>
> The workflow persists its state across hours or days of waiting. Process restarts, deployments, even server crashes don't lose the approval context. When the human finally responds, the workflow picks up exactly where it left off.

---

## Querying Pending Approvals

Build dashboards showing what needs attention:

```csharp
// Find all documents awaiting legal approval
var pending = await session
    .Query<DocumentApprovalReadModel>()
    .Where(d => d.CurrentPhase == DocumentApprovalPhase.AwaitingApproval)
    .ToListAsync();

// Find approvals approaching timeout
var urgent = await session
    .Query<DocumentApprovalReadModel>()
    .Where(d => d.CurrentPhase == DocumentApprovalPhase.AwaitingApproval
             && d.ApprovalRequestedAt < DateTimeOffset.UtcNow.AddHours(-36))
    .ToListAsync();
```

---

## Extension Exercises

### Exercise 1: Add Reminder Notifications

Send reminders before timeout:

1. Configure reminder interval (e.g., every 8 hours)
2. Create `SendReminder` step
3. Add to timeout path: remind → wait → remind → escalate

### Exercise 2: Multi-Level Approval

Require multiple approvals in sequence:

1. Add `AwaitApproval<LegalTeam>()` followed by `AwaitApproval<Finance>()`
2. Each approval captures different perspective
3. Handle rejection at any level

### Exercise 3: Conditional Approval Requirements

Different documents need different approval levels:

1. Add logic to check `Document.Type`
2. Marketing → single approver
3. Contract → legal + executive approval
4. High-value → board approval

---

## Key Takeaways

1. **Approval steps persist workflow state** and wait for external input
2. **Timeouts prevent workflows from waiting forever**—always configure handling
3. **Escalation paths maintain business continuity** when approvers don't respond
4. **Rejection paths enable iterative improvement**—not workflow death
5. **Context is critical**—approvers need all information to make good decisions
6. **Audit trails capture who approved what, when, and why**

---

## Related

- [ContentPipeline Sample](../../samples/ContentPipeline/) - Working implementation with approval gates
- [Iterative Refinement Pattern](./iterative-refinement.md) - Loops for quality improvement
- [Branching Pattern](./branching.md) - Conditional routing based on decisions
