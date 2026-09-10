// =============================================================================
// <copyright file="IBeliefStoreContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

using Strategos.Primitives;
using Strategos.Selection;

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Contract tests for <see cref="IBeliefStore"/> interface verifying that all async methods
/// return <see cref="ValueTask{TResult}"/> to eliminate allocations on synchronous paths.
/// </summary>
/// <remarks>
/// <para>
/// ValueTask is preferred over Task for belief store methods because:
/// <list type="bullet">
///   <item><description>Most belief lookups complete synchronously from in-memory cache</description></item>
///   <item><description>Task allocates a state machine on the heap even for synchronous completion</description></item>
///   <item><description>ValueTask avoids allocation when returning synchronously completed results</description></item>
/// </list>
/// </para>
/// </remarks>
[Property("Category", "Unit")]
public sealed class IBeliefStoreContractTests
{
    /// <summary>
    /// Verifies that GetBeliefAsync returns ValueTask to avoid allocation on cache hits.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_ReturnsValueTask()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(ValueTask<Result<AgentBelief>>));
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync returns ValueTask for synchronous updates.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_ReturnsValueTask()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.UpdateBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(ValueTask<Result<Unit>>));
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync returns ValueTask for indexed lookups.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_ReturnsValueTask()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForAgentAsync));

        // Assert
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(ValueTask<Result<IReadOnlyList<AgentBelief>>>));
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync returns ValueTask for indexed lookups.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_ReturnsValueTask()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForCategoryAsync));

        // Assert
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(ValueTask<Result<IReadOnlyList<AgentBelief>>>));
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync returns ValueTask for synchronous saves.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_ReturnsValueTask()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.SaveBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(ValueTask<Result<Unit>>));
    }

    /// <summary>
    /// Verifies that all IBeliefStore async methods return ValueTask variants.
    /// </summary>
    /// <remarks>
    /// This comprehensive test ensures any new async methods added to the interface
    /// also use ValueTask for consistency and performance.
    /// </remarks>
    [Test]
    public async Task AllAsyncMethods_ReturnValueTask()
    {
        // Arrange
        var asyncMethods = typeof(IBeliefStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        // Assert - all async methods should return ValueTask variants
        await Assert.That(asyncMethods.Count).IsGreaterThan(0);

        foreach (var method in asyncMethods)
        {
            var returnType = method.ReturnType;
            var isValueTask = returnType == typeof(ValueTask) ||
                             (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));

            await Assert.That(isValueTask)
                .IsTrue()
                .Because($"Method {method.Name} should return ValueTask but returns {returnType.Name}");
        }
    }

    // =============================================================================
    // B. ValueTask Synchronous Completion Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefAsync method signature includes the correct Result wrapper.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_ReturnsResultOfAgentBelief()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var returnType = method!.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));

        var innerType = returnType.GetGenericArguments()[0];
        await Assert.That(innerType.IsGenericType).IsTrue();
        await Assert.That(innerType.GetGenericTypeDefinition()).IsEqualTo(typeof(Result<>));
        await Assert.That(innerType.GetGenericArguments()[0]).IsEqualTo(typeof(AgentBelief));
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync method signature includes the correct Result wrapper.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_ReturnsResultOfUnit()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.SaveBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var returnType = method!.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));

        var innerType = returnType.GetGenericArguments()[0];
        await Assert.That(innerType.IsGenericType).IsTrue();
        await Assert.That(innerType.GetGenericTypeDefinition()).IsEqualTo(typeof(Result<>));
        await Assert.That(innerType.GetGenericArguments()[0]).IsEqualTo(typeof(Unit));
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync method signature includes the correct Result wrapper.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_ReturnsResultOfUnit()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.UpdateBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var returnType = method!.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));

        var innerType = returnType.GetGenericArguments()[0];
        await Assert.That(innerType.IsGenericType).IsTrue();
        await Assert.That(innerType.GetGenericTypeDefinition()).IsEqualTo(typeof(Result<>));
        await Assert.That(innerType.GetGenericArguments()[0]).IsEqualTo(typeof(Unit));
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync method signature includes the correct Result wrapper with IReadOnlyList.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_ReturnsResultOfIReadOnlyList()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForAgentAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var returnType = method!.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));

        var innerType = returnType.GetGenericArguments()[0];
        await Assert.That(innerType.IsGenericType).IsTrue();
        await Assert.That(innerType.GetGenericTypeDefinition()).IsEqualTo(typeof(Result<>));

        var resultInnerType = innerType.GetGenericArguments()[0];
        await Assert.That(resultInnerType.IsGenericType).IsTrue();
        await Assert.That(resultInnerType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(resultInnerType.GetGenericArguments()[0]).IsEqualTo(typeof(AgentBelief));
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync method signature includes the correct Result wrapper with IReadOnlyList.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_ReturnsResultOfIReadOnlyList()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForCategoryAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var returnType = method!.ReturnType;
        await Assert.That(returnType.IsGenericType).IsTrue();
        await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));

        var innerType = returnType.GetGenericArguments()[0];
        await Assert.That(innerType.IsGenericType).IsTrue();
        await Assert.That(innerType.GetGenericTypeDefinition()).IsEqualTo(typeof(Result<>));

        var resultInnerType = innerType.GetGenericArguments()[0];
        await Assert.That(resultInnerType.IsGenericType).IsTrue();
        await Assert.That(resultInnerType.GetGenericTypeDefinition()).IsEqualTo(typeof(IReadOnlyList<>));
        await Assert.That(resultInnerType.GetGenericArguments()[0]).IsEqualTo(typeof(AgentBelief));
    }

    // =============================================================================
    // C. Method Parameter Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that GetBeliefAsync has the correct parameters: agentId, taskCategory, and optional CancellationToken.
    /// </summary>
    [Test]
    public async Task GetBeliefAsync_HasCorrectParameters()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(3);

        await Assert.That(parameters[0].Name).IsEqualTo("agentId");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(string));

        await Assert.That(parameters[1].Name).IsEqualTo("taskCategory");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(string));

        await Assert.That(parameters[2].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[2].ParameterType).IsEqualTo(typeof(CancellationToken));
        await Assert.That(parameters[2].IsOptional).IsTrue();
    }

    /// <summary>
    /// Verifies that UpdateBeliefAsync has the correct parameters: agentId, taskCategory, success, and optional CancellationToken.
    /// </summary>
    [Test]
    public async Task UpdateBeliefAsync_HasCorrectParameters()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.UpdateBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(4);

        await Assert.That(parameters[0].Name).IsEqualTo("agentId");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(string));

        await Assert.That(parameters[1].Name).IsEqualTo("taskCategory");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(string));

        await Assert.That(parameters[2].Name).IsEqualTo("success");
        await Assert.That(parameters[2].ParameterType).IsEqualTo(typeof(bool));

        await Assert.That(parameters[3].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[3].ParameterType).IsEqualTo(typeof(CancellationToken));
        await Assert.That(parameters[3].IsOptional).IsTrue();
    }

    /// <summary>
    /// Verifies that SaveBeliefAsync has the correct parameters: belief and optional CancellationToken.
    /// </summary>
    [Test]
    public async Task SaveBeliefAsync_HasCorrectParameters()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.SaveBeliefAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);

        await Assert.That(parameters[0].Name).IsEqualTo("belief");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(AgentBelief));

        await Assert.That(parameters[1].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(CancellationToken));
        await Assert.That(parameters[1].IsOptional).IsTrue();
    }

    /// <summary>
    /// Verifies that GetBeliefsForAgentAsync has the correct parameters: agentId and optional CancellationToken.
    /// </summary>
    [Test]
    public async Task GetBeliefsForAgentAsync_HasCorrectParameters()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForAgentAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);

        await Assert.That(parameters[0].Name).IsEqualTo("agentId");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(string));

        await Assert.That(parameters[1].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(CancellationToken));
        await Assert.That(parameters[1].IsOptional).IsTrue();
    }

    /// <summary>
    /// Verifies that GetBeliefsForCategoryAsync has the correct parameters: taskCategory and optional CancellationToken.
    /// </summary>
    [Test]
    public async Task GetBeliefsForCategoryAsync_HasCorrectParameters()
    {
        // Arrange
        var method = typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForCategoryAsync));

        // Assert
        await Assert.That(method).IsNotNull();

        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);

        await Assert.That(parameters[0].Name).IsEqualTo("taskCategory");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(string));

        await Assert.That(parameters[1].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(CancellationToken));
        await Assert.That(parameters[1].IsOptional).IsTrue();
    }

    // =============================================================================
    // D. Interface Method Count Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IBeliefStore has exactly 5 async methods as expected.
    /// </summary>
    [Test]
    public async Task IBeliefStore_HasExpectedMethodCount()
    {
        // Arrange
        var asyncMethods = typeof(IBeliefStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        // Assert
        await Assert.That(asyncMethods.Count).IsEqualTo(5);
    }

    /// <summary>
    /// Verifies that IBeliefStore contains all expected methods by name.
    /// </summary>
    [Test]
    public async Task IBeliefStore_ContainsAllExpectedMethods()
    {
        // Arrange
        var expectedMethods = new[]
        {
            nameof(IBeliefStore.GetBeliefAsync),
            nameof(IBeliefStore.SaveBeliefAsync),
            nameof(IBeliefStore.UpdateBeliefAsync),
            nameof(IBeliefStore.GetBeliefsForAgentAsync),
            nameof(IBeliefStore.GetBeliefsForCategoryAsync),
        };

        var interfaceMethods = typeof(IBeliefStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        // Assert
        foreach (var expectedMethod in expectedMethods)
        {
            await Assert.That(interfaceMethods.Contains(expectedMethod))
                .IsTrue()
                .Because($"Interface should contain method {expectedMethod}");
        }
    }

    // =============================================================================
    // E. CancellationToken Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that all async methods have CancellationToken as the last parameter.
    /// </summary>
    [Test]
    public async Task AllAsyncMethods_HaveCancellationTokenAsLastParameter()
    {
        // Arrange
        var asyncMethods = typeof(IBeliefStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        // Assert
        foreach (var method in asyncMethods)
        {
            var parameters = method.GetParameters();
            var lastParam = parameters[^1];

            await Assert.That(lastParam.ParameterType)
                .IsEqualTo(typeof(CancellationToken))
                .Because($"Method {method.Name} should have CancellationToken as last parameter");

            await Assert.That(lastParam.IsOptional)
                .IsTrue()
                .Because($"CancellationToken parameter in {method.Name} should be optional");
        }
    }

    /// <summary>
    /// Verifies that all CancellationToken parameters have default value.
    /// </summary>
    [Test]
    public async Task AllCancellationTokenParameters_HaveDefaultValue()
    {
        // Arrange
        var asyncMethods = typeof(IBeliefStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        // Assert
        foreach (var method in asyncMethods)
        {
            var cancellationParam = method.GetParameters()
                .First(p => p.ParameterType == typeof(CancellationToken));

            await Assert.That(cancellationParam.HasDefaultValue)
                .IsTrue()
                .Because($"CancellationToken in {method.Name} should have default value");

            // Note: Reflection returns null for default(CancellationToken) because it's a value type
            // The DefaultValue is DBNull.Value or null for value types with defaults
            var defaultValue = cancellationParam.DefaultValue;
            var isDefaultCancellationToken = defaultValue is null || defaultValue == DBNull.Value ||
                                              (defaultValue is CancellationToken ct && ct == default);
            await Assert.That(isDefaultCancellationToken)
                .IsTrue()
                .Because($"CancellationToken in {method.Name} should default to CancellationToken.None");
        }
    }

    // =============================================================================
    // F. Result Type Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that all async methods return Result-wrapped types for proper error handling.
    /// </summary>
    [Test]
    public async Task AllAsyncMethods_ReturnResultWrappedTypes()
    {
        // Arrange
        var asyncMethods = typeof(IBeliefStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        // Assert
        foreach (var method in asyncMethods)
        {
            var returnType = method.ReturnType;

            // Should be ValueTask<T>
            await Assert.That(returnType.IsGenericType).IsTrue();
            await Assert.That(returnType.GetGenericTypeDefinition()).IsEqualTo(typeof(ValueTask<>));

            // Inner type should be Result<T>
            var innerType = returnType.GetGenericArguments()[0];
            await Assert.That(innerType.IsGenericType)
                .IsTrue()
                .Because($"Method {method.Name} should return ValueTask<Result<T>>");
            await Assert.That(innerType.GetGenericTypeDefinition())
                .IsEqualTo(typeof(Result<>))
                .Because($"Method {method.Name} should return ValueTask<Result<T>>");
        }
    }

    /// <summary>
    /// Verifies that methods returning collections use IReadOnlyList for immutability.
    /// </summary>
    [Test]
    public async Task CollectionReturningMethods_UseIReadOnlyList()
    {
        // Arrange
        var collectionMethods = new[]
        {
            typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForAgentAsync)),
            typeof(IBeliefStore).GetMethod(nameof(IBeliefStore.GetBeliefsForCategoryAsync)),
        };

        // Assert
        foreach (var method in collectionMethods)
        {
            await Assert.That(method).IsNotNull();

            var returnType = method!.ReturnType;
            var resultType = returnType.GetGenericArguments()[0];
            var collectionType = resultType.GetGenericArguments()[0];

            await Assert.That(collectionType.IsGenericType).IsTrue();
            await Assert.That(collectionType.GetGenericTypeDefinition())
                .IsEqualTo(typeof(IReadOnlyList<>))
                .Because($"Method {method.Name} should use IReadOnlyList<> for immutability");
        }
    }
}
