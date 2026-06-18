// -----------------------------------------------------------------------
// <copyright file="BranchFailureConfigExpressibilityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Builders;

/// <summary>
/// Tests that resilience configuration declared via the
/// <c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(...))</c> configure-lambda overload on the
/// <see cref="Strategos.Builders.IBranchBuilder{TState}"/> branch builder and the
/// <see cref="Strategos.Builders.IFailureBuilder{TState}"/> failure-handler builder reaches the
/// generator's <see cref="StepModel"/> IR (epic #135, DR-7).
/// </summary>
/// <remarks>
/// <para>
/// The configure overload (carrying <c>.WithRetry/.WithTimeout/.Compensate/.RequireConfidence/
/// .OnLowConfidence/.ValidateState/.WithContext</c>) already exists on the top-level
/// <see cref="Strategos.Builders.IWorkflowBuilder{TState}"/>, the loop-body
/// <see cref="Strategos.Builders.ILoopBuilder{TState}"/>, and (via #134) the fork-path
/// <see cref="Strategos.Builders.IForkPathBuilder{TState}"/> builder. DR-7 closes the
/// expressibility gap by adding it to the branch and failure-handler builders too.
/// </para>
/// <para>
/// These tests drive a workflow snippet through <see cref="ParserTestHelper.ExtractStepModels"/>,
/// which routes through <c>FluentDslParser.ExtractStepModels</c> →
/// <see cref="StepExtractor.ExtractStepModels"/>. The shared <c>TryGetStepModel</c> path invokes
/// <c>ExtractConfiguredResilience</c> on each <c>Then</c> invocation, so config declared in any
/// context the walker descends into is captured as a populated <see cref="RetryModel"/> on the
/// step's <see cref="StepModel"/>.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class BranchFailureConfigExpressibilityTests
{
    /// <summary>
    /// Verifies that <c>.WithRetry(2)</c> declared via the new branch-builder configure overload
    /// (<c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(2))</c>) populates the branch step's
    /// <see cref="StepModel.Retry"/>.
    /// </summary>
    [Test]
    public async Task BranchBuilder_ThenWithConfig_StepModelCarriesRetry()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(BranchConfigWorkflow);

        // Act
        var branchStep = stepModels.Single(s => s.StepName == "EscalateClaim");

        // Assert
        await Assert.That(branchStep.Retry).IsNotNull();
        await Assert.That(branchStep.Retry!.MaxAttempts).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that <c>.WithRetry(2)</c> declared via the failure-handler-builder configure
    /// overload (<c>OnFailure(f =&gt; f.Then&lt;TStep&gt;(step =&gt; step.WithRetry(2)))</c>) populates
    /// the failure-handler step's <see cref="StepModel.Retry"/>.
    /// </summary>
    /// <remarks>
    /// The recovery step lives inside the <c>OnFailure(...)</c> handler lambda, so it is owned by
    /// <see cref="FailureHandlerExtractor"/> — NOT by the main-chain <c>ExtractStepModels</c> walk.
    /// (Before the F1 nested-lambda-boundary fix, the fork-path walk leaked the handler-nested
    /// <c>Then&lt;RefundPayment&gt;</c> into the fork-path step models, which is the very bug F1
    /// closes; the correct assertion is against the failure-handler model.)
    /// </remarks>
    [Test]
    public async Task FailureHandlerBuilder_ThenWithConfig_StepModelCarriesRetry()
    {
        // Arrange
        var context = CreateWorkflowParseContext(FailureHandlerConfigWorkflow, "failure-config");

        // Act
        var handlers = FailureHandlerExtractor.Extract(context);
        var recoveryStep = handlers
            .SelectMany(h => h.Steps ?? [])
            .Single(s => s.StepName == "RefundPayment");

        // Assert
        await Assert.That(recoveryStep.Retry).IsNotNull();
        await Assert.That(recoveryStep.Retry!.MaxAttempts).IsEqualTo(2);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> and builds a <see cref="FluentDslParseContext"/>
    /// anchored on the <c>[Workflow]</c>-attributed class so chain invocations such as
    /// <c>OnFailure</c> are in scope (the first type declaration is a record state).
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
    /// A workflow whose branch case declares a step's retry via the configure lambda,
    /// exercising the new branch-builder <c>Then&lt;TStep&gt;(Action&lt;IStepConfiguration&lt;TState&gt;&gt;)</c>
    /// overload.
    /// </summary>
    private const string BranchConfigWorkflow = """
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
            public bool RequiresEscalation { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class EscalateClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AutoApproveClaim : IWorkflowStep<ClaimState>
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

        [Workflow("branch-config")]
        public static partial class BranchConfigWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("branch-config")
                .StartWith<IntakeClaim>()
                .Branch(state => state.RequiresEscalation,
                    BranchCase.When(true, path => path
                        .Then<EscalateClaim>(step => step.WithRetry(2))),
                    BranchCase.Otherwise(path => path
                        .Then<AutoApproveClaim>()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A workflow whose fork-path failure handler declares a recovery step's retry via the
    /// configure lambda, exercising the new failure-handler-builder
    /// <c>Then&lt;TStep&gt;(Action&lt;IStepConfiguration&lt;TState&gt;&gt;)</c> overload.
    /// </summary>
    private const string FailureHandlerConfigWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record CheckoutState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateCart : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class ChargeCard : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class RefundPayment : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class ReserveStock : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class FinalizeOrder : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class CompleteCheckout : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        [Workflow("failure-config")]
        public static partial class FailureHandlerConfigWorkflow
        {
            public static WorkflowDefinition<CheckoutState> Definition => Workflow<CheckoutState>
                .Create("failure-config")
                .StartWith<ValidateCart>()
                .Fork(
                    path => path.Then<ChargeCard>()
                        .OnFailure(f => f.Then<RefundPayment>(step => step.WithRetry(2))),
                    path => path.Then<ReserveStock>())
                .Join<FinalizeOrder>()
                .Finally<CompleteCheckout>();
        }
        """;
}
