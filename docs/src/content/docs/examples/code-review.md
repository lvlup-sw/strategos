---
title: "Code Review Workflow"
---

# Code Review Workflow

An automated pull request review workflow demonstrating Fork/Join for parallel analysis, conditional branching, human approval, and event sourcing for audit trails.

## Overview

This example implements an intelligent code review system for pull requests. When a PR is opened, the workflow runs parallel analyses for security vulnerabilities, code style, and complexity metrics. Based on the findings, it routes to automatic approval, human review, or blocking with required fixes. Every decision is captured as an immutable event for compliance auditing.

**Use this pattern when:**
- Multiple independent analyses can run concurrently
- Different severity levels require different handling
- Human oversight is needed for certain conditions
- Complete audit trail is a compliance requirement

## State Definition

```csharp
[WorkflowState]
public record CodeReviewState : IWorkflowState
{
    public Guid WorkflowId { get; init; }

    // Pull request details
    public PullRequest PullRequest { get; init; } = null!;
    public ReviewStatus Status { get; init; } = ReviewStatus.Pending;

    // Analysis results from parallel checks
    public SecurityAnalysis? Security { get; init; }
    public StyleAnalysis? Style { get; init; }
    public ComplexityAnalysis? Complexity { get; init; }

    // Synthesized review
    public ReviewSummary? Summary { get; init; }
    public ReviewDecision? Decision { get; init; }

    // Human review
    public HumanReviewRequest? HumanReviewRequest { get; init; }
    public HumanReviewResponse? HumanReview { get; init; }

    // Actions taken
    public bool CommentPosted { get; init; }
    public bool StatusUpdated { get; init; }

    // Event sourcing - complete audit trail
    [Append]
    public ImmutableList<ReviewEvent> Events { get; init; } = [];
}

public record PullRequest(
    int Number,
    string Repository,
    string Title,
    string Author,
    string BaseBranch,
    string HeadBranch,
    string HeadSha,
    IReadOnlyList<ChangedFile> Files,
    int Additions,
    int Deletions);

public record ChangedFile(
    string Path,
    FileChangeType ChangeType,
    int Additions,
    int Deletions,
    string? Patch);

public record SecurityAnalysis(
    SecuritySeverity HighestSeverity,
    IReadOnlyList<SecurityFinding> Findings,
    bool HasCriticalIssues,
    decimal RiskScore);

public record SecurityFinding(
    string RuleId,
    SecuritySeverity Severity,
    string File,
    int Line,
    string Description,
    string Recommendation);

public record StyleAnalysis(
    int ViolationCount,
    IReadOnlyList<StyleViolation> Violations,
    bool PassesThreshold,
    decimal StyleScore);

public record StyleViolation(
    string RuleId,
    StyleSeverity Severity,
    string File,
    int Line,
    string Message);

public record ComplexityAnalysis(
    decimal AverageCyclomaticComplexity,
    decimal MaxCyclomaticComplexity,
    IReadOnlyList<ComplexityIssue> Issues,
    bool PassesThreshold);

public record ComplexityIssue(
    string File,
    string Method,
    int Complexity,
    string Recommendation);

public record ReviewSummary(
    ReviewOutcome Outcome,
    string SummaryText,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Suggestions);

public record ReviewDecision(
    ReviewOutcome Outcome,
    string Reason,
    bool RequiresHumanReview,
    DateTimeOffset DecidedAt);

public record HumanReviewRequest(
    string RequestedReviewer,
    string Reason,
    IReadOnlyList<string> FocusAreas,
    DateTimeOffset RequestedAt);

public record HumanReviewResponse(
    string ReviewerId,
    bool Approved,
    string? Comments,
    IReadOnlyList<string> RequestedChanges,
    DateTimeOffset ReviewedAt);

public record ReviewEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string Actor,
    string Details,
    IReadOnlyDictionary<string, object>? Metadata);

public enum ReviewStatus
{
    Pending,
    Analyzing,
    AnalysisComplete,
    AwaitingHumanReview,
    Approved,
    ChangesRequested,
    Blocked
}

public enum ReviewOutcome { Approve, RequestChanges, Block }

public enum SecuritySeverity { None, Low, Medium, High, Critical }

public enum StyleSeverity { Info, Warning, Error }

public enum FileChangeType { Added, Modified, Deleted, Renamed }
```

## Workflow Definition

```csharp
public class CodeReviewWorkflow
{
    public static Workflow<CodeReviewState> Create() =>
        Workflow<CodeReviewState>
            .Create("code-review")
            .StartWith<FetchPullRequestDetails>()
            .Fork(
                flow => flow.Then<SecurityAnalysisStep>(),
                flow => flow.Then<StyleAnalysisStep>(),
                flow => flow.Then<ComplexityAnalysisStep>())
            .Join<SynthesizeReview>()
            .Branch(state => DetermineOutcome(state),
                when: ReviewOutcome.Approve, then: flow => flow
                    .Then<AutoApprove>(),
                when: ReviewOutcome.RequestChanges, then: flow => flow
                    .AwaitApproval<SeniorDeveloper>(options => options
                        .WithTimeout(TimeSpan.FromDays(2))
                        .OnTimeout(flow => flow.Then<EscalateToTeamLead>()))
                    .Then<ProcessHumanDecision>(),
                when: ReviewOutcome.Block, then: flow => flow
                    .Then<BlockPullRequest>())
            .Finally<PostReviewComment>();

    private static ReviewOutcome DetermineOutcome(CodeReviewState state)
    {
        // Critical security issues always block
        if (state.Security?.HasCriticalIssues == true)
        {
            return ReviewOutcome.Block;
        }

        // High-severity security or major complexity issues need human review
        if (state.Security?.HighestSeverity >= SecuritySeverity.High ||
            state.Complexity?.MaxCyclomaticComplexity > 20)
        {
            return ReviewOutcome.RequestChanges;
        }

        // All checks pass - auto-approve
        if (state.Security?.RiskScore < 0.3m &&
            state.Style?.PassesThreshold == true &&
            state.Complexity?.PassesThreshold == true)
        {
            return ReviewOutcome.Approve;
        }

        // Default to requesting human review
        return ReviewOutcome.RequestChanges;
    }
}
```

This workflow:
1. Fetches PR details from GitHub
2. Forks into three parallel analysis paths
3. Joins and synthesizes the results
4. Branches based on severity to auto-approve, request human review, or block
5. Posts a comprehensive review comment

## Step Implementations

### FetchPullRequestDetails

```csharp
public class FetchPullRequestDetails : IWorkflowStep<CodeReviewState>
{
    private readonly IGitHubClient _github;
    private readonly TimeProvider _time;

    public FetchPullRequestDetails(IGitHubClient github, TimeProvider time)
    {
        _github = github;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        // Fetch full PR details including file diffs
        var pr = await _github.GetPullRequestAsync(
            state.PullRequest.Repository,
            state.PullRequest.Number,
            ct);

        var files = await _github.GetPullRequestFilesAsync(
            state.PullRequest.Repository,
            state.PullRequest.Number,
            ct);

        var enrichedPr = state.PullRequest with
        {
            HeadSha = pr.HeadSha,
            Files = files.Select(f => new ChangedFile(
                f.Filename,
                ParseChangeType(f.Status),
                f.Additions,
                f.Deletions,
                f.Patch)).ToList(),
            Additions = files.Sum(f => f.Additions),
            Deletions = files.Sum(f => f.Deletions)
        };

        var evt = new ReviewEvent(
            "PullRequestFetched",
            _time.GetUtcNow(),
            "system",
            $"Fetched PR #{pr.Number} with {files.Count} changed files",
            new Dictionary<string, object>
            {
                ["files_count"] = files.Count,
                ["additions"] = enrichedPr.Additions,
                ["deletions"] = enrichedPr.Deletions
            });

        return state
            .With(s => s.PullRequest, enrichedPr)
            .With(s => s.Status, ReviewStatus.Analyzing)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }

    private static FileChangeType ParseChangeType(string status) => status switch
    {
        "added" => FileChangeType.Added,
        "modified" => FileChangeType.Modified,
        "deleted" => FileChangeType.Deleted,
        "renamed" => FileChangeType.Renamed,
        _ => FileChangeType.Modified
    };
}
```

### SecurityAnalysisStep (Fork Path 1)

```csharp
public class SecurityAnalysisStep : IWorkflowStep<CodeReviewState>
{
    private readonly ISecurityScanner _scanner;
    private readonly TimeProvider _time;

    public SecurityAnalysisStep(ISecurityScanner scanner, TimeProvider time)
    {
        _scanner = scanner;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        var findings = new List<SecurityFinding>();

        foreach (var file in state.PullRequest.Files.Where(ShouldScan))
        {
            var fileFindings = await _scanner.ScanFileAsync(new SecurityScanRequest
            {
                Repository = state.PullRequest.Repository,
                Sha = state.PullRequest.HeadSha,
                FilePath = file.Path,
                Patch = file.Patch
            }, ct);

            findings.AddRange(fileFindings);
        }

        var highestSeverity = findings.Any()
            ? findings.Max(f => f.Severity)
            : SecuritySeverity.None;

        var hasCritical = findings.Any(f => f.Severity == SecuritySeverity.Critical);
        var riskScore = CalculateRiskScore(findings);

        var analysis = new SecurityAnalysis(
            highestSeverity,
            findings,
            hasCritical,
            riskScore);

        var evt = new ReviewEvent(
            "SecurityAnalysisCompleted",
            _time.GetUtcNow(),
            "security-scanner",
            $"Found {findings.Count} security issues, highest severity: {highestSeverity}",
            new Dictionary<string, object>
            {
                ["finding_count"] = findings.Count,
                ["highest_severity"] = highestSeverity.ToString(),
                ["risk_score"] = riskScore,
                ["has_critical"] = hasCritical
            });

        return state
            .With(s => s.Security, analysis)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }

    private static bool ShouldScan(ChangedFile file)
    {
        var scanExtensions = new[] { ".cs", ".ts", ".js", ".py", ".java", ".go" };
        return scanExtensions.Any(ext => file.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal CalculateRiskScore(IReadOnlyList<SecurityFinding> findings)
    {
        if (!findings.Any()) return 0m;

        var weights = new Dictionary<SecuritySeverity, decimal>
        {
            [SecuritySeverity.Critical] = 1.0m,
            [SecuritySeverity.High] = 0.7m,
            [SecuritySeverity.Medium] = 0.4m,
            [SecuritySeverity.Low] = 0.1m
        };

        return Math.Min(1.0m, findings.Sum(f => weights.GetValueOrDefault(f.Severity, 0m)));
    }
}
```

### StyleAnalysisStep (Fork Path 2)

```csharp
public class StyleAnalysisStep : IWorkflowStep<CodeReviewState>
{
    private readonly IStyleChecker _styleChecker;
    private readonly TimeProvider _time;

    public StyleAnalysisStep(IStyleChecker styleChecker, TimeProvider time)
    {
        _styleChecker = styleChecker;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        var violations = new List<StyleViolation>();

        foreach (var file in state.PullRequest.Files)
        {
            var fileViolations = await _styleChecker.CheckFileAsync(new StyleCheckRequest
            {
                Repository = state.PullRequest.Repository,
                Sha = state.PullRequest.HeadSha,
                FilePath = file.Path
            }, ct);

            violations.AddRange(fileViolations);
        }

        var errorCount = violations.Count(v => v.Severity == StyleSeverity.Error);
        var passesThreshold = errorCount == 0; // No errors allowed
        var styleScore = CalculateStyleScore(violations);

        var analysis = new StyleAnalysis(
            violations.Count,
            violations,
            passesThreshold,
            styleScore);

        var evt = new ReviewEvent(
            "StyleAnalysisCompleted",
            _time.GetUtcNow(),
            "style-checker",
            $"Found {violations.Count} style violations ({errorCount} errors)",
            new Dictionary<string, object>
            {
                ["violation_count"] = violations.Count,
                ["error_count"] = errorCount,
                ["passes_threshold"] = passesThreshold,
                ["style_score"] = styleScore
            });

        return state
            .With(s => s.Style, analysis)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }

    private static decimal CalculateStyleScore(IReadOnlyList<StyleViolation> violations)
    {
        if (!violations.Any()) return 1.0m;

        var penalties = new Dictionary<StyleSeverity, decimal>
        {
            [StyleSeverity.Error] = 0.1m,
            [StyleSeverity.Warning] = 0.02m,
            [StyleSeverity.Info] = 0.005m
        };

        var totalPenalty = violations.Sum(v => penalties.GetValueOrDefault(v.Severity, 0m));
        return Math.Max(0m, 1.0m - totalPenalty);
    }
}
```

### ComplexityAnalysisStep (Fork Path 3)

```csharp
public class ComplexityAnalysisStep : IWorkflowStep<CodeReviewState>
{
    private readonly IComplexityAnalyzer _analyzer;
    private readonly TimeProvider _time;

    public ComplexityAnalysisStep(IComplexityAnalyzer analyzer, TimeProvider time)
    {
        _analyzer = analyzer;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        var issues = new List<ComplexityIssue>();
        var complexities = new List<int>();

        foreach (var file in state.PullRequest.Files.Where(f =>
            f.ChangeType != FileChangeType.Deleted))
        {
            var fileAnalysis = await _analyzer.AnalyzeFileAsync(new ComplexityRequest
            {
                Repository = state.PullRequest.Repository,
                Sha = state.PullRequest.HeadSha,
                FilePath = file.Path
            }, ct);

            complexities.AddRange(fileAnalysis.MethodComplexities);

            issues.AddRange(fileAnalysis.HighComplexityMethods.Select(m =>
                new ComplexityIssue(
                    file.Path,
                    m.MethodName,
                    m.Complexity,
                    m.Recommendation)));
        }

        var avgComplexity = complexities.Any()
            ? complexities.Average()
            : 0;
        var maxComplexity = complexities.Any()
            ? complexities.Max()
            : 0;

        var passesThreshold = maxComplexity <= 15 && avgComplexity <= 8;

        var analysis = new ComplexityAnalysis(
            (decimal)avgComplexity,
            maxComplexity,
            issues,
            passesThreshold);

        var evt = new ReviewEvent(
            "ComplexityAnalysisCompleted",
            _time.GetUtcNow(),
            "complexity-analyzer",
            $"Average complexity: {avgComplexity:F1}, max: {maxComplexity}",
            new Dictionary<string, object>
            {
                ["average_complexity"] = avgComplexity,
                ["max_complexity"] = maxComplexity,
                ["issue_count"] = issues.Count,
                ["passes_threshold"] = passesThreshold
            });

        return state
            .With(s => s.Complexity, analysis)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }
}
```

### SynthesizeReview (Join Step)

```csharp
public class SynthesizeReview : IWorkflowStep<CodeReviewState>
{
    private readonly ILlmClient _llm;
    private readonly TimeProvider _time;

    public SynthesizeReview(ILlmClient llm, TimeProvider time)
    {
        _llm = llm;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        // All three analyses are available here after Join
        var blockingIssues = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<string>();

        // Process security findings
        if (state.Security!.HasCriticalIssues)
        {
            foreach (var finding in state.Security.Findings
                .Where(f => f.Severity == SecuritySeverity.Critical))
            {
                blockingIssues.Add(
                    $"CRITICAL: {finding.Description} in {finding.File}:{finding.Line}");
            }
        }

        foreach (var finding in state.Security.Findings
            .Where(f => f.Severity == SecuritySeverity.High))
        {
            warnings.Add(
                $"Security: {finding.Description} in {finding.File}:{finding.Line}");
        }

        // Process style violations
        foreach (var violation in state.Style!.Violations
            .Where(v => v.Severity == StyleSeverity.Error))
        {
            warnings.Add(
                $"Style: {violation.Message} in {violation.File}:{violation.Line}");
        }

        // Process complexity issues
        foreach (var issue in state.Complexity!.Issues
            .Where(i => i.Complexity > 15))
        {
            warnings.Add(
                $"Complexity: {issue.Method} has complexity {issue.Complexity}. {issue.Recommendation}");
        }

        foreach (var issue in state.Complexity.Issues
            .Where(i => i.Complexity > 10 && i.Complexity <= 15))
        {
            suggestions.Add(
                $"Consider simplifying {issue.Method} (complexity: {issue.Complexity})");
        }

        // Generate summary using LLM
        var summaryPrompt = BuildSummaryPrompt(state, blockingIssues, warnings, suggestions);
        var summaryResponse = await _llm.CompleteAsync(new CompletionRequest
        {
            Prompt = summaryPrompt,
            MaxTokens = 500,
            Temperature = 0.3
        }, ct);

        var outcome = DetermineOutcome(blockingIssues, warnings, state);

        var summary = new ReviewSummary(
            outcome,
            summaryResponse.Text,
            blockingIssues,
            warnings,
            suggestions);

        var decision = new ReviewDecision(
            outcome,
            GetDecisionReason(outcome, state),
            outcome == ReviewOutcome.RequestChanges,
            _time.GetUtcNow());

        var evt = new ReviewEvent(
            "ReviewSynthesized",
            _time.GetUtcNow(),
            "review-synthesizer",
            $"Review outcome: {outcome}, {blockingIssues.Count} blocking, {warnings.Count} warnings",
            new Dictionary<string, object>
            {
                ["outcome"] = outcome.ToString(),
                ["blocking_count"] = blockingIssues.Count,
                ["warning_count"] = warnings.Count,
                ["suggestion_count"] = suggestions.Count
            });

        return state
            .With(s => s.Summary, summary)
            .With(s => s.Decision, decision)
            .With(s => s.Status, ReviewStatus.AnalysisComplete)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }

    private static ReviewOutcome DetermineOutcome(
        List<string> blockingIssues,
        List<string> warnings,
        CodeReviewState state)
    {
        if (blockingIssues.Any()) return ReviewOutcome.Block;
        if (warnings.Any() || state.Security!.HighestSeverity >= SecuritySeverity.High)
            return ReviewOutcome.RequestChanges;
        return ReviewOutcome.Approve;
    }

    private static string GetDecisionReason(ReviewOutcome outcome, CodeReviewState state) =>
        outcome switch
        {
            ReviewOutcome.Block =>
                $"Blocked due to {state.Security?.Findings.Count(f => f.Severity == SecuritySeverity.Critical)} critical security issues",
            ReviewOutcome.RequestChanges =>
                "Requires human review due to security or complexity concerns",
            ReviewOutcome.Approve =>
                "All automated checks passed",
            _ => "Unknown"
        };

    private static string BuildSummaryPrompt(
        CodeReviewState state,
        List<string> blockingIssues,
        List<string> warnings,
        List<string> suggestions)
    {
        return $"""
            Summarize the following code review findings for PR #{state.PullRequest.Number}:
            "{state.PullRequest.Title}"

            ## Changes
            - Files changed: {state.PullRequest.Files.Count}
            - Lines added: {state.PullRequest.Additions}
            - Lines deleted: {state.PullRequest.Deletions}

            ## Security Analysis
            - Risk score: {state.Security?.RiskScore:P0}
            - Highest severity: {state.Security?.HighestSeverity}
            - Finding count: {state.Security?.Findings.Count}

            ## Style Analysis
            - Style score: {state.Style?.StyleScore:P0}
            - Violations: {state.Style?.ViolationCount}

            ## Complexity Analysis
            - Average complexity: {state.Complexity?.AverageCyclomaticComplexity:F1}
            - Max complexity: {state.Complexity?.MaxCyclomaticComplexity}
            - Issues: {state.Complexity?.Issues.Count}

            ## Blocking Issues ({blockingIssues.Count})
            {string.Join("\n", blockingIssues.Select(i => $"- {i}"))}

            ## Warnings ({warnings.Count})
            {string.Join("\n", warnings.Take(5).Select(w => $"- {w}"))}

            Write a concise 2-3 sentence summary suitable for posting as a PR comment.
            Focus on the most important findings and recommended actions.
            """;
    }
}
```

### AutoApprove

```csharp
public class AutoApprove : IWorkflowStep<CodeReviewState>
{
    private readonly IGitHubClient _github;
    private readonly TimeProvider _time;

    public AutoApprove(IGitHubClient github, TimeProvider time)
    {
        _github = github;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        await _github.CreateReviewAsync(
            state.PullRequest.Repository,
            state.PullRequest.Number,
            new ReviewSubmission
            {
                Event = ReviewAction.Approve,
                Body = "All automated checks passed. Auto-approved."
            }, ct);

        var evt = new ReviewEvent(
            "AutoApproved",
            _time.GetUtcNow(),
            "code-review-bot",
            "PR auto-approved after passing all checks",
            new Dictionary<string, object>
            {
                ["security_score"] = 1.0m - state.Security!.RiskScore,
                ["style_score"] = state.Style!.StyleScore,
                ["complexity_passes"] = state.Complexity!.PassesThreshold
            });

        return state
            .With(s => s.Status, ReviewStatus.Approved)
            .With(s => s.StatusUpdated, true)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }
}
```

### SeniorDeveloper Approver

```csharp
public class SeniorDeveloper : IApprover<CodeReviewState>
{
    public string Role => "senior-developer";

    public ApprovalRequest CreateRequest(CodeReviewState state)
    {
        return new ApprovalRequest
        {
            Title = $"Review Required: PR #{state.PullRequest.Number}",
            Description = state.Summary!.SummaryText,
            Context = new Dictionary<string, object>
            {
                ["pr_number"] = state.PullRequest.Number,
                ["repository"] = state.PullRequest.Repository,
                ["title"] = state.PullRequest.Title,
                ["author"] = state.PullRequest.Author,
                ["security_risk"] = state.Security!.RiskScore,
                ["blocking_issues"] = state.Summary.BlockingIssues,
                ["warnings"] = state.Summary.Warnings,
                ["suggestions"] = state.Summary.Suggestions
            }
        };
    }

    public CodeReviewState ApplyApproval(CodeReviewState state, HumanReviewResponse response)
    {
        return state
            .With(s => s.HumanReview, response)
            .With(s => s.Status, response.Approved
                ? ReviewStatus.Approved
                : ReviewStatus.ChangesRequested);
    }
}
```

### ProcessHumanDecision

```csharp
public class ProcessHumanDecision : IWorkflowStep<CodeReviewState>
{
    private readonly IGitHubClient _github;
    private readonly TimeProvider _time;

    public ProcessHumanDecision(IGitHubClient github, TimeProvider time)
    {
        _github = github;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        var review = state.HumanReview!;

        var reviewAction = review.Approved
            ? ReviewAction.Approve
            : ReviewAction.RequestChanges;

        var body = review.Approved
            ? $"Approved by {review.ReviewerId}. {review.Comments}"
            : $"Changes requested by {review.ReviewerId}:\n\n{string.Join("\n", review.RequestedChanges.Select(c => $"- {c}"))}";

        await _github.CreateReviewAsync(
            state.PullRequest.Repository,
            state.PullRequest.Number,
            new ReviewSubmission
            {
                Event = reviewAction,
                Body = body
            }, ct);

        var evt = new ReviewEvent(
            review.Approved ? "HumanApproved" : "ChangesRequested",
            _time.GetUtcNow(),
            review.ReviewerId,
            review.Approved ? "Human reviewer approved PR" : "Human reviewer requested changes",
            new Dictionary<string, object>
            {
                ["reviewer"] = review.ReviewerId,
                ["approved"] = review.Approved,
                ["requested_changes_count"] = review.RequestedChanges.Count
            });

        return state
            .With(s => s.StatusUpdated, true)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }
}
```

### BlockPullRequest

```csharp
public class BlockPullRequest : IWorkflowStep<CodeReviewState>
{
    private readonly IGitHubClient _github;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;

    public BlockPullRequest(
        IGitHubClient github,
        INotificationService notifications,
        TimeProvider time)
    {
        _github = github;
        _notifications = notifications;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        // Add blocking review to PR
        await _github.CreateReviewAsync(
            state.PullRequest.Repository,
            state.PullRequest.Number,
            new ReviewSubmission
            {
                Event = ReviewAction.RequestChanges,
                Body = BuildBlockingComment(state)
            }, ct);

        // Notify security team for critical issues
        if (state.Security!.HasCriticalIssues)
        {
            await _notifications.SendSecurityAlertAsync(new SecurityAlertNotification
            {
                Repository = state.PullRequest.Repository,
                PrNumber = state.PullRequest.Number,
                Author = state.PullRequest.Author,
                CriticalFindings = state.Security.Findings
                    .Where(f => f.Severity == SecuritySeverity.Critical)
                    .ToList()
            }, ct);
        }

        var evt = new ReviewEvent(
            "PullRequestBlocked",
            _time.GetUtcNow(),
            "code-review-bot",
            $"PR blocked due to {state.Summary!.BlockingIssues.Count} critical issues",
            new Dictionary<string, object>
            {
                ["blocking_issues"] = state.Summary.BlockingIssues,
                ["critical_security_count"] = state.Security.Findings
                    .Count(f => f.Severity == SecuritySeverity.Critical)
            });

        return state
            .With(s => s.Status, ReviewStatus.Blocked)
            .With(s => s.StatusUpdated, true)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }

    private static string BuildBlockingComment(CodeReviewState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## PR Blocked - Critical Issues Found");
        sb.AppendLine();
        sb.AppendLine("This PR cannot be merged until the following critical issues are resolved:");
        sb.AppendLine();

        foreach (var issue in state.Summary!.BlockingIssues)
        {
            sb.AppendLine($"- {issue}");
        }

        sb.AppendLine();
        sb.AppendLine("Please address these issues and push new commits to trigger re-review.");

        return sb.ToString();
    }
}
```

### PostReviewComment

```csharp
public class PostReviewComment : IWorkflowStep<CodeReviewState>
{
    private readonly IGitHubClient _github;
    private readonly TimeProvider _time;

    public PostReviewComment(IGitHubClient github, TimeProvider time)
    {
        _github = github;
        _time = time;
    }

    public async Task<StepResult<CodeReviewState>> ExecuteAsync(
        CodeReviewState state,
        StepContext context,
        CancellationToken ct)
    {
        // Post detailed analysis comment
        var comment = BuildDetailedComment(state);

        await _github.CreateIssueCommentAsync(
            state.PullRequest.Repository,
            state.PullRequest.Number,
            comment, ct);

        var evt = new ReviewEvent(
            "ReviewCommentPosted",
            _time.GetUtcNow(),
            "code-review-bot",
            "Posted detailed review comment to PR",
            new Dictionary<string, object>
            {
                ["final_status"] = state.Status.ToString(),
                ["total_events"] = state.Events.Count + 1
            });

        return state
            .With(s => s.CommentPosted, true)
            .With(s => s.Events, state.Events.Add(evt))
            .AsResult();
    }

    private static string BuildDetailedComment(CodeReviewState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Automated Code Review - {state.Status}");
        sb.AppendLine();
        sb.AppendLine(state.Summary!.SummaryText);
        sb.AppendLine();
        sb.AppendLine("### Analysis Details");
        sb.AppendLine();
        sb.AppendLine("| Check | Score | Status |");
        sb.AppendLine("|-------|-------|--------|");
        sb.AppendLine($"| Security | {1 - state.Security!.RiskScore:P0} | {GetStatusEmoji(state.Security.RiskScore < 0.3m)} |");
        sb.AppendLine($"| Style | {state.Style!.StyleScore:P0} | {GetStatusEmoji(state.Style.PassesThreshold)} |");
        sb.AppendLine($"| Complexity | {GetComplexityScore(state.Complexity!):P0} | {GetStatusEmoji(state.Complexity.PassesThreshold)} |");
        sb.AppendLine();

        if (state.Summary.Warnings.Any())
        {
            sb.AppendLine("### Warnings");
            foreach (var warning in state.Summary.Warnings.Take(10))
            {
                sb.AppendLine($"- {warning}");
            }
            if (state.Summary.Warnings.Count > 10)
            {
                sb.AppendLine($"- ... and {state.Summary.Warnings.Count - 10} more");
            }
            sb.AppendLine();
        }

        if (state.Summary.Suggestions.Any())
        {
            sb.AppendLine("### Suggestions");
            foreach (var suggestion in state.Summary.Suggestions.Take(5))
            {
                sb.AppendLine($"- {suggestion}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"*Workflow ID: {state.WorkflowId}*");

        return sb.ToString();
    }

    private static string GetStatusEmoji(bool passes) => passes ? "Pass" : "Needs Attention";

    private static decimal GetComplexityScore(ComplexityAnalysis complexity)
    {
        if (complexity.MaxCyclomaticComplexity == 0) return 1.0m;
        return Math.Max(0m, 1.0m - (complexity.MaxCyclomaticComplexity / 30m));
    }
}
```

## Service Interfaces

```csharp
public interface IGitHubClient
{
    Task<PullRequestDetails> GetPullRequestAsync(string repo, int number, CancellationToken ct);
    Task<IReadOnlyList<GitHubFile>> GetPullRequestFilesAsync(string repo, int number, CancellationToken ct);
    Task CreateReviewAsync(string repo, int number, ReviewSubmission submission, CancellationToken ct);
    Task CreateIssueCommentAsync(string repo, int number, string comment, CancellationToken ct);
}

public interface ISecurityScanner
{
    Task<IReadOnlyList<SecurityFinding>> ScanFileAsync(SecurityScanRequest request, CancellationToken ct);
}

public interface IStyleChecker
{
    Task<IReadOnlyList<StyleViolation>> CheckFileAsync(StyleCheckRequest request, CancellationToken ct);
}

public interface IComplexityAnalyzer
{
    Task<ComplexityResult> AnalyzeFileAsync(ComplexityRequest request, CancellationToken ct);
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
    .AddWorkflow<CodeReviewWorkflow>();

// GitHub integration
builder.Services.AddSingleton<IGitHubClient, OctokitGitHubClient>();
builder.Services.Configure<GitHubOptions>(builder.Configuration.GetSection("GitHub"));

// Analysis services
builder.Services.AddScoped<ISecurityScanner, SemgrepSecurityScanner>();
builder.Services.AddScoped<IStyleChecker, EditorConfigStyleChecker>();
builder.Services.AddScoped<IComplexityAnalyzer, RoslynComplexityAnalyzer>();
builder.Services.AddScoped<ILlmClient, OpenAiLlmClient>();
builder.Services.AddScoped<INotificationService, SlackNotificationService>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

## Starting the Workflow (GitHub Webhook)

```csharp
[ApiController]
[Route("api/webhooks/github")]
public class GitHubWebhookController : ControllerBase
{
    private readonly IWorkflowStarter _workflowStarter;

    public GitHubWebhookController(IWorkflowStarter workflowStarter)
    {
        _workflowStarter = workflowStarter;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook(
        [FromHeader(Name = "X-GitHub-Event")] string eventType,
        [FromBody] JsonDocument payload,
        CancellationToken ct)
    {
        if (eventType != "pull_request")
        {
            return Ok();
        }

        var action = payload.RootElement.GetProperty("action").GetString();
        if (action is not ("opened" or "synchronize" or "reopened"))
        {
            return Ok();
        }

        var pr = payload.RootElement.GetProperty("pull_request");
        var repo = payload.RootElement.GetProperty("repository");

        var workflowId = Guid.NewGuid();

        var pullRequest = new PullRequest(
            Number: pr.GetProperty("number").GetInt32(),
            Repository: repo.GetProperty("full_name").GetString()!,
            Title: pr.GetProperty("title").GetString()!,
            Author: pr.GetProperty("user").GetProperty("login").GetString()!,
            BaseBranch: pr.GetProperty("base").GetProperty("ref").GetString()!,
            HeadBranch: pr.GetProperty("head").GetProperty("ref").GetString()!,
            HeadSha: pr.GetProperty("head").GetProperty("sha").GetString()!,
            Files: [], // Will be fetched in first step
            Additions: pr.GetProperty("additions").GetInt32(),
            Deletions: pr.GetProperty("deletions").GetInt32());

        var initialState = new CodeReviewState
        {
            WorkflowId = workflowId,
            PullRequest = pullRequest,
            Events =
            [
                new ReviewEvent(
                    "WorkflowStarted",
                    DateTimeOffset.UtcNow,
                    "github-webhook",
                    $"Code review workflow started for PR #{pullRequest.Number}",
                    new Dictionary<string, object>
                    {
                        ["action"] = action,
                        ["pr_number"] = pullRequest.Number
                    })
            ]
        };

        await _workflowStarter.StartAsync("code-review", initialState, ct);

        return Ok(new { WorkflowId = workflowId });
    }
}
```

## Generated Artifacts

### Phase Enum

```csharp
public enum CodeReviewPhase
{
    NotStarted,
    FetchPullRequestDetails,
    SecurityAnalysisStep,
    StyleAnalysisStep,
    ComplexityAnalysisStep,
    SynthesizeReview,
    AutoApprove,
    AwaitingApproval,
    EscalateToTeamLead,
    ProcessHumanDecision,
    BlockPullRequest,
    PostReviewComment,
    Completed,
    Failed
}
```

### Fork Handler

```csharp
// After FetchPullRequestDetails - cascades to all fork paths
public async Task<object[]> Handle(
    ExecuteFetchPullRequestDetailsCommand command,
    FetchPullRequestDetails step,
    CancellationToken ct)
{
    var result = await step.ExecuteAsync(State, ct);
    State = CodeReviewStateReducer.Reduce(State, result.StateUpdate);

    // Return commands for all fork paths (executed in parallel)
    return [
        new ExecuteSecurityAnalysisStepCommand(WorkflowId),
        new ExecuteStyleAnalysisStepCommand(WorkflowId),
        new ExecuteComplexityAnalysisStepCommand(WorkflowId)
    ];
}
```

## Querying the Audit Trail

```csharp
// Query all events for compliance audit
var events = await session
    .Query<CodeReviewSaga>()
    .Where(s => s.State.PullRequest.Repository == "my-org/my-repo")
    .SelectMany(s => s.State.Events)
    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
    .ToListAsync();

// Find all blocked PRs
var blockedPrs = await session
    .Query<CodeReviewSaga>()
    .Where(s => s.State.Status == ReviewStatus.Blocked)
    .ToListAsync();

// Get security findings summary
var securityReport = await session
    .Query<CodeReviewSaga>()
    .Where(s => s.State.Security!.HasCriticalIssues)
    .Select(s => new
    {
        s.State.PullRequest.Repository,
        s.State.PullRequest.Number,
        s.State.Security!.Findings
    })
    .ToListAsync();
```

## Key Points

- **Fork/Join** enables parallel analysis for faster review cycles
- **Conditional branching** routes based on severity findings
- **Human-in-the-loop** for cases requiring judgment
- **Event sourcing** via `[Append]` provides complete audit trail
- **GitHub integration** via webhooks for automatic triggering
- **Comprehensive state** captures all analysis results
- **LLM summarization** provides human-readable review summaries

## Related Documentation

- [Fork/Join Example](./fork-join.md) - Parallel execution pattern
- [Branching Example](./branching.md) - Conditional routing
- [Approval Flow Example](./approval-flow.md) - Human approval patterns
