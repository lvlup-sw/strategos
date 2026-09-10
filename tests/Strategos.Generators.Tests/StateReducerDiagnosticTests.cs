// -----------------------------------------------------------------------
// <copyright file="StateReducerDiagnosticTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Diagnostics;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Tests for state reducer generator diagnostics.
/// </summary>
[Property("Category", "Unit")]
public class StateReducerDiagnosticTests
{
    // =============================================================================
    // A. Diagnostic Descriptor Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AGSR001 diagnostic has correct ID.
    /// </summary>
    [Test]
    public async Task AppendOnNonCollectionDiagnostic_HasCorrectId()
    {
        // Assert
        await Assert.That(StateReducerDiagnostics.AppendOnNonCollection.Id).IsEqualTo("AGSR001");
    }

    /// <summary>
    /// Verifies that AGSR001 diagnostic has Error severity.
    /// </summary>
    [Test]
    public async Task AppendOnNonCollectionDiagnostic_IsError()
    {
        // Assert
        await Assert.That(StateReducerDiagnostics.AppendOnNonCollection.DefaultSeverity)
            .IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that AGSR002 diagnostic has correct ID.
    /// </summary>
    [Test]
    public async Task MergeOnNonDictionaryDiagnostic_HasCorrectId()
    {
        // Assert
        await Assert.That(StateReducerDiagnostics.MergeOnNonDictionary.Id).IsEqualTo("AGSR002");
    }

    /// <summary>
    /// Verifies that AGSR002 diagnostic has Error severity.
    /// </summary>
    [Test]
    public async Task MergeOnNonDictionaryDiagnostic_IsError()
    {
        // Assert
        await Assert.That(StateReducerDiagnostics.MergeOnNonDictionary.DefaultSeverity)
            .IsEqualTo(DiagnosticSeverity.Error);
    }

    // =============================================================================
    // B. Diagnostic Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that [Append] on a non-collection property produces AGSR001 diagnostic.
    /// </summary>
    [Test]
    public async Task Generator_AppendOnNonCollection_ProducesDiagnostic()
    {
        // Arrange
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [WorkflowState]
            public record BadState
            {
                [Append]
                public string NotACollection { get; init; } = "";
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(source);

        // Assert
        var agsr001 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGSR001");
        await Assert.That(agsr001).IsNotNull();
    }

    /// <summary>
    /// Verifies that [Merge] on a non-dictionary property produces AGSR002 diagnostic.
    /// </summary>
    [Test]
    public async Task Generator_MergeOnNonDictionary_ProducesDiagnostic()
    {
        // Arrange
        var source = """
            using System.Collections.Generic;
            using Strategos.Attributes;

            namespace TestNamespace;

            [WorkflowState]
            public record BadState
            {
                [Merge]
                public IReadOnlyList<string> NotADictionary { get; init; } = [];
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(source);

        // Assert
        var agsr002 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGSR002");
        await Assert.That(agsr002).IsNotNull();
    }

    /// <summary>
    /// Verifies that valid state produces no diagnostics.
    /// </summary>
    [Test]
    public async Task Generator_ValidState_ProducesNoDiagnostics()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(SourceTexts.StateWithMixedProperties);

        // Assert
        await Assert.That(result.Diagnostics).IsEmpty();
    }

    /// <summary>
    /// Verifies that diagnostic for [Append] on non-collection includes property name.
    /// </summary>
    [Test]
    public async Task Generator_AppendOnNonCollection_DiagnosticIncludesPropertyName()
    {
        // Arrange
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [WorkflowState]
            public record BadState
            {
                [Append]
                public int MyIntProperty { get; init; }
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(source);

        // Assert
        var agsr001 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGSR001");
        await Assert.That(agsr001).IsNotNull();
        await Assert.That(agsr001!.GetMessage()).Contains("MyIntProperty");
    }

    /// <summary>
    /// Verifies that diagnostic for [Merge] on non-dictionary includes property name.
    /// </summary>
    [Test]
    public async Task Generator_MergeOnNonDictionary_DiagnosticIncludesPropertyName()
    {
        // Arrange
        var source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [WorkflowState]
            public record BadState
            {
                [Merge]
                public string MyStringProperty { get; init; } = "";
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunStateReducerGenerator(source);

        // Assert
        var agsr002 = result.Diagnostics.FirstOrDefault(d => d.Id == "AGSR002");
        await Assert.That(agsr002).IsNotNull();
        await Assert.That(agsr002!.GetMessage()).Contains("MyStringProperty");
    }
}
