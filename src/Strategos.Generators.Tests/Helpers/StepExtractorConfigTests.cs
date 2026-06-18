// -----------------------------------------------------------------------
// <copyright file="StepExtractorConfigTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Tests that the per-step resilience configuration declared via the new
/// <c>StartWith&lt;TStep&gt;(step =&gt; ...)</c> and <c>Finally&lt;TStep&gt;(step =&gt; ...)</c>
/// configure-lambda overloads (GitHub #141) reaches the generator's
/// <see cref="StepModel"/> IR for the FIRST (entry) and TERMINAL steps.
/// </summary>
/// <remarks>
/// <para>
/// Before #141 only <c>Then&lt;TStep&gt;(configure)</c> accepted a configure lambda, so
/// the entry step (<c>StartWith</c>) and the terminal step (<c>Finally</c>) could not
/// declare per-step retry/timeout/compensation/confidence — they had to be preceded /
/// followed by a non-configured <c>Then</c>. These tests pin that the entry and
/// terminal <see cref="StepModel"/> now carry the resilience config declared inline on
/// the <c>StartWith</c>/<c>Finally</c> calls.
/// </para>
/// <para>
/// The generator side already threads config uniformly: the shared
/// <c>StepExtractor.TryGetStepModel</c> path runs <c>ExtractConfiguredResilience</c> for
/// every DSL call (<c>StartWith</c>/<c>Then</c>/<c>Finally</c>), so once the C# overloads
/// exist the entry/terminal steps populate without any extractor change. These tests
/// drive a workflow snippet through <see cref="ParserTestHelper.ExtractStepModels"/>,
/// which routes through <c>FluentDslParser.ExtractStepModels</c> →
/// <see cref="StepExtractor.ExtractStepModels"/>.
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class StepExtractorConfigTests
{
    /// <summary>
    /// Verifies that <c>.WithRetry(2)</c> declared inline on the entry step via the new
    /// <c>StartWith&lt;First&gt;(step =&gt; step.WithRetry(2))</c> overload populates the
    /// FIRST step's <see cref="StepModel.Retry"/>.
    /// </summary>
    [Test]
    public async Task StepExtractor_StartWithConfigure_PopulatesFirstStepResilience()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(StartWithFinallyConfigWorkflow);

        // Act - the entry step declared via StartWith<First>(s => s.WithRetry(2))
        var firstStep = stepModels.Single(s => s.StepName == "First");

        // Assert - the configure lambda on StartWith reaches the entry step's IR
        await Assert.That(firstStep.Retry).IsNotNull();
        await Assert.That(firstStep.Retry!.MaxAttempts).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that <c>.WithTimeout(TimeSpan.FromSeconds(5))</c> declared inline on the
    /// terminal step via the new <c>Finally&lt;Last&gt;(step =&gt; step.WithTimeout(...))</c>
    /// overload populates the TERMINAL step's <see cref="StepModel.Timeout"/>.
    /// </summary>
    [Test]
    public async Task StepExtractor_FinallyConfigure_PopulatesTerminalStepResilience()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(StartWithFinallyConfigWorkflow);

        // Act - the terminal step declared via Finally<Last>(s => s.WithTimeout(...))
        var lastStep = stepModels.Single(s => s.StepName == "Last");

        // Assert - the configure lambda on Finally reaches the terminal step's IR
        await Assert.That(lastStep.Timeout).IsNotNull();
        await Assert.That(lastStep.Timeout!.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Verifies that the named-instance + configure overload
    /// (<c>StartWith&lt;First&gt;("Entry", step =&gt; step.WithRetry(4))</c>) carries BOTH the
    /// instance name AND the resilience config onto the entry step's
    /// <see cref="StepModel"/> (the string literal is read as the instance name; the
    /// lambda is read as the config).
    /// </summary>
    [Test]
    public async Task StepExtractor_StartWithNamedConfigure_CarriesInstanceNameAndResilience()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(StartWithNamedConfigWorkflow);

        // Act - the entry step declared via StartWith<First>("Entry", s => s.WithRetry(4))
        var firstStep = stepModels.Single(s => s.StepName == "First");

        // Assert - both the instance name and the resilience config reach the IR
        await Assert.That(firstStep.InstanceName).IsEqualTo("Entry");
        await Assert.That(firstStep.Retry).IsNotNull();
        await Assert.That(firstStep.Retry!.MaxAttempts).IsEqualTo(4);
    }

    // =========================================================================
    // Test source workflows
    // =========================================================================

    /// <summary>
    /// A linear workflow whose ENTRY step declares <c>.WithRetry(2)</c> via the new
    /// <c>StartWith</c> configure overload and whose TERMINAL step declares
    /// <c>.WithTimeout(TimeSpan.FromSeconds(5))</c> via the new <c>Finally</c> configure
    /// overload.
    /// </summary>
    private const string StartWithFinallyConfigWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class First : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public class Middle : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public class Last : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        [Workflow("startwith-finally-config")]
        public static partial class StartWithFinallyConfigWorkflow
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("startwith-finally-config")
                .StartWith<First>(step => step.WithRetry(2))
                .Then<Middle>()
                .Finally<Last>(step => step.WithTimeout(TimeSpan.FromSeconds(5)));
        }
        """;

    /// <summary>
    /// A linear workflow whose ENTRY step declares both an instance name and a retry
    /// policy via the named-instance + configure <c>StartWith</c> overload
    /// (<c>StartWith&lt;First&gt;("Entry", step =&gt; step.WithRetry(4))</c>).
    /// </summary>
    private const string StartWithNamedConfigWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class First : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public class Last : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        [Workflow("startwith-named-config")]
        public static partial class StartWithNamedConfigWorkflow
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("startwith-named-config")
                .StartWith<First>("Entry", step => step.WithRetry(4))
                .Finally<Last>();
        }
        """;
}
