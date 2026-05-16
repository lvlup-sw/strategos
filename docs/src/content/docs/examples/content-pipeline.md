---
title: "Content Pipeline Workflow"
---

# Content Pipeline Workflow

An AI-powered content generation pipeline demonstrating iterative refinement, Thompson Sampling for model selection, and human approval workflows.

## Overview

This example implements an intelligent content generation system. When a content request arrives, the workflow generates a draft using LLM, iteratively refines it based on quality scores until a threshold is met, and then routes to human approval before publishing. Thompson Sampling intelligently selects which LLM model to use based on historical performance.

**Use this pattern when:**
- Output quality requires iterative improvement
- Multiple AI models are available with varying performance
- Human oversight is required before final actions
- Quality metrics can be automatically evaluated

## State Definition

```csharp
[WorkflowState]
public record ContentState : IWorkflowState
{
    public Guid WorkflowId { get; init; }

    // Request details
    public ContentRequest Request { get; init; } = null!;
    public ContentStatus Status { get; init; } = ContentStatus.Draft;

    // Current content
    public string? CurrentDraft { get; init; }
    public decimal QualityScore { get; init; }
    public int IterationCount { get; init; }

    // Model selection tracking
    public string? SelectedModel { get; init; }
    public ModelSelectionInfo? ModelSelection { get; init; }

    // Quality assessment
    public QualityAssessment? LatestAssessment { get; init; }

    // Approval
    public ApprovalDecision? Approval { get; init; }
    public bool IsPublished { get; init; }

    // History for learning and audit
    [Append]
    public ImmutableList<ContentIteration> Iterations { get; init; } = [];

    [Append]
    public ImmutableList<ModelOutcome> ModelOutcomes { get; init; } = [];
}

public record ContentRequest(
    string Title,
    ContentType Type,
    string Topic,
    string Audience,
    int TargetWordCount,
    string? StyleGuide,
    IReadOnlyList<string> Keywords);

public record ContentIteration(
    int Number,
    string Content,
    decimal Score,
    string ModelUsed,
    IReadOnlyList<string> Improvements,
    DateTimeOffset GeneratedAt);

public record QualityAssessment(
    decimal OverallScore,
    decimal ClarityScore,
    decimal AccuracyScore,
    decimal EngagementScore,
    decimal StyleScore,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Suggestions);

public record ModelSelectionInfo(
    string SelectedModelId,
    double SampledConfidence,
    TaskCategory Category,
    IReadOnlyDictionary<string, double> AllSamples);

public record ModelOutcome(
    string ModelId,
    TaskCategory Category,
    bool Success,
    decimal QualityScore,
    DateTimeOffset RecordedAt);

public record ApprovalDecision(
    bool Approved,
    string ReviewerId,
    string? Feedback,
    DateTimeOffset DecisionTime);

public enum ContentType { BlogPost, Article, Documentation, Marketing, Social }

public enum ContentStatus
{
    Draft,
    Refining,
    QualityMet,
    AwaitingApproval,
    Approved,
    Published,
    Rejected
}

public enum TaskCategory { Creative, Technical, Marketing, Educational }
```

## Workflow Definition

```csharp
public class ContentPipelineWorkflow
{
    public static Workflow<ContentState> Create() =>
        Workflow<ContentState>
            .Create("content-pipeline")
            .StartWith<SelectModel>()
            .Then<GenerateDraft>()
            .RepeatUntil(
                condition: state => state.QualityScore >= 0.85m,
                maxIterations: 5,
                body: flow => flow
                    .Then<AssessQuality>()
                    .Then<RefineContent>())
            .AwaitApproval<ContentEditor>(options => options
                .WithTimeout(TimeSpan.FromDays(3))
                .OnTimeout(flow => flow.Then<EscalateToLeadEditor>())
                .OnRejection(flow => flow
                    .Then<ApplyEditorFeedback>()
                    .Then<AssessQuality>()))
            .Finally<PublishContent>();
}
```

This workflow:
1. Selects the best LLM model via Thompson Sampling
2. Generates an initial draft
3. Iteratively assesses and refines until quality >= 85%
4. Waits for human editor approval
5. Publishes the final content

## Step Implementations

### SelectModel

Uses Thompson Sampling to intelligently select the best model based on historical performance.

```csharp
public class SelectModel : IWorkflowStep<ContentState>
{
    private readonly IAgentSelector _selector;
    private readonly ILogger<SelectModel> _logger;

    public SelectModel(IAgentSelector selector, ILogger<SelectModel> logger)
    {
        _selector = selector;
        _logger = logger;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        // Determine task category based on content type
        var category = state.Request.Type switch
        {
            ContentType.BlogPost or ContentType.Article => TaskCategory.Creative,
            ContentType.Documentation => TaskCategory.Technical,
            ContentType.Marketing or ContentType.Social => TaskCategory.Marketing,
            _ => TaskCategory.Creative
        };

        // Select model via Thompson Sampling
        var selection = await _selector.SelectAgentAsync(new AgentSelectionContext
        {
            AvailableAgentIds = ["gpt-4o", "claude-3-opus", "claude-3-sonnet", "gemini-pro"],
            TaskDescription = $"Generate {state.Request.Type} content about {state.Request.Topic}",
            Category = category
        }, ct);

        _logger.LogInformation(
            "Selected model {Model} with sampled confidence {Confidence:P1} for {Category}",
            selection.SelectedAgentId,
            selection.SampledValue,
            category);

        var modelSelection = new ModelSelectionInfo(
            selection.SelectedAgentId,
            selection.SampledValue,
            category,
            selection.AllSamples);

        return state
            .With(s => s.SelectedModel, selection.SelectedAgentId)
            .With(s => s.ModelSelection, modelSelection)
            .AsResult();
    }
}
```

### GenerateDraft

```csharp
public class GenerateDraft : IWorkflowStep<ContentState>
{
    private readonly ILlmClientFactory _llmFactory;
    private readonly TimeProvider _time;

    public GenerateDraft(ILlmClientFactory llmFactory, TimeProvider time)
    {
        _llmFactory = llmFactory;
        _time = time;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        var llm = _llmFactory.GetClient(state.SelectedModel!);

        var prompt = BuildPrompt(state.Request);

        var response = await llm.CompleteAsync(new CompletionRequest
        {
            Prompt = prompt,
            MaxTokens = EstimateTokens(state.Request.TargetWordCount),
            Temperature = 0.7
        }, ct);

        var iteration = new ContentIteration(
            Number: 1,
            Content: response.Text,
            Score: 0m, // Will be assessed next
            ModelUsed: state.SelectedModel!,
            Improvements: [],
            GeneratedAt: _time.GetUtcNow());

        return state
            .With(s => s.CurrentDraft, response.Text)
            .With(s => s.IterationCount, 1)
            .With(s => s.Status, ContentStatus.Refining)
            .With(s => s.Iterations, state.Iterations.Add(iteration))
            .AsResult();
    }

    private static string BuildPrompt(ContentRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Write a {request.Type.ToString().ToLowerInvariant()} about: {request.Topic}");
        sb.AppendLine();
        sb.AppendLine($"Title: {request.Title}");
        sb.AppendLine($"Target audience: {request.Audience}");
        sb.AppendLine($"Target length: approximately {request.TargetWordCount} words");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(request.StyleGuide))
        {
            sb.AppendLine($"Style guide: {request.StyleGuide}");
        }

        if (request.Keywords.Any())
        {
            sb.AppendLine($"Keywords to include: {string.Join(", ", request.Keywords)}");
        }

        sb.AppendLine();
        sb.AppendLine("Requirements:");
        sb.AppendLine("- Clear and engaging introduction");
        sb.AppendLine("- Well-structured body with logical flow");
        sb.AppendLine("- Actionable or thought-provoking conclusion");
        sb.AppendLine("- Use appropriate headings and formatting");

        return sb.ToString();
    }

    private static int EstimateTokens(int wordCount) => (int)(wordCount * 1.5);
}
```

### AssessQuality

```csharp
public class AssessQuality : IWorkflowStep<ContentState>
{
    private readonly IContentEvaluator _evaluator;
    private readonly IAgentSelector _selector;
    private readonly TimeProvider _time;

    public AssessQuality(
        IContentEvaluator evaluator,
        IAgentSelector selector,
        TimeProvider time)
    {
        _evaluator = evaluator;
        _selector = selector;
        _time = time;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        var assessment = await _evaluator.EvaluateAsync(new EvaluationRequest
        {
            Content = state.CurrentDraft!,
            ContentType = state.Request.Type,
            TargetAudience = state.Request.Audience,
            RequiredKeywords = state.Request.Keywords,
            TargetWordCount = state.Request.TargetWordCount
        }, ct);

        // Record outcome for Thompson Sampling learning
        var outcome = new ModelOutcome(
            state.SelectedModel!,
            state.ModelSelection!.Category,
            Success: assessment.OverallScore >= 0.7m, // Considered success if decent quality
            assessment.OverallScore,
            _time.GetUtcNow());

        // Report to selector for belief updates
        await _selector.RecordOutcomeAsync(
            state.SelectedModel!,
            state.ModelSelection!.Category,
            assessment.OverallScore >= 0.7m
                ? AgentOutcome.Succeeded(assessment.OverallScore)
                : AgentOutcome.Partial((double)assessment.OverallScore),
            ct);

        var newState = state
            .With(s => s.LatestAssessment, assessment)
            .With(s => s.QualityScore, assessment.OverallScore)
            .With(s => s.ModelOutcomes, state.ModelOutcomes.Add(outcome));

        // Update status if quality threshold met
        if (assessment.OverallScore >= 0.85m)
        {
            newState = newState.With(s => s.Status, ContentStatus.QualityMet);
        }

        return newState.AsResult();
    }
}
```

### RefineContent

```csharp
public class RefineContent : IWorkflowStep<ContentState>
{
    private readonly ILlmClientFactory _llmFactory;
    private readonly TimeProvider _time;

    public RefineContent(ILlmClientFactory llmFactory, TimeProvider time)
    {
        _llmFactory = llmFactory;
        _time = time;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        // Skip if quality already met
        if (state.QualityScore >= 0.85m)
        {
            return state.AsResult();
        }

        var llm = _llmFactory.GetClient(state.SelectedModel!);
        var assessment = state.LatestAssessment!;

        var refinementPrompt = BuildRefinementPrompt(state, assessment);

        var response = await llm.CompleteAsync(new CompletionRequest
        {
            Prompt = refinementPrompt,
            MaxTokens = EstimateTokens(state.Request.TargetWordCount * 2), // Allow expansion
            Temperature = 0.5 // Lower temperature for more focused refinement
        }, ct);

        var newIteration = state.IterationCount + 1;

        var iteration = new ContentIteration(
            Number: newIteration,
            Content: response.Text,
            Score: 0m, // Will be assessed in next loop iteration
            ModelUsed: state.SelectedModel!,
            Improvements: assessment.Suggestions,
            GeneratedAt: _time.GetUtcNow());

        return state
            .With(s => s.CurrentDraft, response.Text)
            .With(s => s.IterationCount, newIteration)
            .With(s => s.Iterations, state.Iterations.Add(iteration))
            .AsResult();
    }

    private static string BuildRefinementPrompt(ContentState state, QualityAssessment assessment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Refine the following content based on the feedback provided.");
        sb.AppendLine();
        sb.AppendLine("## Current Content");
        sb.AppendLine(state.CurrentDraft);
        sb.AppendLine();
        sb.AppendLine("## Quality Assessment");
        sb.AppendLine($"Overall Score: {assessment.OverallScore:P0}");
        sb.AppendLine($"Clarity: {assessment.ClarityScore:P0}");
        sb.AppendLine($"Accuracy: {assessment.AccuracyScore:P0}");
        sb.AppendLine($"Engagement: {assessment.EngagementScore:P0}");
        sb.AppendLine($"Style: {assessment.StyleScore:P0}");
        sb.AppendLine();
        sb.AppendLine("## Weaknesses to Address");
        foreach (var weakness in assessment.Weaknesses)
        {
            sb.AppendLine($"- {weakness}");
        }
        sb.AppendLine();
        sb.AppendLine("## Specific Improvements Requested");
        foreach (var suggestion in assessment.Suggestions)
        {
            sb.AppendLine($"- {suggestion}");
        }
        sb.AppendLine();
        sb.AppendLine("Please rewrite the content addressing all the feedback while preserving the strengths.");
        sb.AppendLine("Maintain the original structure and key points, but improve clarity, engagement, and accuracy.");

        return sb.ToString();
    }

    private static int EstimateTokens(int wordCount) => (int)(wordCount * 1.5);
}
```

### ContentEditor Approver

```csharp
public class ContentEditor : IApprover<ContentState>
{
    public string Role => "content-editor";

    public ApprovalRequest CreateRequest(ContentState state)
    {
        return new ApprovalRequest
        {
            Title = $"Review: {state.Request.Title}",
            Description = $"Please review this {state.Request.Type} content before publication.",
            Context = new Dictionary<string, object>
            {
                ["ContentType"] = state.Request.Type.ToString(),
                ["Topic"] = state.Request.Topic,
                ["TargetAudience"] = state.Request.Audience,
                ["QualityScore"] = state.QualityScore,
                ["IterationCount"] = state.IterationCount,
                ["Content"] = state.CurrentDraft!,
                ["Assessment"] = state.LatestAssessment!
            }
        };
    }

    public ContentState ApplyApproval(ContentState state, ApprovalDecision decision)
    {
        return state
            .With(s => s.Approval, decision)
            .With(s => s.Status, decision.Approved
                ? ContentStatus.Approved
                : ContentStatus.Rejected);
    }
}
```

### EscalateToLeadEditor

```csharp
public class EscalateToLeadEditor : IWorkflowStep<ContentState>
{
    private readonly INotificationService _notifications;

    public EscalateToLeadEditor(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        await _notifications.SendEscalationAsync(new EscalationNotification
        {
            Title = $"Urgent Review Needed: {state.Request.Title}",
            Message = "Content approval has been pending for 3 days without action.",
            WorkflowId = state.WorkflowId,
            Priority = Priority.High
        }, ct);

        return state.AsResult();
    }
}
```

### ApplyEditorFeedback

```csharp
public class ApplyEditorFeedback : IWorkflowStep<ContentState>
{
    private readonly ILlmClientFactory _llmFactory;
    private readonly TimeProvider _time;

    public ApplyEditorFeedback(ILlmClientFactory llmFactory, TimeProvider time)
    {
        _llmFactory = llmFactory;
        _time = time;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        if (state.Approval?.Feedback is null or "")
        {
            return state.AsResult();
        }

        var llm = _llmFactory.GetClient(state.SelectedModel!);

        var prompt = $"""
            Revise the following content based on editor feedback.

            ## Current Content
            {state.CurrentDraft}

            ## Editor Feedback
            {state.Approval.Feedback}

            Please rewrite the content addressing all the editor's concerns.
            """;

        var response = await llm.CompleteAsync(new CompletionRequest
        {
            Prompt = prompt,
            MaxTokens = EstimateTokens(state.Request.TargetWordCount * 2),
            Temperature = 0.5
        }, ct);

        var newIteration = state.IterationCount + 1;

        var iteration = new ContentIteration(
            Number: newIteration,
            Content: response.Text,
            Score: 0m,
            ModelUsed: state.SelectedModel!,
            Improvements: [state.Approval.Feedback],
            GeneratedAt: _time.GetUtcNow());

        return state
            .With(s => s.CurrentDraft, response.Text)
            .With(s => s.IterationCount, newIteration)
            .With(s => s.Iterations, state.Iterations.Add(iteration))
            .With(s => s.Approval, null) // Clear for re-approval
            .With(s => s.Status, ContentStatus.Refining)
            .AsResult();
    }

    private static int EstimateTokens(int wordCount) => (int)(wordCount * 1.5);
}
```

### PublishContent

```csharp
public class PublishContent : IWorkflowStep<ContentState>
{
    private readonly IContentManagementSystem _cms;
    private readonly IAnalyticsService _analytics;
    private readonly TimeProvider _time;

    public PublishContent(
        IContentManagementSystem cms,
        IAnalyticsService analytics,
        TimeProvider time)
    {
        _cms = cms;
        _analytics = analytics;
        _time = time;
    }

    public async Task<StepResult<ContentState>> ExecuteAsync(
        ContentState state,
        StepContext context,
        CancellationToken ct)
    {
        // Idempotency check
        if (state.IsPublished)
        {
            return state.AsResult();
        }

        var publishResult = await _cms.PublishAsync(new PublishRequest
        {
            Title = state.Request.Title,
            Content = state.CurrentDraft!,
            ContentType = state.Request.Type,
            Author = state.Approval?.ReviewerId ?? "system",
            Keywords = state.Request.Keywords,
            Metadata = new Dictionary<string, string>
            {
                ["workflow_id"] = state.WorkflowId.ToString(),
                ["iterations"] = state.IterationCount.ToString(),
                ["quality_score"] = state.QualityScore.ToString("F2"),
                ["model_used"] = state.SelectedModel!
            }
        }, ct);

        // Track content performance for future model selection
        await _analytics.TrackPublicationAsync(new PublicationEvent
        {
            ContentId = publishResult.ContentId,
            ModelUsed = state.SelectedModel!,
            Category = state.ModelSelection!.Category,
            QualityScore = state.QualityScore,
            IterationsRequired = state.IterationCount
        }, ct);

        return state
            .With(s => s.IsPublished, true)
            .With(s => s.Status, ContentStatus.Published)
            .AsResult();
    }
}
```

## Service Interfaces

```csharp
public interface ILlmClientFactory
{
    ILlmClient GetClient(string modelId);
}

public interface ILlmClient
{
    Task<CompletionResponse> CompleteAsync(CompletionRequest request, CancellationToken ct);
}

public interface IContentEvaluator
{
    Task<QualityAssessment> EvaluateAsync(EvaluationRequest request, CancellationToken ct);
}

public interface IContentManagementSystem
{
    Task<PublishResult> PublishAsync(PublishRequest request, CancellationToken ct);
}

public interface IAnalyticsService
{
    Task TrackPublicationAsync(PublicationEvent evt, CancellationToken ct);
}
```

## Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(opts =>
{
    opts.Durability.Mode = DurabilityMode.Solo;
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Marten")!);
})
.IntegrateWithWolverine();

// Register workflow
builder.Services.AddStrategos()
    .AddWorkflow<ContentPipelineWorkflow>();

// Configure Thompson Sampling
builder.Services.AddAgentSelection(options => options
    .WithPrior(alpha: 2, beta: 2)
    .WithCategories(
        TaskCategory.Creative,
        TaskCategory.Technical,
        TaskCategory.Marketing,
        TaskCategory.Educational));

// Register LLM clients
builder.Services.AddSingleton<ILlmClientFactory, MultiProviderLlmFactory>();
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("LLM"));

// Register services
builder.Services.AddScoped<IContentEvaluator, LlmContentEvaluator>();
builder.Services.AddScoped<IContentManagementSystem, WordPressCms>();
builder.Services.AddScoped<IAnalyticsService, GoogleAnalyticsService>();
builder.Services.AddScoped<INotificationService, SlackNotificationService>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

## Starting the Workflow

```csharp
[ApiController]
[Route("api/content")]
public class ContentController : ControllerBase
{
    private readonly IWorkflowStarter _workflowStarter;
    private readonly IDocumentSession _session;

    public ContentController(
        IWorkflowStarter workflowStarter,
        IDocumentSession session)
    {
        _workflowStarter = workflowStarter;
        _session = session;
    }

    [HttpPost]
    public async Task<IActionResult> CreateContent(
        [FromBody] CreateContentRequest request,
        CancellationToken ct)
    {
        var workflowId = Guid.NewGuid();

        var contentRequest = new ContentRequest(
            Title: request.Title,
            Type: request.Type,
            Topic: request.Topic,
            Audience: request.Audience,
            TargetWordCount: request.WordCount,
            StyleGuide: request.StyleGuide,
            Keywords: request.Keywords);

        var initialState = new ContentState
        {
            WorkflowId = workflowId,
            Request = contentRequest
        };

        await _workflowStarter.StartAsync("content-pipeline", initialState, ct);

        return Accepted(new { WorkflowId = workflowId });
    }

    [HttpGet("{workflowId}")]
    public async Task<IActionResult> GetContentStatus(Guid workflowId, CancellationToken ct)
    {
        var saga = await _session.LoadAsync<ContentPipelineSaga>(workflowId, ct);

        if (saga is null) return NotFound();

        return Ok(new
        {
            WorkflowId = workflowId,
            Title = saga.State.Request.Title,
            Status = saga.State.Status.ToString(),
            Phase = saga.Phase.ToString(),
            QualityScore = saga.State.QualityScore,
            IterationCount = saga.State.IterationCount,
            ModelUsed = saga.State.SelectedModel,
            CurrentDraft = saga.State.CurrentDraft,
            Assessment = saga.State.LatestAssessment
        });
    }

    [HttpPost("{workflowId}/approve")]
    public async Task<IActionResult> ApproveContent(
        Guid workflowId,
        [FromBody] ApprovalRequest request,
        CancellationToken ct)
    {
        var approvalService = HttpContext.RequestServices.GetRequiredService<IApprovalService>();

        await approvalService.SubmitDecisionAsync(workflowId, new ApprovalDecision(
            Approved: true,
            ReviewerId: User.Identity!.Name!,
            Feedback: request.Comments,
            DecisionTime: DateTimeOffset.UtcNow), ct);

        return Ok();
    }

    [HttpPost("{workflowId}/reject")]
    public async Task<IActionResult> RejectContent(
        Guid workflowId,
        [FromBody] RejectionRequest request,
        CancellationToken ct)
    {
        var approvalService = HttpContext.RequestServices.GetRequiredService<IApprovalService>();

        await approvalService.SubmitDecisionAsync(workflowId, new ApprovalDecision(
            Approved: false,
            ReviewerId: User.Identity!.Name!,
            Feedback: request.Feedback,
            DecisionTime: DateTimeOffset.UtcNow), ct);

        return Ok();
    }
}
```

## Generated Artifacts

### Phase Enum

```csharp
public enum ContentPipelinePhase
{
    NotStarted,
    SelectModel,
    GenerateDraft,
    Refinement_AssessQuality,
    Refinement_RefineContent,
    AwaitingApproval,
    EscalateToLeadEditor,
    ApplyEditorFeedback,
    PublishContent,
    Completed,
    Failed
}
```

### Loop Control Flow

The generated saga handles the refinement loop:

```csharp
// After RefineContent completes
public async Task<object> Handle(
    ExecuteRefinement_RefineContentCommand command,
    RefineContent step,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, ct);
    State = ContentStateReducer.Reduce(State, result.StateUpdate);

    // Check loop exit condition
    if (State.QualityScore >= 0.85m)
    {
        // Quality met - proceed to approval
        Phase = ContentPipelinePhase.AwaitingApproval;
        return new RequestApprovalCommand(WorkflowId);
    }

    if (State.IterationCount >= 5)
    {
        // Max iterations - proceed to approval with current quality
        Phase = ContentPipelinePhase.AwaitingApproval;
        return new RequestApprovalCommand(WorkflowId);
    }

    // Continue loop - back to assessment
    return new ExecuteRefinement_AssessQualityCommand(WorkflowId);
}
```

## Key Points

- **Thompson Sampling** selects the best LLM model based on learned performance
- **Iterative refinement** with quality threshold and max iteration limit
- **Quality assessment** drives model learning and content improvement
- **Human-in-the-loop** approval with timeout escalation
- **Rejection handling** applies editor feedback and re-submits for approval
- **[Append] reducers** preserve complete iteration history
- **Model outcomes** feed back to improve future model selection

## Related Documentation

- [Iterative Refinement Example](./iterative-refinement.md) - Loop pattern details
- [Thompson Sampling Example](./thompson-sampling.md) - Agent selection algorithm
- [Approval Flow Example](./approval-flow.md) - Human approval patterns
