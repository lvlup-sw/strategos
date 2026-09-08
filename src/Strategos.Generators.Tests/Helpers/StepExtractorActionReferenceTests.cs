// -----------------------------------------------------------------------
// <copyright file="StepExtractorActionReferenceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

using Strategos.Builders;
using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Tests the occurrence-scoped workflow action identity from fluent syntax into
/// the generator's closed, name-only step model.
/// </summary>
[Property("Category", "Unit")]
public sealed class StepExtractorActionReferenceTests
{
    /// <summary>
    /// Forcing-function inventory for every public occurrence-authoring callback. A new builder
    /// overload that can receive <c>IStepConfiguration&lt;TState&gt;</c> must be added here and
    /// to an extraction vector before it can enter the public surface unnoticed.
    /// </summary>
    [Test]
    public async Task PublicConfiguredOccurrenceSurface_MatchesActionExtractionInventory()
    {
        string[] expected =
        [
            "IApprovalEscalationBuilder.Then(configure)",
            "IApprovalRejectionBuilder.Then(configure)",
            "IBranchBuilder.Then(configure)",
            "IFailureBuilder.Then(configure)",
            "IForkJoinBuilder.Join(configure)",
            "IForkPathBuilder.Then(configure)",
            "ILoopBuilder.Then(configure)",
            "ILoopForkJoinBuilder.Join(configure)",
            "IWorkflowBuilder.Finally(configure)",
            "IWorkflowBuilder.StartWith(configure)",
            "IWorkflowBuilder.StartWith(string,configure)",
            "IWorkflowBuilder.Then(configure)",
        ];

        var actual = typeof(IWorkflowBuilder<>).Assembly
            .GetTypes()
            .Where(static type =>
                type.IsInterface &&
                string.Equals(type.Namespace, "Strategos.Builders", StringComparison.Ordinal))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static method => method.GetParameters().Any(IsStepConfigurationCallback))
            .Select(static method =>
                $"{method.DeclaringType!.Name.Split('`')[0]}.{method.Name}(" +
                string.Join(",", method.GetParameters().Select(DescribeParameter)) + ")")
            .OrderBy(static signature => signature, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected);
        await Assert.That(actual.Length).IsEqualTo(expected.Length);
    }

    /// <summary>
    /// The validated parser entry points must reject a broken fixture before a semantic-model
    /// assertion can turn the compiler error into a false green.
    /// </summary>
    [Test]
    public async Task ValidatedParserEntryPoints_WithCompilerError_RejectFixture()
    {
        var invalidSource = Source("""
            .StartWith<First>()
            .Then<Middle>(step => step.Performs(MissingActionReference))
            .Finally<Last>()
            """);

        await Assert.That(() => ParserTestHelper.ExtractStepModelsValidated(invalidSource))
            .Throws<InvalidOperationException>();
        await Assert.That(() => ParserTestHelper.CompileWorkflowValidated(invalidSource))
            .Throws<InvalidOperationException>();
    }

    /// <summary>
    /// Entry, ordinary, and terminal configure overloads retain all three names,
    /// including named constructor arguments whose source order differs.
    /// </summary>
    [Test]
    public async Task Extract_LinearConfiguredSteps_ResolvesEveryNameWithoutLoss()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "receive")))
            .Then<Middle>(step => step
                .Performs(new WorkflowActionReference(
                    actionName: "process",
                    domainName: "orders",
                    objectTypeName: "Order"))
                .WithRetry(2))
            .Finally<Last>(step => step.Performs(
                new WorkflowActionReference("orders", "Order", "complete")))
            """));

        await AssertResolved(steps.Single(step => step.StepName == "First"), "receive");
        await AssertResolved(steps.Single(step => step.StepName == "Middle"), "process");
        await AssertResolved(steps.Single(step => step.StepName == "Last"), "complete");
        await Assert.That(steps.Single(step => step.StepName == "Middle").Retry!.MaxAttempts)
            .IsEqualTo(2);
    }

    /// <summary>The combined entry instance-name/configuration overload retains both identities.</summary>
    [Test]
    public async Task Extract_NamedConfiguredEntry_ResolvesPhaseAndAction()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>("NamedEntry", step => step.Performs(
                new WorkflowActionReference("orders", "Order", "receive")))
            .Finally<Last>()
            """));

        var entry = steps.Single(step => step.StepName == "First");
        await Assert.That(entry.InstanceName).IsEqualTo("NamedEntry");
        await Assert.That(entry.PhaseName).IsEqualTo("NamedEntry");
        await AssertResolved(entry, "receive");
    }

    /// <summary>
    /// Fluent configuration order does not affect action identity extraction, including
    /// when validation's nested predicate precedes the final <c>Performs</c> call.
    /// </summary>
    [Test]
    public async Task Extract_PerformsLastAfterConfiguration_ResolvesOccurrenceAction()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Then<Middle>(step => step
                .WithRetry(3)
                .ValidateState(state => state.Done, "Must be done")
                .Performs(new WorkflowActionReference("orders", "Order", "process")))
            .Finally<Last>()
            """));

        var middle = steps.Single(step => step.StepName == "Middle");
        await AssertResolved(middle, "process");
        await Assert.That(middle.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(middle.ValidationErrorMessage).IsEqualTo("Must be done");
    }

    /// <summary>
    /// Missing declarations stay missing, while factory calls, multiple declarations,
    /// and blank literal names are retained as authored-but-unprovable.
    /// </summary>
    [Test]
    public async Task Extract_UnresolvedForms_PreserveResolutionReason()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Then<Middle>(step => step.Performs(ActionFactory.Create()))
            .Then<Third>(step =>
            {
                step.Performs(new WorkflowActionReference("orders", "Order", "one"));
                step.Performs(new WorkflowActionReference("orders", "Order", "two"));
            })
            .Then<Blank>(step => step.Performs(
                new WorkflowActionReference("orders", " ", "blank")))
            .Finally<Last>()
            """));

        await AssertMissing(steps.Single(step => step.StepName == "First"));
        await AssertDynamic(steps.Single(step => step.StepName == "Middle"));
        await AssertDynamic(steps.Single(step => step.StepName == "Third"));
        await AssertDynamic(steps.Single(step => step.StepName == "Blank"));
        await AssertMissing(steps.Single(step => step.StepName == "Last"));
    }

    /// <summary>
    /// Passing the configuration builder to a helper makes its final mutation set unknown, even
    /// when one direct Performs call is otherwise parseable.
    /// </summary>
    [Test]
    public async Task Extract_ConfigurationBuilderEscape_InvalidatesDirectActionReference()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Then<Middle>(step =>
            {
                step.Performs(new WorkflowActionReference("orders", "Order", "process"));
                ConfigurationHelpers.Configure(step);
            })
            .Finally<Last>()
            """));

        await AssertDynamic(steps.Single(step => step.StepName == "Middle"));
    }

    /// <summary>An alias can mutate the same builder and is therefore outside the closed grammar.</summary>
    [Test]
    public async Task Extract_ConfigurationBuilderAlias_InvalidatesDirectActionReference()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Then<Middle>(step =>
            {
                step.Performs(new WorkflowActionReference("orders", "Order", "process"));
                var alias = step;
                alias.WithRetry(2);
            })
            .Finally<Last>()
            """));

        await AssertDynamic(steps.Single(step => step.StepName == "Middle"));
    }

    /// <summary>A conditional mutation is not an exactly-once occurrence declaration.</summary>
    [Test]
    public async Task Extract_ConditionalPerforms_InvalidatesActionReference()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Then<Middle>(step =>
            {
                if (DateTime.UtcNow.Ticks > 0)
                {
                    step.Performs(new WorkflowActionReference("orders", "Order", "process"));
                }
            })
            .Finally<Last>()
            """));

        await AssertDynamic(steps.Single(step => step.StepName == "Middle"));
    }

    /// <summary>
    /// Loop, branch, and fork path walkers all route configured steps through the
    /// same action-aware model construction path.
    /// </summary>
    [Test]
    public async Task Extract_StructuralPaths_ResolveOccurrenceActions()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .RepeatUntil(
                state => state.Done,
                "Refinement",
                loop => loop.Then<LoopBody>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "refine"))),
                maxIterations: 2)
            .Branch(
                state => state.Kind,
                BranchCase<FlowState, FlowKind>.When(
                    FlowKind.Left,
                    path => path.Then<BranchLeft>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "branch-left")))),
                BranchCase<FlowState, FlowKind>.When(
                    FlowKind.Right,
                    path => path.Then<BranchRight>(step => step.Performs(
                        new WorkflowActionReference("orders", "Order", "branch-right")))))
            .Fork(
                path => path.Then<ForkLeft>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "fork-left"))),
                path => path.Then<ForkRight>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "fork-right"))))
            .Join<Middle>()
            .Finally<Last>()
            """));

        await AssertResolved(steps.Single(step => step.StepName == "LoopBody"), "refine");
        await AssertResolved(steps.Single(step => step.StepName == "BranchLeft"), "branch-left");
        await AssertResolved(steps.Single(step => step.StepName == "BranchRight"), "branch-right");
        await AssertResolved(steps.Single(step => step.StepName == "ForkLeft"), "fork-left");
        await AssertResolved(steps.Single(step => step.StepName == "ForkRight"), "fork-right");
    }

    /// <summary>
    /// A configured top-level fork join is parsed as a normal action-bearing
    /// occurrence, including ordinary configuration from the same callback.
    /// </summary>
    [Test]
    public async Task Extract_ConfiguredTopLevelJoin_ResolvesActionAndConfiguration()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Fork(
                path => path.Then<ForkLeft>(),
                path => path.Then<ForkRight>())
            .Join<Middle>(step => step
                .Performs(new WorkflowActionReference("orders", "Order", "merge"))
                .WithRetry(4))
            .Finally<Last>()
            """));

        var join = steps.Single(step => step.StepName == "Middle");
        await AssertResolved(join, "merge");
        await Assert.That(join.Retry!.MaxAttempts).IsEqualTo(4);
    }

    /// <summary>
    /// The dedicated loop-body join parser delegates to the common configured-step
    /// path, preserving action identity, timeout, and loop phase prefix.
    /// </summary>
    [Test]
    public async Task Extract_ConfiguredLoopJoin_ResolvesActionAndConfiguration()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .RepeatUntil(
                state => state.Done,
                "Refinement",
                loop => loop
                    .Fork(
                        path => path.Then<ForkLeft>(),
                        path => path.Then<ForkRight>())
                    .Join<Middle>(step => step
                        .Performs(new WorkflowActionReference("orders", "Order", "merge-loop"))
                        .WithTimeout(TimeSpan.FromSeconds(30))),
                maxIterations: 2)
            .Finally<Last>()
            """));

        var join = steps.Single(step => step.StepName == "Middle");
        await AssertResolved(join, "merge-loop");
        await Assert.That(join.Timeout!.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(join.LoopName).IsEqualTo("Refinement");
    }

    /// <summary>
    /// The failure-handler extractor uses the configured-step path rather than
    /// reconstructing a model that drops occurrence action identity.
    /// </summary>
    [Test]
    public async Task Extract_FailureHandler_ResolvesOccurrenceAction()
    {
        var source = Source("""
            .StartWith<First>()
            .OnFailure(path => path
                .Then<FailureStep>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "recover")))
                .Complete())
            .Finally<Last>()
            """);
        var (workflowClass, semanticModel) = ParserTestHelper.CompileWorkflowValidated(source);
        var context = FluentDslParseContext.Create(
            workflowClass,
            semanticModel,
            "action-reference",
            CancellationToken.None);

        var handlerStep = FailureHandlerExtractor.Extract(context).Single().Steps!.Single();
        await AssertResolved(handlerStep, "recover");
    }

    /// <summary>
    /// A nested low-confidence handler owns its action; that declaration neither
    /// leaks to the parent nor gets dropped from the handler's model.
    /// </summary>
    [Test]
    public async Task Extract_LowConfidenceHandler_IsolatesOccurrenceAction()
    {
        var steps = ParserTestHelper.ExtractStepModelsValidated(Source("""
            .StartWith<First>()
            .Then<Middle>(step => step
                .RequireConfidence(0.8)
                .OnLowConfidence(path => path.Then<HandlerStep>(handler => handler.Performs(
                    new WorkflowActionReference("orders", "Order", "human-review")))))
            .Finally<Last>()
            """));

        var parent = steps.Single(step => step.StepName == "Middle");
        await AssertMissing(parent);
        await AssertResolved(
            parent.Confidence!.OnLowConfidenceHandlerChain!.Steps.Single(),
            "human-review");
    }

    /// <summary>
    /// The generator-side twin enforces the same non-blank identity invariant as
    /// the runtime value object.
    /// </summary>
    /// <param name="invalidName">The invalid identity component.</param>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" \t")]
    public async Task Model_InvalidIdentityComponent_ThrowsArgumentException(string? invalidName)
    {
        await Assert.That(() => new WorkflowActionReferenceModel(invalidName!, "Order", "process"))
            .Throws<ArgumentException>();
        await Assert.That(() => new WorkflowActionReferenceModel("orders", invalidName!, "process"))
            .Throws<ArgumentException>();
        await Assert.That(() => new WorkflowActionReferenceModel("orders", "Order", invalidName!))
            .Throws<ArgumentException>();
    }

    private static async Task AssertResolved(StepModel step, string actionName)
    {
        await Assert.That(step.ActionResolution).IsEqualTo(WorkflowActionReferenceResolution.Resolved);
        await Assert.That(step.Action).IsNotNull();
        await Assert.That(step.Action!.DomainName).IsEqualTo("orders");
        await Assert.That(step.Action.ObjectTypeName).IsEqualTo("Order");
        await Assert.That(step.Action.ActionName).IsEqualTo(actionName);
    }

    private static bool IsStepConfigurationCallback(ParameterInfo parameter)
    {
        var parameterType = parameter.ParameterType;
        if (!parameterType.IsGenericType ||
            parameterType.GetGenericTypeDefinition() != typeof(Action<>))
        {
            return false;
        }

        var callbackType = parameterType.GetGenericArguments()[0];
        return callbackType.IsGenericType &&
            callbackType.GetGenericTypeDefinition() == typeof(IStepConfiguration<>);
    }

    private static string DescribeParameter(ParameterInfo parameter) =>
        IsStepConfigurationCallback(parameter)
            ? "configure"
            : parameter.ParameterType == typeof(string)
                ? "string"
                : parameter.ParameterType.Name;

    private static async Task AssertMissing(StepModel step)
    {
        await Assert.That(step.Action).IsNull();
        await Assert.That(step.ActionResolution).IsEqualTo(WorkflowActionReferenceResolution.Missing);
    }

    private static async Task AssertDynamic(StepModel step)
    {
        await Assert.That(step.Action).IsNull();
        await Assert.That(step.ActionResolution)
            .IsEqualTo(WorkflowActionReferenceResolution.DynamicOrInvalid);
    }

    private static string Source(string chain) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum FlowKind { Left, Right }

        public record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public bool Done { get; init; }
            public FlowKind Kind { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class First : TestStep { }
        public sealed class Middle : TestStep { }
        public sealed class Third : TestStep { }
        public sealed class Blank : TestStep { }
        public sealed class LoopBody : TestStep { }
        public sealed class BranchLeft : TestStep { }
        public sealed class BranchRight : TestStep { }
        public sealed class ForkLeft : TestStep { }
        public sealed class ForkRight : TestStep { }
        public sealed class FailureStep : TestStep { }
        public sealed class HandlerStep : TestStep { }
        public sealed class Last : TestStep { }

        public static class ActionFactory
        {
            public static WorkflowActionReference Create() =>
                new WorkflowActionReference("orders", "Order", "dynamic");
        }

        public static class ConfigurationHelpers
        {
            public static void Configure(IStepConfiguration<FlowState> step) =>
                step.WithRetry(2);
        }

        [Workflow("action-reference")]
        public static partial class ActionReferenceWorkflow
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("action-reference")
                {{chain}};
        }
        """;
}
