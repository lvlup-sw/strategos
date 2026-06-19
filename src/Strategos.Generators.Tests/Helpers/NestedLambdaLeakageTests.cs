// -----------------------------------------------------------------------
// <copyright file="NestedLambdaLeakageTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// CodeRabbit F1 (PR #137, epic #135) — regression tests for nested-lambda leakage
/// across the step-resilience / step-discovery / step-validation extraction sites that walk a
/// configure/handler lambda with <c>DescendantNodes()</c>:
/// <list type="bullet">
///   <item>parent-step resilience (<c>StepExtractor.ExtractConfiguredResilience</c>),</item>
///   <item>branch-path step discovery (<c>StepExtractor.ParseBranchPathStepModels</c>),</item>
///   <item>failure-handler step discovery (<c>FailureHandlerExtractor.ParseFailureHandlerBody</c>),</item>
///   <item>parent-step validation (<c>StepExtractor.ExtractConfiguredValidation</c>) — G-6 review (#143).</item>
/// </list>
/// Each site must consider ONLY invocations in its own lambda body, never those captured
/// from a nested lambda (e.g. an inner <c>OnLowConfidence(alt =&gt; alt.Then&lt;Y&gt;(c =&gt; c.WithTimeout(t)))</c>).
/// INV-1: lowering stays correct; INV-7: immutability untouched.
/// </summary>
[Property("Category", "Unit")]
public sealed class NestedLambdaLeakageTests
{
    // =========================================================================
    // F1a — parent-step resilience must NOT absorb a nested lambda's resilience.
    // =========================================================================

    /// <summary>
    /// The outer step declares <c>WithRetry(2)</c> and an <c>OnLowConfidence</c> whose
    /// handler step declares <c>WithTimeout(...)</c> via its own configure lambda. The
    /// outer step must carry the retry but NOT the inner handler's timeout.
    /// </summary>
    [Test]
    public async Task ExtractResilience_NestedOnLowConfidenceTimeout_DoesNotLeakToOuterStep()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(NestedConfidenceTimeoutWorkflow);

        // Act
        var outerStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Assert - outer step keeps its own retry
        await Assert.That(outerStep.Retry).IsNotNull();
        await Assert.That(outerStep.Retry!.MaxAttempts).IsEqualTo(2);

        // Assert - the inner handler's WithTimeout must NOT leak onto the outer step
        await Assert.That(outerStep.Timeout).IsNull();
    }

    /// <summary>
    /// The legitimately-nested <c>OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;(...))</c>
    /// handler step must still be identified on the outer step's confidence model — the
    /// nested-lambda boundary fix must not drop config that belongs to the inner handler.
    /// (Threshold + handler identity survive; the handler step's own resilience extraction
    /// is a separate concern — DR-5 lowers the handler by name/type, not its retry/timeout.)
    /// </summary>
    [Test]
    public async Task ExtractResilience_NestedOnLowConfidenceTimeout_HandlerStepStillIdentified()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(NestedConfidenceTimeoutWorkflow);
        var outerStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Act - the confidence model + handler step are carried on the outer step
        var confidence = outerStep.Confidence;
        var handlerStep = confidence?.OnLowConfidenceHandlerStep;

        // Assert - the threshold and low-confidence handler survive the boundary fix
        await Assert.That(confidence).IsNotNull();
        await Assert.That(confidence!.Threshold).IsEqualTo(0.85);
        await Assert.That(confidence.OnLowConfidenceHandlerId).IsEqualTo("HumanReview");
        await Assert.That(handlerStep).IsNotNull();
        await Assert.That(handlerStep!.StepName).IsEqualTo("HumanReview");
    }

    // =========================================================================
    // F1b — branch-path step discovery must NOT pick up nested Then<> calls.
    // =========================================================================

    /// <summary>
    /// A branch case path step declares an <c>OnLowConfidence(alt =&gt; alt.Then&lt;Inner&gt;())</c>.
    /// The branch-path step discovery must yield only the outer <c>Then&lt;Outer&gt;</c> step,
    /// never the nested <c>Then&lt;Inner&gt;</c> handler step (which is lowered via the
    /// confidence model, not as an outer branch step).
    /// </summary>
    [Test]
    public async Task BranchPath_NestedThenInOnLowConfidence_NotPickedUpAsBranchStep()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(BranchNestedThenWorkflow);

        // Act
        var innerOccurrences = stepModels.Count(s => s.StepName == "InnerHandler");
        var outerOccurrences = stepModels.Count(s => s.StepName == "ProcessAuto");

        // Assert - the outer branch step is discovered once
        await Assert.That(outerOccurrences).IsEqualTo(1);

        // Assert - the nested Then<InnerHandler> is NOT discovered as a branch-path step
        await Assert.That(innerOccurrences).IsEqualTo(0);
    }

    // =========================================================================
    // F1c — failure-handler step discovery must NOT pick up nested Then<> calls.
    // =========================================================================

    /// <summary>
    /// A workflow failure handler declares a step whose configure lambda nests an
    /// <c>OnLowConfidence(alt =&gt; alt.Then&lt;Nested&gt;())</c>. The failure-handler body
    /// walk must yield only the outer handler step, never the nested <c>Then&lt;Nested&gt;</c>.
    /// </summary>
    [Test]
    public async Task FailureHandler_NestedThenInConfigLambda_NotPickedUpAsHandlerStep()
    {
        // Arrange — build the context from the workflow-attributed class so the
        // OnFailure invocation is in scope (the first type declaration is a record state).
        var context = CreateWorkflowParseContext(FailureHandlerNestedThenWorkflow, "nested-failure");

        // Act
        var handlers = FailureHandlerExtractor.Extract(context);
        var handler = handlers.Single();
        var stepNames = (handler.Steps ?? []).Select(s => s.StepName).ToList();

        // Assert - the outer failure-handler step is present
        await Assert.That(stepNames).Contains("LogFailure");

        // Assert - the nested Then<NestedRecovery> is NOT a failure-handler step
        await Assert.That(stepNames.Contains("NestedRecovery")).IsFalse();
    }

    // =========================================================================
    // F1d — parent-step validation must NOT absorb a nested lambda's ValidateState.
    // (G-6 review, #143) The top-level fallback added in TryGetStepModel made the
    // latent unscoped DescendantNodes() walk in ExtractConfiguredValidation reachable:
    // a top-level step with NO direct ValidateState but a nested
    // OnLowConfidence(alt => alt.Then<HumanReview>(c => c.ValidateState(...))) would
    // wrongly absorb the inner handler's guard.
    // =========================================================================

    /// <summary>
    /// The top-level <c>AssessClaim</c> step declares NO direct <c>ValidateState</c>, only an
    /// <c>OnLowConfidence</c> whose handler step declares <c>ValidateState</c> via its own configure
    /// lambda. The top-level step must NOT carry the inner handler's validation guard.
    /// </summary>
    [Test]
    public async Task ExtractValidation_NestedOnLowConfidenceValidateState_DoesNotLeakToOuterStep()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(NestedConfidenceValidationWorkflow);

        // Act
        var outerStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Assert - the inner handler's ValidateState must NOT leak onto the outer step
        await Assert.That(outerStep.HasValidation).IsFalse();
        await Assert.That(outerStep.ValidationPredicate).IsNull();
    }

    /// <summary>
    /// The legitimately-nested <c>OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;(...))</c>
    /// handler step must still be identified on the outer step's confidence model — the
    /// nested-lambda boundary fix must not drop config that belongs to the inner handler.
    /// (The handler step's own validation extraction is a separate concern — DR-5 lowers the
    /// handler by name/type, mirroring the F1a resilience assertion.)
    /// </summary>
    [Test]
    public async Task ExtractValidation_NestedOnLowConfidenceValidateState_HandlerStepStillIdentified()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(NestedConfidenceValidationWorkflow);
        var outerStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Act
        var confidence = outerStep.Confidence;
        var handlerStep = confidence?.OnLowConfidenceHandlerStep;

        // Assert - the threshold and low-confidence handler survive the boundary fix
        await Assert.That(confidence).IsNotNull();
        await Assert.That(confidence!.Threshold).IsEqualTo(0.85);
        await Assert.That(confidence.OnLowConfidenceHandlerId).IsEqualTo("HumanReview");
        await Assert.That(handlerStep).IsNotNull();
        await Assert.That(handlerStep!.StepName).IsEqualTo("HumanReview");
    }

    // =========================================================================
    // Local harness — build a parse context anchored on the workflow class.
    // =========================================================================

    /// <summary>
    /// Compiles <paramref name="source"/> and builds a <see cref="FluentDslParseContext"/>
    /// anchored on the <c>[Workflow]</c>-attributed class (not the first type declaration,
    /// which is a record state) so chain invocations such as <c>OnFailure</c> are in scope.
    /// </summary>
    private static FluentDslParseContext CreateWorkflowParseContext(string source, string workflowName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .Append(MetadataReference.CreateFromFile(
                typeof(Strategos.Abstractions.IWorkflowState).Assembly.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        var workflowClass = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .First(t => t.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Workflow", StringComparison.Ordinal)));

        return FluentDslParseContext.Create(workflowClass, semanticModel, workflowName, CancellationToken.None);
    }

    // =========================================================================
    // Test source workflows
    // =========================================================================

    /// <summary>
    /// An outer step carrying <c>WithRetry(2)</c> plus an <c>OnLowConfidence</c> whose
    /// handler step declares its own <c>WithTimeout(TimeSpan.FromSeconds(45))</c> via a
    /// nested configure lambda. Probes parent-step resilience leakage (F1a).
    /// </summary>
    private const string NestedConfidenceTimeoutWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AssessClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("nested-confidence-timeout")]
        public static partial class NestedConfidenceTimeoutWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("nested-confidence-timeout")
                .StartWith<IntakeClaim>()
                .Then<AssessClaim>(step => step
                    .WithRetry(2)
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<HumanReview>(h => h
                        .WithTimeout(TimeSpan.FromSeconds(45)))))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A branch case path whose step declares an <c>OnLowConfidence(alt =&gt; alt.Then&lt;InnerHandler&gt;())</c>.
    /// Probes branch-path step-discovery leakage (F1b).
    /// </summary>
    private const string BranchNestedThenWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimType { Auto, Home }

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimType Type { get; init; }
        }

        public class ValidateClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ProcessAuto : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ProcessHome : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class InnerHandler : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class CompleteClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("branch-nested-then")]
        public static partial class BranchNestedThenWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("branch-nested-then")
                .StartWith<ValidateClaim>()
                .Branch(state => state.Type,
                    BranchCase<ClaimState, ClaimType>.When(ClaimType.Auto, path => path.Then<ProcessAuto>(step => step
                        .RequireConfidence(0.7)
                        .OnLowConfidence(alt => alt.Then<InnerHandler>()))),
                    BranchCase<ClaimState, ClaimType>.Otherwise(path => path.Then<ProcessHome>()))
                .Finally<CompleteClaim>();
        }
        """;

    /// <summary>
    /// A workflow whose failure handler step nests an <c>OnLowConfidence(alt =&gt; alt.Then&lt;NestedRecovery&gt;())</c>.
    /// Probes failure-handler step-discovery leakage (F1c).
    /// </summary>
    private const string FailureHandlerNestedThenWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class PlaceOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class LogFailure : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class NestedRecovery : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class CompleteOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("nested-failure")]
        public static partial class NestedFailureWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("nested-failure")
                .StartWith<PlaceOrder>()
                .OnFailure(f => f.Then<LogFailure>(step => step
                    .RequireConfidence(0.6)
                    .OnLowConfidence(alt => alt.Then<NestedRecovery>())).Complete())
                .Finally<CompleteOrder>();
        }
        """;

    /// <summary>
    /// A top-level <c>StartWith&lt;AssessClaim&gt;</c> step carrying NO direct <c>ValidateState</c>,
    /// only an <c>OnLowConfidence</c> whose handler step (<c>HumanReview</c>) declares its own
    /// <c>ValidateState</c> via a nested configure lambda. Probes parent-step validation leakage (F1d).
    /// </summary>
    private const string NestedConfidenceValidationWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool Ok { get; init; }
        }

        public class AssessClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class Finish : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("nested-confidence-validation")]
        public static partial class NestedConfidenceValidationWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("nested-confidence-validation")
                .StartWith<AssessClaim>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<HumanReview>(c => c.ValidateState(s => s.Ok, "inner-only"))))
                .Finally<Finish>();
        }
        """;
}
