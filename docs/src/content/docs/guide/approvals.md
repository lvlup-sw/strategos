---
title: "Human-in-the-Loop"
---

# Human-in-the-Loop

Not every decision can be automated. Legal documents need attorney sign-off. High-value transactions require manager approval. Content moderation decisions may need human review. This tutorial shows you how to use `AwaitApproval` to pause workflows for human input, with timeout and rejection handling.

## What You Will Build

A document approval workflow that:

1. **Drafts** a document from a template
2. **Reviews** for legal compliance
3. **Awaits approval** from the legal team (with 2-day timeout)
4. **Handles timeout** by escalating to management
5. **Handles rejection** by revising and re-submitting
6. **Publishes** the approved document
7. **Notifies** stakeholders

## Step 1: Define the State

The state tracks document content, review results, and approval decisions:

```csharp
[WorkflowState]
public record DocumentState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public Document Document { get; init; } = null!;
    public string? DraftContent { get; init; }
    public LegalReviewResult? LegalReview { get; init; }
    public ApprovalDecision? Approval { get; init; }
    public bool IsPublished { get; init; }
    public bool StakeholdersNotified { get; init; }
    public bool IsEscalated { get; init; }
}

public record Document(
    string Title,
    string Author,
    DocumentType Type,
    string Content);

public record LegalReviewResult(
    bool HasIssues,
    IReadOnlyList<string> Issues,
    string ReviewerComments);

public record ApprovalDecision(
    bool Approved,
    string ApproverId,
    DateTimeOffset DecisionTime,
    string? Comments);

public enum DocumentType { Contract, Policy, Procedure, Marketing }
```

## Step 2: Define the Workflow with AwaitApproval

The `AwaitApproval` method pauses the workflow until a human responds:

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

This workflow:
- Waits up to 2 days for legal team approval
- On timeout, escalates to management then continues
- On rejection, addresses concerns and loops back for re-review
- After approval (or escalation), publishes and notifies

## Approval Options

### Basic Approval

Wait indefinitely for approval:

```csharp
.AwaitApproval<LegalTeam>()
```

### With Timeout

Fail the workflow if approval is not received in time:

```csharp
.AwaitApproval<LegalTeam>(options => options
    .WithTimeout(TimeSpan.FromDays(2)))
```

### Timeout with Escalation

Route to an alternative path on timeout:

```csharp
.AwaitApproval<LegalTeam>(options => options
    .WithTimeout(TimeSpan.FromDays(2))
    .OnTimeout(flow => flow.Then<EscalateToManager>()))
```

After the timeout path completes, execution continues to the next step.

### With Rejection Handling

Execute steps when the request is rejected:

```csharp
.AwaitApproval<LegalTeam>(options => options
    .OnRejection(flow => flow
        .Then<AddressLegalConcerns>()
        .Then<LegalReview>()))
```

After the rejection path completes, the workflow re-requests approval.

### Multiple Approvers

Require approval from multiple people:

```csharp
.AwaitApproval<LegalTeam>(options => options
    .RequireAll())  // All team members must approve

// Or require a quorum
.AwaitApproval<LegalTeam>(options => options
    .WithQuorum(2))  // At least 2 must approve
```

## Step 3: Implement the Approver

Approvers define who can approve and how approval affects state:

```csharp
public class LegalTeam : IApprover<DocumentState>
{
    public string Role => "legal-team";

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

    public DocumentState ApplyApproval(DocumentState state, ApprovalDecision decision)
    {
        return state.With(s => s.Approval, decision);
    }
}
```

The `CreateRequest` method builds the information shown to approvers. The `ApplyApproval` method updates state with the decision.

## Step 4: Implement the Steps

### DraftDocument

```csharp
public class DraftDocument : IWorkflowStep<DocumentState>
{
    private readonly IDocumentDrafter _drafter;

    public DraftDocument(IDocumentDrafter drafter)
    {
        _drafter = drafter;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        var draft = await _drafter.CreateDraftAsync(state.Document, ct);

        return state
            .With(s => s.DraftContent, draft)
            .AsResult();
    }
}
```

### LegalReview

```csharp
public class LegalReview : IWorkflowStep<DocumentState>
{
    private readonly ILegalReviewService _legalService;

    public LegalReview(ILegalReviewService legalService)
    {
        _legalService = legalService;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        var review = await _legalService.ReviewAsync(state.DraftContent!, ct);

        return state
            .With(s => s.LegalReview, review)
            .AsResult();
    }
}
```

### EscalateToManager

Handles the timeout scenario:

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

### AddressLegalConcerns

Handles the rejection scenario:

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
        var revisedContent = await _reviser.AddressIssuesAsync(
            state.DraftContent!,
            state.LegalReview!.Issues,
            state.Approval?.Comments,
            ct);

        return state
            .With(s => s.DraftContent, revisedContent)
            .With(s => s.LegalReview, null)  // Clear for re-review
            .With(s => s.Approval, null)
            .AsResult();
    }
}
```

### PublishDocument

```csharp
public class PublishDocument : IWorkflowStep<DocumentState>
{
    private readonly IPublishingService _publishing;

    public PublishDocument(IPublishingService publishing)
    {
        _publishing = publishing;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        await _publishing.PublishAsync(
            state.Document.Title,
            state.DraftContent!,
            ct);

        return state
            .With(s => s.IsPublished, true)
            .AsResult();
    }
}
```

### NotifyStakeholders

```csharp
public class NotifyStakeholders : IWorkflowStep<DocumentState>
{
    private readonly INotificationService _notifications;

    public NotifyStakeholders(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<StepResult<DocumentState>> ExecuteAsync(
        DocumentState state,
        StepContext context,
        CancellationToken ct)
    {
        await _notifications.NotifyDocumentPublishedAsync(
            state.Document.Title,
            state.Document.Author,
            ct);

        return state
            .With(s => s.StakeholdersNotified, true)
            .AsResult();
    }
}
```

## Submitting Approvals

Create an API for approvers to submit decisions:

```csharp
public class ApprovalController : ControllerBase
{
    private readonly IApprovalService _approvals;

    public ApprovalController(IApprovalService approvals)
    {
        _approvals = approvals;
    }

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
            Comments: request.Reason));

        return Ok();
    }
}
```

## Querying Pending Approvals

Find all workflows waiting for approval:

```csharp
// Find all documents awaiting legal approval
var pending = await session
    .Query<DocumentApprovalReadModel>()
    .Where(d => d.CurrentPhase == DocumentApprovalPhase.AwaitingApproval)
    .ToListAsync();
```

Build dashboards and notification systems on top of this query capability.

## Generated Phase Enum

```csharp
public enum DocumentApprovalPhase
{
    NotStarted,
    DraftDocument,
    LegalReview,
    AwaitingApproval,
    EscalateToManager,
    AddressLegalConcerns,
    PublishDocument,
    NotifyStakeholders,
    Completed,
    Failed
}
```

## Key Points

- **Approval steps persist state** and wait for external input (hours or days)
- **Timeouts prevent indefinite waiting** - configure escalation paths
- **Rejection paths enable iterative review** - address concerns and re-submit
- **The saga resumes exactly where it paused** when approval arrives
- **Approvals can require multiple approvers** or reach a quorum
- **Query pending approvals** to build dashboards and notifications

## Next Steps

You have learned how to incorporate human decision-making into workflows. The final tutorial covers intelligent agent selection:

- [Agent Selection](./agents) - Use Thompson Sampling to route tasks to the best-performing agents
