// =============================================================================
// <copyright file="FailureHandlerExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="FailureHandlerExtractor"/> class.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>OnFailure calls are detected and parsed</description></item>
///   <item><description>Failure handler steps are extracted correctly</description></item>
///   <item><description>Terminal vs non-terminal handlers are distinguished</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class FailureHandlerExtractorTests
{
    // =============================================================================
    // A. Basic Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns empty list when no OnFailure calls.
    /// </summary>
    [Test]
    public async Task Extract_NoOnFailure_ReturnsEmptyList()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = FailureHandlerExtractor.Extract(context);

        // Assert
        await Assert.That(result).HasCount().EqualTo(0);
    }

    /// <summary>
    /// Verifies that Extract finds workflow-scoped OnFailure.
    /// </summary>
    [Test]
    public async Task Extract_WithWorkflowOnFailure_ReturnsFailureHandler()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .OnFailure(f => f.Then<LogFailure>().Complete())
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogFailure : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = FailureHandlerExtractor.Extract(context);

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Scope).IsEqualTo(FailureHandlerScope.Workflow);
    }

    /// <summary>
    /// Verifies that Extract captures failure handler steps.
    /// </summary>
    [Test]
    public async Task Extract_WithMultipleSteps_CapturesAllSteps()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .OnFailure(f => f
            .Then<LogFailure>()
            .Then<NotifyAdmin>()
            .Complete())
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogFailure : IWorkflowStep<TestState> { }
public class NotifyAdmin : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = FailureHandlerExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].StepNames).HasCount().EqualTo(2);
        await Assert.That(result[0].StepNames[0]).IsEqualTo("LogFailure");
        await Assert.That(result[0].StepNames[1]).IsEqualTo("NotifyAdmin");
    }

    // =============================================================================
    // B. Terminal Flag Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract detects terminal handlers with Complete().
    /// </summary>
    [Test]
    public async Task Extract_WithComplete_SetsIsTerminalTrue()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .OnFailure(f => f.Then<LogFailure>().Complete())
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogFailure : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = FailureHandlerExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Extract detects non-terminal handlers without Complete().
    /// </summary>
    [Test]
    public async Task Extract_WithoutComplete_SetsIsTerminalFalse()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .OnFailure(f => f.Then<LogFailure>())
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogFailure : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = FailureHandlerExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].IsTerminal).IsFalse();
    }

    // =============================================================================
    // C. Handler ID Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract generates unique handler IDs.
    /// </summary>
    [Test]
    public async Task Extract_GeneratesHandlerId()
    {
        // Arrange
        var source = @"
using Strategos.Builders;
using Strategos.Definitions;

public class MyWorkflow
{
    public WorkflowDefinition<TestState> Definition = Workflow<TestState>
        .Create(""test"")
        .StartWith<ValidateStep>()
        .OnFailure(f => f.Then<LogFailure>().Complete())
        .Finally<CompleteStep>();
}

public class TestState : IWorkflowState { }
public class ValidateStep : IWorkflowStep<TestState> { }
public class CompleteStep : IWorkflowStep<TestState> { }
public class LogFailure : IWorkflowStep<TestState> { }
";
        var context = ParserTestHelper.CreateParseContext(source, "test");

        // Act
        var result = FailureHandlerExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].HandlerId).IsNotNull().And.IsNotEqualTo(string.Empty);
        await Assert.That(result[0].HandlerId).Contains("test"); // Contains workflow name
    }
}
