// =============================================================================
// <copyright file="IArtifactStoreContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections.Concurrent;
using System.Reflection;

namespace Strategos.Tests.Abstractions;

/// <summary>
/// Contract tests for <see cref="IArtifactStore"/> interface.
/// </summary>
/// <remarks>
/// These tests verify the interface contract using reflection to ensure
/// methods return ValueTask instead of Task for allocation-free synchronous paths.
/// </remarks>
[Property("Category", "Unit")]
public sealed class IArtifactStoreContractTests
{
    // =============================================================================
    // A. Interface Return Type Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StoreAsync returns ValueTask{Uri} instead of Task{Uri}.
    /// </summary>
    /// <remarks>
    /// ValueTask eliminates allocations on synchronous completion paths,
    /// which is important for high-throughput artifact store implementations.
    /// </remarks>
    [Test]
    public async Task StoreAsync_ReturnsValueTask()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var storeAsyncMethod = interfaceType.GetMethod("StoreAsync");

        // Assert
        await Assert.That(storeAsyncMethod).IsNotNull();
        await Assert.That(storeAsyncMethod!.ReturnType.IsGenericType).IsTrue();
        await Assert.That(storeAsyncMethod.ReturnType.GetGenericTypeDefinition())
            .IsEqualTo(typeof(ValueTask<>));
        await Assert.That(storeAsyncMethod.ReturnType.GetGenericArguments()[0])
            .IsEqualTo(typeof(Uri));
    }

    /// <summary>
    /// Verifies that RetrieveAsync returns ValueTask{T} instead of Task{T}.
    /// </summary>
    /// <remarks>
    /// ValueTask eliminates allocations on synchronous completion paths,
    /// especially beneficial for in-memory artifact stores.
    /// </remarks>
    [Test]
    public async Task RetrieveAsync_ReturnsValueTask()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var retrieveAsyncMethod = interfaceType.GetMethod("RetrieveAsync");

        // Assert
        await Assert.That(retrieveAsyncMethod).IsNotNull();
        await Assert.That(retrieveAsyncMethod!.ReturnType.IsGenericType).IsTrue();
        await Assert.That(retrieveAsyncMethod.ReturnType.GetGenericTypeDefinition())
            .IsEqualTo(typeof(ValueTask<>));
    }

    /// <summary>
    /// Verifies that DeleteAsync returns ValueTask instead of Task.
    /// </summary>
    /// <remarks>
    /// ValueTask eliminates allocations on synchronous completion paths,
    /// which is the common case for delete operations.
    /// </remarks>
    [Test]
    public async Task DeleteAsync_ReturnsValueTask()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var deleteAsyncMethod = interfaceType.GetMethod("DeleteAsync");

        // Assert
        await Assert.That(deleteAsyncMethod).IsNotNull();
        await Assert.That(deleteAsyncMethod!.ReturnType).IsEqualTo(typeof(ValueTask));
    }

    /// <summary>
    /// Verifies that all async methods on IArtifactStore use ValueTask for allocation optimization.
    /// </summary>
    /// <remarks>
    /// This comprehensive test ensures all current and future async methods
    /// follow the ValueTask pattern for optimal performance.
    /// </remarks>
    [Test]
    public async Task AllAsyncMethods_UseValueTaskReturnType()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var asyncMethods = interfaceType.GetMethods()
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        // Assert - should have exactly 3 async methods
        await Assert.That(asyncMethods.Count).IsEqualTo(3);

        foreach (var method in asyncMethods)
        {
            var returnType = method.ReturnType;

            // Method should return either ValueTask or ValueTask<T>
            var isValueTask = returnType == typeof(ValueTask);
            var isGenericValueTask = returnType.IsGenericType &&
                                     returnType.GetGenericTypeDefinition() == typeof(ValueTask<>);

            await Assert.That(isValueTask || isGenericValueTask)
                .IsTrue()
                .Because($"Method {method.Name} should return ValueTask or ValueTask<T>, not {returnType.Name}");
        }
    }

    // =============================================================================
    // B. Method Signature Contract Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StoreAsync has the required generic type constraint.
    /// </summary>
    /// <remarks>
    /// StoreAsync should have a generic type parameter T with a class constraint
    /// to ensure only reference types can be stored.
    /// </remarks>
    [Test]
    public async Task StoreAsync_HasClassConstraint()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var method = interfaceType.GetMethod("StoreAsync");

        // Assert
        await Assert.That(method).IsNotNull();
        var genericArguments = method!.GetGenericArguments();
        await Assert.That(genericArguments.Length).IsEqualTo(1);

        var typeParam = genericArguments[0];
        var constraints = typeParam.GetGenericParameterConstraints();

        // class constraint means the base type constraint is Object and ReferenceTypeConstraint is set
        var hasClassConstraint = (typeParam.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
        await Assert.That(hasClassConstraint).IsTrue()
            .Because("StoreAsync generic parameter should have class constraint");
    }

    /// <summary>
    /// Verifies that RetrieveAsync has the required generic type constraint.
    /// </summary>
    /// <remarks>
    /// RetrieveAsync should have a generic type parameter T with a class constraint
    /// to ensure type safety during deserialization.
    /// </remarks>
    [Test]
    public async Task RetrieveAsync_HasClassConstraint()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var method = interfaceType.GetMethod("RetrieveAsync");

        // Assert
        await Assert.That(method).IsNotNull();
        var genericArguments = method!.GetGenericArguments();
        await Assert.That(genericArguments.Length).IsEqualTo(1);

        var typeParam = genericArguments[0];
        var hasClassConstraint = (typeParam.GenericParameterAttributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
        await Assert.That(hasClassConstraint).IsTrue()
            .Because("RetrieveAsync generic parameter should have class constraint");
    }

    /// <summary>
    /// Verifies that StoreAsync has the correct parameter types and order.
    /// </summary>
    [Test]
    public async Task StoreAsync_HasCorrectParameters()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var method = interfaceType.GetMethod("StoreAsync");

        // Assert
        await Assert.That(method).IsNotNull();
        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(3);

        // First param: T artifact (generic)
        await Assert.That(parameters[0].Name).IsEqualTo("artifact");

        // Second param: string category
        await Assert.That(parameters[1].Name).IsEqualTo("category");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(string));

        // Third param: CancellationToken
        await Assert.That(parameters[2].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[2].ParameterType).IsEqualTo(typeof(CancellationToken));
    }

    /// <summary>
    /// Verifies that RetrieveAsync has the correct parameter types and order.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_HasCorrectParameters()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var method = interfaceType.GetMethod("RetrieveAsync");

        // Assert
        await Assert.That(method).IsNotNull();
        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);

        // First param: Uri reference
        await Assert.That(parameters[0].Name).IsEqualTo("reference");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(Uri));

        // Second param: CancellationToken
        await Assert.That(parameters[1].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(CancellationToken));
    }

    /// <summary>
    /// Verifies that DeleteAsync has the correct parameter types and order.
    /// </summary>
    [Test]
    public async Task DeleteAsync_HasCorrectParameters()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var method = interfaceType.GetMethod("DeleteAsync");

        // Assert
        await Assert.That(method).IsNotNull();
        var parameters = method!.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(2);

        // First param: Uri reference
        await Assert.That(parameters[0].Name).IsEqualTo("reference");
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(Uri));

        // Second param: CancellationToken
        await Assert.That(parameters[1].Name).IsEqualTo("cancellationToken");
        await Assert.That(parameters[1].ParameterType).IsEqualTo(typeof(CancellationToken));
    }

    /// <summary>
    /// Verifies that DeleteAsync returns non-generic ValueTask (void equivalent).
    /// </summary>
    /// <remarks>
    /// DeleteAsync should return ValueTask (not ValueTask{T}) since delete
    /// operations do not need to return a value on success.
    /// </remarks>
    [Test]
    public async Task DeleteAsync_ReturnsNonGenericValueTask()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var method = interfaceType.GetMethod("DeleteAsync");

        // Assert
        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(ValueTask));
        await Assert.That(method.ReturnType.IsGenericType).IsFalse()
            .Because("DeleteAsync should return non-generic ValueTask, not ValueTask<T>");
    }

    // =============================================================================
    // C. Interface Completeness Tests
    // =============================================================================

    /// <summary>
    /// Verifies the interface has exactly the expected methods (no more, no less).
    /// </summary>
    /// <remarks>
    /// This test guards against accidental interface pollution or removal of required methods.
    /// </remarks>
    [Test]
    public async Task Interface_HasExactlyThreeMethods()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);

        // Act
        var methods = interfaceType.GetMethods();

        // Assert
        await Assert.That(methods.Length).IsEqualTo(3)
            .Because("IArtifactStore should have exactly 3 methods: StoreAsync, RetrieveAsync, DeleteAsync");
    }

    /// <summary>
    /// Verifies that the interface contains all required method names.
    /// </summary>
    [Test]
    public async Task Interface_ContainsAllRequiredMethods()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);
        var expectedMethods = new[] { "StoreAsync", "RetrieveAsync", "DeleteAsync" };

        // Act
        var actualMethods = interfaceType.GetMethods().Select(m => m.Name).ToHashSet();

        // Assert
        foreach (var expectedMethod in expectedMethods)
        {
            await Assert.That(actualMethods.Contains(expectedMethod)).IsTrue()
                .Because($"Interface should contain method '{expectedMethod}'");
        }
    }

    /// <summary>
    /// Verifies the interface is in the correct namespace.
    /// </summary>
    [Test]
    public async Task Interface_IsInCorrectNamespace()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);

        // Assert
        await Assert.That(interfaceType.Namespace).IsEqualTo("Strategos.Abstractions");
    }

    /// <summary>
    /// Verifies the interface is public.
    /// </summary>
    [Test]
    public async Task Interface_IsPublic()
    {
        // Arrange
        var interfaceType = typeof(IArtifactStore);

        // Assert
        await Assert.That(interfaceType.IsPublic).IsTrue();
        await Assert.That(interfaceType.IsInterface).IsTrue();
    }

    // =============================================================================
    // D. Behavioral Contract Tests (using TestableArtifactStore)
    // =============================================================================

    /// <summary>
    /// Verifies that StoreAsync returns a valid URI with expected scheme.
    /// </summary>
    [Test]
    public async Task StoreAsync_ReturnsValidUri()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new SimpleTestArtifact { Name = "test" };

        // Act
        var uri = await store.StoreAsync(artifact, "test-category", CancellationToken.None);

        // Assert
        await Assert.That(uri).IsNotNull();
        await Assert.That(uri.IsAbsoluteUri).IsTrue();
    }

    /// <summary>
    /// Verifies that StoreAsync throws ArgumentNullException for null artifact.
    /// </summary>
    [Test]
    public async Task StoreAsync_WithNullArtifact_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new TestableArtifactStore();

        // Act & Assert
        await Assert.That(async () =>
            await store.StoreAsync<SimpleTestArtifact>(null!, "category", CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that StoreAsync throws ArgumentException for null/empty category.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task StoreAsync_WithInvalidCategory_ThrowsArgumentException(string? category)
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new SimpleTestArtifact { Name = "test" };

        // Act & Assert
        await Assert.That(async () =>
            await store.StoreAsync(artifact, category!, CancellationToken.None))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that RetrieveAsync returns stored artifact correctly.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithValidUri_ReturnsStoredArtifact()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var original = new SimpleTestArtifact { Name = "test-data", Value = 42 };
        var uri = await store.StoreAsync(original, "category", CancellationToken.None);

        // Act
        var retrieved = await store.RetrieveAsync<SimpleTestArtifact>(uri, CancellationToken.None);

        // Assert
        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved.Name).IsEqualTo("test-data");
        await Assert.That(retrieved.Value).IsEqualTo(42);
    }

    /// <summary>
    /// Verifies that RetrieveAsync throws ArgumentNullException for null reference.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithNullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new TestableArtifactStore();

        // Act & Assert
        await Assert.That(async () =>
            await store.RetrieveAsync<SimpleTestArtifact>(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that RetrieveAsync throws KeyNotFoundException for missing artifact.
    /// </summary>
    [Test]
    public async Task RetrieveAsync_WithNonExistentUri_ThrowsKeyNotFoundException()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var nonExistentUri = new Uri("test://artifacts/nonexistent/12345");

        // Act & Assert
        await Assert.That(async () =>
            await store.RetrieveAsync<SimpleTestArtifact>(nonExistentUri, CancellationToken.None))
            .Throws<KeyNotFoundException>();
    }

    /// <summary>
    /// Verifies that DeleteAsync throws ArgumentNullException for null reference.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithNullReference_ThrowsArgumentNullException()
    {
        // Arrange
        var store = new TestableArtifactStore();

        // Act & Assert
        await Assert.That(async () =>
            await store.DeleteAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that DeleteAsync is idempotent for non-existent artifacts.
    /// </summary>
    [Test]
    public async Task DeleteAsync_WithNonExistentUri_CompletesWithoutException()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var nonExistentUri = new Uri("test://artifacts/nonexistent/12345");

        // Act & Assert - should not throw
        await store.DeleteAsync(nonExistentUri, CancellationToken.None);
    }

    /// <summary>
    /// Verifies that deleted artifacts cannot be retrieved.
    /// </summary>
    [Test]
    public async Task DeleteAsync_RemovesArtifact_CannotRetrieve()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new SimpleTestArtifact { Name = "to-delete" };
        var uri = await store.StoreAsync(artifact, "category", CancellationToken.None);

        // Verify it exists
        var retrieved = await store.RetrieveAsync<SimpleTestArtifact>(uri, CancellationToken.None);
        await Assert.That(retrieved.Name).IsEqualTo("to-delete");

        // Act
        await store.DeleteAsync(uri, CancellationToken.None);

        // Assert
        await Assert.That(async () =>
            await store.RetrieveAsync<SimpleTestArtifact>(uri, CancellationToken.None))
            .Throws<KeyNotFoundException>();
    }

    // =============================================================================
    // E. Type Parameter Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StoreAsync/RetrieveAsync works with complex nested types.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_ComplexNestedType_Succeeds()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var complex = new ComplexTestArtifact
        {
            Id = Guid.NewGuid(),
            Metadata = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            },
            Children = new List<SimpleTestArtifact>
            {
                new() { Name = "child1", Value = 1 },
                new() { Name = "child2", Value = 2 }
            }
        };

        // Act
        var uri = await store.StoreAsync(complex, "complex", CancellationToken.None);
        var retrieved = await store.RetrieveAsync<ComplexTestArtifact>(uri, CancellationToken.None);

        // Assert
        await Assert.That(retrieved.Id).IsEqualTo(complex.Id);
        await Assert.That(retrieved.Metadata).IsNotNull();
        await Assert.That(retrieved.Metadata!.Count).IsEqualTo(2);
        await Assert.That(retrieved.Children).IsNotNull();
        await Assert.That(retrieved.Children!.Count).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that StoreAsync/RetrieveAsync works with string type.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_StringType_Succeeds()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = "This is a simple string artifact with special chars: <>&\"'";

        // Act
        var uri = await store.StoreAsync(artifact, "strings", CancellationToken.None);
        var retrieved = await store.RetrieveAsync<string>(uri, CancellationToken.None);

        // Assert
        await Assert.That(retrieved).IsEqualTo(artifact);
    }

    /// <summary>
    /// Verifies that StoreAsync/RetrieveAsync works with array type.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_ArrayType_Succeeds()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new[] { "one", "two", "three" };

        // Act
        var uri = await store.StoreAsync(artifact, "arrays", CancellationToken.None);
        var retrieved = await store.RetrieveAsync<string[]>(uri, CancellationToken.None);

        // Assert
        await Assert.That(retrieved.Length).IsEqualTo(3);
        await Assert.That(retrieved[0]).IsEqualTo("one");
        await Assert.That(retrieved[1]).IsEqualTo("two");
        await Assert.That(retrieved[2]).IsEqualTo("three");
    }

    /// <summary>
    /// Verifies that StoreAsync/RetrieveAsync works with List collection type.
    /// </summary>
    [Test]
    public async Task StoreAndRetrieve_ListType_Succeeds()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var uri = await store.StoreAsync(artifact, "lists", CancellationToken.None);
        var retrieved = await store.RetrieveAsync<List<int>>(uri, CancellationToken.None);

        // Assert
        await Assert.That(retrieved.Count).IsEqualTo(5);
        await Assert.That(retrieved).Contains(3);
    }

    // =============================================================================
    // F. Category Parameter Tests
    // =============================================================================

    /// <summary>
    /// Verifies that category is respected in URI structure.
    /// </summary>
    [Test]
    public async Task StoreAsync_CategoryIncludedInUri()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new SimpleTestArtifact { Name = "test" };

        // Act
        var uri = await store.StoreAsync(artifact, "my-category", CancellationToken.None);

        // Assert
        await Assert.That(uri.ToString()).Contains("my-category");
    }

    /// <summary>
    /// Verifies that different categories result in different URI paths.
    /// </summary>
    [Test]
    public async Task StoreAsync_DifferentCategories_DifferentUriPaths()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact1 = new SimpleTestArtifact { Name = "test1" };
        var artifact2 = new SimpleTestArtifact { Name = "test2" };

        // Act
        var uri1 = await store.StoreAsync(artifact1, "category-a", CancellationToken.None);
        var uri2 = await store.StoreAsync(artifact2, "category-b", CancellationToken.None);

        // Assert
        await Assert.That(uri1.ToString()).Contains("category-a");
        await Assert.That(uri2.ToString()).Contains("category-b");
        await Assert.That(uri1).IsNotEqualTo(uri2);
    }

    // =============================================================================
    // G. Multiple Artifacts Tests
    // =============================================================================

    /// <summary>
    /// Verifies that multiple artifacts can be stored and retrieved independently.
    /// </summary>
    [Test]
    public async Task MultipleArtifacts_StoredAndRetrievedIndependently()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifacts = Enumerable.Range(1, 10)
            .Select(i => new SimpleTestArtifact { Name = $"artifact-{i}", Value = i })
            .ToList();

        // Act - store all
        var uris = new List<Uri>();
        foreach (var artifact in artifacts)
        {
            var uri = await store.StoreAsync(artifact, "batch", CancellationToken.None);
            uris.Add(uri);
        }

        // Assert - retrieve all and verify
        for (int i = 0; i < 10; i++)
        {
            var retrieved = await store.RetrieveAsync<SimpleTestArtifact>(uris[i], CancellationToken.None);
            await Assert.That(retrieved.Name).IsEqualTo($"artifact-{i + 1}");
            await Assert.That(retrieved.Value).IsEqualTo(i + 1);
        }
    }

    /// <summary>
    /// Verifies that each stored artifact gets a unique URI.
    /// </summary>
    [Test]
    public async Task StoreAsync_EachArtifactGetsUniqueUri()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new SimpleTestArtifact { Name = "same-content" };

        // Act - store same artifact multiple times
        var uris = new HashSet<Uri>();
        for (int i = 0; i < 5; i++)
        {
            var uri = await store.StoreAsync(artifact, "unique-test", CancellationToken.None);
            uris.Add(uri);
        }

        // Assert - all URIs should be unique
        await Assert.That(uris.Count).IsEqualTo(5);
    }

    // =============================================================================
    // H. Concurrent Operations Tests
    // =============================================================================

    /// <summary>
    /// Verifies that concurrent store operations work correctly.
    /// </summary>
    [Test]
    public async Task ConcurrentStoreOperations_AllSucceed()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var tasks = new List<Task<Uri>>();

        // Act - launch concurrent stores
        for (int i = 0; i < 20; i++)
        {
            var artifact = new SimpleTestArtifact { Name = $"concurrent-{i}", Value = i };
            tasks.Add(store.StoreAsync(artifact, "concurrent", CancellationToken.None).AsTask());
        }

        var uris = await Task.WhenAll(tasks);

        // Assert - all should succeed with unique URIs
        var uniqueUris = new HashSet<Uri>(uris);
        await Assert.That(uniqueUris.Count).IsEqualTo(20);
    }

    /// <summary>
    /// Verifies that concurrent retrieve operations work correctly.
    /// </summary>
    [Test]
    public async Task ConcurrentRetrieveOperations_AllSucceed()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var artifact = new SimpleTestArtifact { Name = "shared", Value = 42 };
        var uri = await store.StoreAsync(artifact, "concurrent", CancellationToken.None);

        // Act - launch concurrent retrieves
        var tasks = new List<Task<SimpleTestArtifact>>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(store.RetrieveAsync<SimpleTestArtifact>(uri, CancellationToken.None).AsTask());
        }

        var results = await Task.WhenAll(tasks);

        // Assert - all should return the same artifact
        foreach (var result in results)
        {
            await Assert.That(result.Name).IsEqualTo("shared");
            await Assert.That(result.Value).IsEqualTo(42);
        }
    }

    /// <summary>
    /// Verifies that mixed concurrent operations (store, retrieve, delete) work correctly.
    /// </summary>
    [Test]
    public async Task MixedConcurrentOperations_CompleteWithoutDeadlock()
    {
        // Arrange
        var store = new TestableArtifactStore();
        var completedOperations = new ConcurrentBag<string>();

        // Pre-store artifacts in two disjoint sets to avoid race conditions
        // Set 1: URIs for retrieval only (3 artifacts)
        var retrieveOnlyUris = new List<Uri>();
        for (int i = 0; i < 3; i++)
        {
            var artifact = new SimpleTestArtifact { Name = $"retrieve-only-{i}" };
            var uri = await store.StoreAsync(artifact, "mixed", CancellationToken.None);
            retrieveOnlyUris.Add(uri);
        }

        // Set 2: URIs for deletion only (2 artifacts)
        var deleteOnlyUris = new List<Uri>();
        for (int i = 0; i < 2; i++)
        {
            var artifact = new SimpleTestArtifact { Name = $"delete-only-{i}" };
            var uri = await store.StoreAsync(artifact, "mixed", CancellationToken.None);
            deleteOnlyUris.Add(uri);
        }

        // Act - launch mixed operations
        var tasks = new List<Task>();

        // Store tasks (10 new artifacts)
        for (int i = 0; i < 10; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(async () =>
            {
                var artifact = new SimpleTestArtifact { Name = $"new-{idx}" };
                await store.StoreAsync(artifact, "mixed", CancellationToken.None);
                completedOperations.Add($"store-{idx}");
            }));
        }

        // Retrieve tasks (only from retrieve-only set)
        foreach (var uri in retrieveOnlyUris)
        {
            var localUri = uri;
            tasks.Add(Task.Run(async () =>
            {
                await store.RetrieveAsync<SimpleTestArtifact>(localUri, CancellationToken.None);
                completedOperations.Add($"retrieve-{localUri}");
            }));
        }

        // Delete tasks (only from delete-only set, no overlap with retrieve)
        foreach (var uri in deleteOnlyUris)
        {
            var localUri = uri;
            tasks.Add(Task.Run(async () =>
            {
                await store.DeleteAsync(localUri, CancellationToken.None);
                completedOperations.Add($"delete-{localUri}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - all operations completed (10 stores + 3 retrieves + 2 deletes = 15)
        await Assert.That(completedOperations.Count).IsGreaterThanOrEqualTo(15);
    }

    // =============================================================================
    // Test Fixtures
    // =============================================================================

    /// <summary>
    /// Simple test artifact for basic tests.
    /// </summary>
    private sealed class SimpleTestArtifact
    {
        public string Name { get; init; } = string.Empty;
        public int Value { get; init; }
    }

    /// <summary>
    /// Complex test artifact with nested collections.
    /// </summary>
    private sealed class ComplexTestArtifact
    {
        public Guid Id { get; init; }
        public Dictionary<string, string>? Metadata { get; init; }
        public List<SimpleTestArtifact>? Children { get; init; }
    }

    /// <summary>
    /// Testable in-memory implementation of IArtifactStore for contract verification.
    /// </summary>
    private sealed class TestableArtifactStore : IArtifactStore
    {
        private readonly ConcurrentDictionary<string, string> _artifacts = new();
        private long _counter;

        public ValueTask<Uri> StoreAsync<T>(T artifact, string category, CancellationToken cancellationToken)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(artifact, nameof(artifact));
            ArgumentException.ThrowIfNullOrWhiteSpace(category, nameof(category));

            var id = Interlocked.Increment(ref _counter);
            var key = $"{category}/{id}";
            var json = System.Text.Json.JsonSerializer.Serialize(artifact);

            _artifacts[key] = json;

            var uri = new Uri($"test://artifacts/{key}");
            return new ValueTask<Uri>(uri);
        }

        public ValueTask<T> RetrieveAsync<T>(Uri reference, CancellationToken cancellationToken)
            where T : class
        {
            ArgumentNullException.ThrowIfNull(reference, nameof(reference));

            var key = ExtractKeyFromUri(reference);

            if (!_artifacts.TryGetValue(key, out var json))
            {
                throw new KeyNotFoundException($"Artifact not found: {reference}");
            }

            var artifact = System.Text.Json.JsonSerializer.Deserialize<T>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize artifact: {reference}");

            return new ValueTask<T>(artifact);
        }

        public ValueTask DeleteAsync(Uri reference, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(reference, nameof(reference));

            var key = ExtractKeyFromUri(reference);
            _artifacts.TryRemove(key, out _);

            return ValueTask.CompletedTask;
        }

        private static string ExtractKeyFromUri(Uri uri)
        {
            var path = uri.AbsolutePath;
            if (path.StartsWith("/artifacts/", StringComparison.OrdinalIgnoreCase))
            {
                return path["/artifacts/".Length..];
            }

            return path.TrimStart('/');
        }
    }
}
