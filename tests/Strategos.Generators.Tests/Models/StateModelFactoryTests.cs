// -----------------------------------------------------------------------
// <copyright file="StateModelFactoryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StateModel.Create"/> and <see cref="StatePropertyModel.Create"/> factory methods.
/// </summary>
public sealed class StateModelFactoryTests
{
    [Test]
    public async Task StatePropertyModel_Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var name = "Items";
        var typeName = "IReadOnlyList<string>";
        var kind = StatePropertyKind.Append;

        // Act
        var model = StatePropertyModel.Create(
            name: name,
            typeName: typeName,
            kind: kind);

        // Assert
        await Assert.That(model.Name).IsEqualTo(name);
        await Assert.That(model.TypeName).IsEqualTo(typeName);
        await Assert.That(model.Kind).IsEqualTo(kind);
    }

    [Test]
    public async Task StatePropertyModel_Create_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        string? name = null;
        var typeName = "string";

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            StatePropertyModel.Create(
                name: name!,
                typeName: typeName,
                kind: StatePropertyKind.Standard);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task StatePropertyModel_Create_WithInvalidName_ThrowsArgumentException()
    {
        // Arrange
        var name = "Invalid-Name"; // Hyphen not valid
        var typeName = "string";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StatePropertyModel.Create(
                name: name,
                typeName: typeName,
                kind: StatePropertyKind.Standard);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task StatePropertyModel_Create_WithEmptyTypeName_ThrowsArgumentException()
    {
        // Arrange
        var name = "Items";
        var typeName = "";

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StatePropertyModel.Create(
                name: name,
                typeName: typeName,
                kind: StatePropertyKind.Standard);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task StateModel_Create_WithValidParameters_ReturnsModel()
    {
        // Arrange
        var typeName = "OrderState";
        var @namespace = "MyCompany.Workflows";
        var properties = new List<StatePropertyModel>
        {
            StatePropertyModel.Create("Items", "IReadOnlyList<string>", StatePropertyKind.Append),
            StatePropertyModel.Create("TotalAmount", "decimal", StatePropertyKind.Standard)
        };

        // Act
        var model = StateModel.Create(
            typeName: typeName,
            @namespace: @namespace,
            properties: properties);

        // Assert
        await Assert.That(model.TypeName).IsEqualTo(typeName);
        await Assert.That(model.Namespace).IsEqualTo(@namespace);
        await Assert.That(model.Properties.Count).IsEqualTo(2);
        await Assert.That(model.ReducerClassName).IsEqualTo("OrderStateReducer");
    }

    [Test]
    public async Task StateModel_Create_WithNullTypeName_ThrowsArgumentNullException()
    {
        // Arrange
        string? typeName = null;
        var @namespace = "MyCompany.Workflows";
        var properties = new List<StatePropertyModel>();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            StateModel.Create(
                typeName: typeName!,
                @namespace: @namespace,
                properties: properties);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task StateModel_Create_WithInvalidTypeName_ThrowsArgumentException()
    {
        // Arrange
        var typeName = "123Invalid"; // Starts with number
        var @namespace = "MyCompany.Workflows";
        var properties = new List<StatePropertyModel>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StateModel.Create(
                typeName: typeName,
                @namespace: @namespace,
                properties: properties);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task StateModel_Create_WithEmptyNamespace_ThrowsArgumentException()
    {
        // Arrange
        var typeName = "OrderState";
        var @namespace = "";
        var properties = new List<StatePropertyModel>();

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            StateModel.Create(
                typeName: typeName,
                @namespace: @namespace,
                properties: properties);
        });

        // Assert
        await Assert.That(exception).IsNotNull();
    }
}
