// -----------------------------------------------------------------------
// <copyright file="StateReducerGeneratorIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for the <see cref="StateReducerIncrementalGenerator"/>.
/// </summary>
[Property("Category", "Integration")]
public class StateReducerGeneratorIntegrationTests
{
    // =============================================================================
    // A. Attribute Detection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces output when a record has [WorkflowState] attribute.
    /// </summary>
    [Test]
    public async Task Generator_RecordWithWorkflowStateAttribute_ProducesOutput()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);

        // Assert
        await Assert.That(result.GeneratedTrees).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that the generator produces no output when no [WorkflowState] attribute is present.
    /// </summary>
    [Test]
    public async Task Generator_RecordWithoutAttribute_ProducesNothing()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithoutAttribute);

        // Assert
        await Assert.That(result.GeneratedTrees).IsEmpty();
    }

    /// <summary>
    /// Verifies that the generator works with structs as well as classes/records.
    /// </summary>
    [Test]
    public async Task Generator_StructWithWorkflowStateAttribute_ProducesOutput()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StructWithWorkflowState);

        // Assert
        await Assert.That(result.GeneratedTrees).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that no diagnostics (errors) are produced for valid input.
    /// </summary>
    [Test]
    public async Task Generator_ValidState_ProducesNoDiagnostics()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);

        // Assert - Should have no error-level diagnostics
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }

    // =============================================================================
    // B. Generated Reducer Structure Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generated file name matches the state type.
    /// </summary>
    [Test]
    public async Task Generator_ProducesCorrectFileName()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).IsNotEmpty();
    }

    /// <summary>
    /// Verifies that the generated reducer class is static.
    /// </summary>
    [Test]
    public async Task Generator_ProducesStaticReducerClass()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("public static partial class OrderStateReducer");
    }

    /// <summary>
    /// Verifies that the generated reducer uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Generator_ExtractsNamespace_FromDeclaration()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("namespace TestNamespace");
    }

    /// <summary>
    /// Verifies that nested namespaces are handled correctly.
    /// </summary>
    [Test]
    public async Task Generator_NestedNamespace_HandlesCorrectly()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithNestedNamespace);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "DomainStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("namespace Company.Product.Domain");
    }

    /// <summary>
    /// Verifies that the generated reducer contains a Reduce method.
    /// </summary>
    [Test]
    public async Task Generator_ProducesReduceMethod()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("public static OrderState Reduce(");
    }

    /// <summary>
    /// Verifies that the Reduce method has correct parameters.
    /// </summary>
    [Test]
    public async Task Generator_ReduceMethod_HasCorrectParameters()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("OrderState current");
        await Assert.That(generatedSource).Contains("OrderState update");
    }

    // =============================================================================
    // C. Property Kind Detection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that standard properties generate overwrite assignment.
    /// </summary>
    [Test]
    public async Task Generator_StandardProperty_GeneratesOverwriteAssignment()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("Status = update.Status");
        await Assert.That(generatedSource).Contains("Total = update.Total");
    }

    /// <summary>
    /// Verifies that [Append] properties generate Concat expression.
    /// </summary>
    [Test]
    public async Task Generator_AppendProperty_GeneratesConcatExpression()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithAppendProperty);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("current.Items.Concat(update.Items)");
        await Assert.That(generatedSource).Contains(".ToList()");
    }

    /// <summary>
    /// Verifies that [Merge] properties generate MergeDictionaries call.
    /// </summary>
    [Test]
    public async Task Generator_MergeProperty_GeneratesMergeDictionariesCall()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithMergeProperty);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("MergeDictionaries(current.Metadata, update.Metadata)");
    }

    /// <summary>
    /// Verifies that [Merge] properties generate the MergeDictionaries helper method.
    /// </summary>
    [Test]
    public async Task Generator_MergeProperty_GeneratesMergeDictionariesHelper()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithMergeProperty);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("private static ImmutableDictionary<TKey, TValue> MergeDictionaries<TKey, TValue>");
    }

    /// <summary>
    /// Verifies that mixed property kinds generate correct assignments.
    /// </summary>
    [Test]
    public async Task Generator_MixedPropertyKinds_GeneratesCorrectAssignments()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithMixedProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "ComplexStateReducer.g.cs");

        // Assert - Standard properties
        await Assert.That(generatedSource).Contains("Status = update.Status");
        await Assert.That(generatedSource).Contains("Count = update.Count");

        // Assert - Append properties
        await Assert.That(generatedSource).Contains("current.Items.Concat(update.Items)");
        await Assert.That(generatedSource).Contains("current.Scores.Concat(update.Scores)");

        // Assert - Merge properties
        await Assert.That(generatedSource).Contains("MergeDictionaries(current.Tags, update.Tags)");
    }

    // =============================================================================
    // D. Edge Case Tests
    // =============================================================================

    /// <summary>
    /// Verifies that empty state generates valid reducer with no property assignments.
    /// </summary>
    [Test]
    public async Task Generator_EmptyState_GeneratesValidReducer()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.EmptyState);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "EmptyStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("public static partial class EmptyStateReducer");
        await Assert.That(generatedSource).Contains("return current with");
    }

    /// <summary>
    /// Verifies that generated code includes auto-generated header.
    /// </summary>
    [Test]
    public async Task Generator_IncludesAutoGeneratedHeader()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("// <auto-generated/>");
    }

    /// <summary>
    /// Verifies that generated code includes nullable enable directive.
    /// </summary>
    [Test]
    public async Task Generator_IncludesNullableEnable()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that generated code includes GeneratedCode attribute.
    /// </summary>
    [Test]
    public async Task Generator_IncludesGeneratedCodeAttribute()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithStandardProperties);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "OrderStateReducer.g.cs");

        // Assert
        await Assert.That(generatedSource).Contains("[GeneratedCode(\"Strategos.Generators\"");
    }

    // =============================================================================
    // E. Performance Optimization Tests (A.10 - Metadata Caching)
    // =============================================================================

    /// <summary>
    /// Verifies that the generator correctly handles multiple collection properties.
    /// This test ensures the cached WellKnownTypes optimization preserves correct behavior
    /// when processing multiple [Append] and [Merge] properties.
    /// </summary>
    [Test]
    public async Task Generator_ManyCollectionProperties_GeneratesCorrectReducer()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithManyCollections);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "MultiCollectionStateReducer.g.cs");

        // Assert - Reducer class structure
        await Assert.That(generatedSource).Contains("public static partial class MultiCollectionStateReducer");

        // Assert - Standard property
        await Assert.That(generatedSource).Contains("Name = update.Name");

        // Assert - All [Append] properties generate Concat expressions
        await Assert.That(generatedSource).Contains("current.Items1.Concat(update.Items1)");
        await Assert.That(generatedSource).Contains("current.Items2.Concat(update.Items2)");
        await Assert.That(generatedSource).Contains("current.Items3.Concat(update.Items3)");

        // Assert - All [Merge] properties generate MergeDictionaries calls
        await Assert.That(generatedSource).Contains("MergeDictionaries(current.Dict1, update.Dict1)");
        await Assert.That(generatedSource).Contains("MergeDictionaries(current.Dict2, update.Dict2)");

        // Assert - No diagnostics (errors)
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }
}
