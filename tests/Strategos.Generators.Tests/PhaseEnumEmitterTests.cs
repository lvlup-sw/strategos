// -----------------------------------------------------------------------
// <copyright file="PhaseEnumEmitterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Unit tests for the Phase enum emitter functionality.
/// </summary>
[Property("Category", "Unit")]
public class PhaseEnumEmitterTests
{
    // =============================================================================
    // A. Enum Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generated enum has the correct name (PascalCase from kebab-case).
    /// </summary>
    [Test]
    public async Task Emit_GeneratesEnumWithCorrectName()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - "process-order" should become "ProcessOrderPhase"
        await Assert.That(generatedSource).Contains("public enum ProcessOrderPhase");
    }

    /// <summary>
    /// Verifies that the generated code uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_UsesCorrectNamespace()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("namespace TestNamespace;");
    }

    /// <summary>
    /// Verifies that the generated phase enum carries the centrally-stamped
    /// <c>[GeneratedCode("LevelUp.Strategos", ...)]</c> marker. An enum does NOT receive
    /// <c>[ExcludeFromCodeCoverage]</c> — the compiler rejects that attribute on enums (CS0592),
    /// and an enum has no executable code to cover.
    /// </summary>
    [Test]
    public async Task Emit_IncludesGeneratedCodeMarker()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("GeneratedCode(\"LevelUp.Strategos\"");
        await Assert.That(generatedSource).DoesNotContain("ExcludeFromCodeCoverage");
    }

    /// <summary>
    /// Verifies that the generated enum includes the JsonConverter attribute.
    /// </summary>
    [Test]
    public async Task Emit_IncludesJsonConverterAttribute()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("[JsonConverter(typeof(JsonStringEnumConverter))]");
    }

    /// <summary>
    /// Verifies that kebab-case workflow names are converted to PascalCase.
    /// </summary>
    [Test]
    [Arguments("simple", "SimplePhase")]
    [Arguments("process-order", "ProcessOrderPhase")]
    [Arguments("multi-word-workflow", "MultiWordWorkflowPhase")]
    public async Task Emit_ConvertsPascalCase_FromKebabCase(string workflowName, string expectedEnumName)
    {
        // Arrange
        var source = $$"""
            using Strategos.Attributes;

            namespace TestNamespace;

            [Workflow("{{workflowName}}")]
            public static partial class TestWorkflow
            {
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains($"public enum {expectedEnumName}");
    }

    // =============================================================================
    // B. Standard Phase Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generated enum always includes NotStarted phase.
    /// </summary>
    [Test]
    public async Task Emit_AlwaysIncludesNotStarted()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("NotStarted");
    }

    /// <summary>
    /// Verifies that the generated enum always includes Completed phase.
    /// </summary>
    [Test]
    public async Task Emit_AlwaysIncludesCompleted()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("Completed");
    }

    /// <summary>
    /// Verifies that the generated enum always includes Failed phase.
    /// </summary>
    [Test]
    public async Task Emit_AlwaysIncludesFailed()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("Failed");
    }

    // =============================================================================
    // C. Auto-generated Header Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generated code includes the auto-generated header.
    /// </summary>
    [Test]
    public async Task Emit_IncludesAutoGeneratedHeader()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("// <auto-generated/>");
    }

    /// <summary>
    /// Verifies that the generated code enables nullable reference types.
    /// </summary>
    [Test]
    public async Task Emit_IncludesNullableEnable()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that XML documentation is generated for the enum.
    /// </summary>
    [Test]
    public async Task Emit_IncludesXmlDocumentation()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("/// <summary>");
        await Assert.That(generatedSource).Contains("Phase enumeration for the process-order workflow");
    }
}
