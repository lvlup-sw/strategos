// -----------------------------------------------------------------------
// <copyright file="ISagaComponentEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Emitters.Saga;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters.Saga;

/// <summary>
/// Unit tests for the <see cref="ISagaComponentEmitter"/> interface.
/// </summary>
[Property("Category", "Unit")]
public class ISagaComponentEmitterTests
{
    /// <summary>
    /// Verifies that an implementation of ISagaComponentEmitter can be created.
    /// </summary>
    [Test]
    public async Task Interface_CanBeImplemented()
    {
        // Arrange
        var emitter = new TestEmitter();

        // Assert - Interface can be assigned
        ISagaComponentEmitter componentEmitter = emitter;
        await Assert.That(componentEmitter).IsNotNull();
    }

    /// <summary>
    /// Verifies that Emit method can be called on implementation.
    /// </summary>
    [Test]
    public async Task Emit_WithValidInputs_CanBeCalled()
    {
        // Arrange
        var emitter = new TestEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);

        // Assert
        await Assert.That(sb.ToString()).Contains("TestEmitter");
    }

    /// <summary>
    /// Verifies that Emit method produces output.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_AppendsToStringBuilder()
    {
        // Arrange
        var emitter = new TestEmitter();
        var sb = new StringBuilder();
        var model = CreateMinimalModel();

        // Act
        emitter.Emit(sb, model);

        // Assert
        await Assert.That(sb.Length).IsGreaterThan(0);
    }

    private static WorkflowModel CreateMinimalModel()
    {
        return new WorkflowModel(
            WorkflowName: "test-workflow",
            PascalName: "TestWorkflow",
            Namespace: "TestNamespace",
            StepNames: ["ValidateStep", "ProcessStep"],
            StateTypeName: "TestState");
    }

    /// <summary>
    /// Test implementation of ISagaComponentEmitter for testing purposes.
    /// </summary>
    private sealed class TestEmitter : ISagaComponentEmitter
    {
        public void Emit(StringBuilder sb, WorkflowModel model)
        {
            sb.AppendLine("// TestEmitter output");
        }
    }
}
