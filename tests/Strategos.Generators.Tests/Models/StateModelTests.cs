// -----------------------------------------------------------------------
// <copyright file="StateModelTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StateModel"/> and related types.
/// </summary>
[Property("Category", "Unit")]
public class StateModelTests
{
    // =============================================================================
    // A. StatePropertyKind Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StatePropertyKind has Standard value.
    /// </summary>
    [Test]
    public async Task StatePropertyKind_HasStandardValue()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(StatePropertyKind), StatePropertyKind.Standard)).IsTrue();
    }

    /// <summary>
    /// Verifies that StatePropertyKind has Append value.
    /// </summary>
    [Test]
    public async Task StatePropertyKind_HasAppendValue()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(StatePropertyKind), StatePropertyKind.Append)).IsTrue();
    }

    /// <summary>
    /// Verifies that StatePropertyKind has Merge value.
    /// </summary>
    [Test]
    public async Task StatePropertyKind_HasMergeValue()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(StatePropertyKind), StatePropertyKind.Merge)).IsTrue();
    }

    // =============================================================================
    // B. StatePropertyModel Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StatePropertyModel can be created with valid parameters.
    /// </summary>
    [Test]
    public async Task StatePropertyModel_Constructor_WithValidParameters_CreatesModel()
    {
        // Arrange & Act
        var model = new StatePropertyModel("Items", "IReadOnlyList<string>", StatePropertyKind.Append);

        // Assert
        await Assert.That(model.Name).IsEqualTo("Items");
        await Assert.That(model.TypeName).IsEqualTo("IReadOnlyList<string>");
        await Assert.That(model.Kind).IsEqualTo(StatePropertyKind.Append);
    }

    /// <summary>
    /// Verifies that StatePropertyModel is a record with value equality.
    /// </summary>
    [Test]
    public async Task StatePropertyModel_IsValueEqual()
    {
        // Arrange
        var model1 = new StatePropertyModel("Items", "IReadOnlyList<string>", StatePropertyKind.Append);
        var model2 = new StatePropertyModel("Items", "IReadOnlyList<string>", StatePropertyKind.Append);

        // Assert
        await Assert.That(model1).IsEqualTo(model2);
    }

    // =============================================================================
    // C. StateModel Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StateModel can be created with valid parameters.
    /// </summary>
    [Test]
    public async Task StateModel_Constructor_WithValidParameters_CreatesModel()
    {
        // Arrange
        var properties = new List<StatePropertyModel>
        {
            new("Status", "string", StatePropertyKind.Standard),
        };

        // Act
        var model = new StateModel("OrderState", "TestNamespace", properties);

        // Assert
        await Assert.That(model.TypeName).IsEqualTo("OrderState");
        await Assert.That(model.Namespace).IsEqualTo("TestNamespace");
        await Assert.That(model.Properties).HasCount().EqualTo(1);
    }

    /// <summary>
    /// Verifies that ReducerClassName returns TypeName with Reducer suffix.
    /// </summary>
    [Test]
    public async Task StateModel_ReducerClassName_ReturnsTypeNameWithReducerSuffix()
    {
        // Arrange
        var model = new StateModel("OrderState", "TestNamespace", []);

        // Assert
        await Assert.That(model.ReducerClassName).IsEqualTo("OrderStateReducer");
    }

    /// <summary>
    /// Verifies that Properties returns the provided property list.
    /// </summary>
    [Test]
    public async Task StateModel_Properties_ReturnsProvidedPropertyList()
    {
        // Arrange
        var properties = new List<StatePropertyModel>
        {
            new("Status", "string", StatePropertyKind.Standard),
            new("Items", "IReadOnlyList<string>", StatePropertyKind.Append),
            new("Metadata", "IReadOnlyDictionary<string, string>", StatePropertyKind.Merge),
        };

        // Act
        var model = new StateModel("OrderState", "TestNamespace", properties);

        // Assert
        await Assert.That(model.Properties).HasCount().EqualTo(3);
        await Assert.That(model.Properties[0].Name).IsEqualTo("Status");
        await Assert.That(model.Properties[1].Kind).IsEqualTo(StatePropertyKind.Append);
        await Assert.That(model.Properties[2].Kind).IsEqualTo(StatePropertyKind.Merge);
    }

    /// <summary>
    /// Verifies that StateModel with empty properties list is valid.
    /// </summary>
    [Test]
    public async Task StateModel_WithEmptyProperties_IsValid()
    {
        // Arrange & Act
        var model = new StateModel("EmptyState", "TestNamespace", []);

        // Assert
        await Assert.That(model.Properties).IsEmpty();
        await Assert.That(model.ReducerClassName).IsEqualTo("EmptyStateReducer");
    }

    /// <summary>
    /// Verifies that StateModel is a record with value equality.
    /// </summary>
    [Test]
    public async Task StateModel_IsValueEqual()
    {
        // Arrange
        var props = new List<StatePropertyModel>();
        var model1 = new StateModel("OrderState", "TestNamespace", props);
        var model2 = new StateModel("OrderState", "TestNamespace", props);

        // Assert
        await Assert.That(model1).IsEqualTo(model2);
    }
}
