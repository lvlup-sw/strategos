// =============================================================================
// <copyright file="AssembledContextBuilderTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Agents.Tests.Models;

/// <summary>
/// Unit tests for the <see cref="AssembledContextBuilder"/> class.
/// </summary>
[Property("Category", "Unit")]
public class AssembledContextBuilderTests
{
    // =============================================================================
    // A. AddStateContext Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AddStateContext adds a StateContextSegment.
    /// </summary>
    [Test]
    public async Task AddStateContext_WithValue_AddsStateSegment()
    {
        // Arrange
        var builder = new AssembledContextBuilder();

        // Act
        builder.AddStateContext("userName", "John Doe");
        var context = builder.Build();

        // Assert
        await Assert.That(context.Segments).HasCount(1);
        await Assert.That(context.Segments[0]).IsTypeOf<StateContextSegment>();

        var segment = (StateContextSegment)context.Segments[0];
        await Assert.That(segment.Name).IsEqualTo("userName");
        await Assert.That(segment.Value).IsEqualTo("John Doe");
    }

    // =============================================================================
    // B. AddRetrievalContext Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AddRetrievalContext adds a RetrievalContextSegment.
    /// </summary>
    [Test]
    public async Task AddRetrievalContext_WithResults_AddsRetrievalSegment()
    {
        // Arrange
        var builder = new AssembledContextBuilder();
        var results = new List<RetrievalResult>
        {
            new("Document content 1", 0.95),
            new("Document content 2", 0.85),
        };

        // Act
        builder.AddRetrievalContext("knowledge-base", results);
        var context = builder.Build();

        // Assert
        await Assert.That(context.Segments).HasCount(1);
        await Assert.That(context.Segments[0]).IsTypeOf<RetrievalContextSegment>();

        var segment = (RetrievalContextSegment)context.Segments[0];
        await Assert.That(segment.CollectionName).IsEqualTo("knowledge-base");
        await Assert.That(segment.Results).HasCount(2);
    }

    // =============================================================================
    // C. AddLiteralContext Tests
    // =============================================================================

    /// <summary>
    /// Verifies that AddLiteralContext adds a LiteralContextSegment.
    /// </summary>
    [Test]
    public async Task AddLiteralContext_WithString_AddsLiteralSegment()
    {
        // Arrange
        var builder = new AssembledContextBuilder();
        var literal = "You are a helpful assistant.";

        // Act
        builder.AddLiteralContext(literal);
        var context = builder.Build();

        // Assert
        await Assert.That(context.Segments).HasCount(1);
        await Assert.That(context.Segments[0]).IsTypeOf<LiteralContextSegment>();

        var segment = (LiteralContextSegment)context.Segments[0];
        await Assert.That(segment.Value).IsEqualTo(literal);
    }

    // =============================================================================
    // D. Build Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Build with multiple sources returns context in order.
    /// </summary>
    [Test]
    public async Task Build_WithMultipleSources_ReturnsAssembledContextInOrder()
    {
        // Arrange
        var builder = new AssembledContextBuilder();
        var results = new List<RetrievalResult>
        {
            new("Retrieved doc", 0.9),
        };

        // Act
        builder
            .AddLiteralContext("First literal")
            .AddStateContext("field", "state value")
            .AddRetrievalContext("collection", results)
            .AddLiteralContext("Last literal");
        var context = builder.Build();

        // Assert
        await Assert.That(context.Segments).HasCount(4);
        await Assert.That(context.Segments[0]).IsTypeOf<LiteralContextSegment>();
        await Assert.That(context.Segments[1]).IsTypeOf<StateContextSegment>();
        await Assert.That(context.Segments[2]).IsTypeOf<RetrievalContextSegment>();
        await Assert.That(context.Segments[3]).IsTypeOf<LiteralContextSegment>();
    }

    /// <summary>
    /// Verifies that Build with no sources returns empty context.
    /// </summary>
    [Test]
    public async Task Build_WithNoSources_ReturnsEmptyContext()
    {
        // Arrange
        var builder = new AssembledContextBuilder();

        // Act
        var context = builder.Build();

        // Assert
        await Assert.That(context).IsEqualTo(AssembledContext.Empty);
        await Assert.That(context.IsEmpty).IsTrue();
    }

    // =============================================================================
    // E. Fluent API Tests
    // =============================================================================

    /// <summary>
    /// Verifies that builder methods return the builder for chaining.
    /// </summary>
    [Test]
    public async Task AllMethods_ReturnBuilder_ForChaining()
    {
        // Arrange
        var builder = new AssembledContextBuilder();

        // Act
        var result1 = builder.AddStateContext("name", "value");
        var result2 = builder.AddLiteralContext("literal");
        var result3 = builder.AddRetrievalContext("coll", []);

        // Assert
        await Assert.That(result1).IsEqualTo(builder);
        await Assert.That(result2).IsEqualTo(builder);
        await Assert.That(result3).IsEqualTo(builder);
    }

    /// <summary>
    /// Verifies that Build can be called multiple times.
    /// </summary>
    [Test]
    public async Task Build_CalledMultipleTimes_ReturnsSameContent()
    {
        // Arrange
        var builder = new AssembledContextBuilder();
        builder.AddLiteralContext("content");

        // Act
        var context1 = builder.Build();
        var context2 = builder.Build();

        // Assert
        await Assert.That(context1.Segments).HasCount(1);
        await Assert.That(context2.Segments).HasCount(1);
        await Assert.That(context1.ToPromptString()).IsEqualTo(context2.ToPromptString());
    }
}
