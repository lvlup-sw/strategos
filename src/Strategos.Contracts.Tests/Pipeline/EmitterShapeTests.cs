// =============================================================================
// <copyright file="EmitterShapeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// T3 — the C# emitter decision gate. Asserts that a generated record satisfies
/// INV-6 (<c>sealed record</c>) and INV-7 (<c>{ get; init; }</c> + collections as
/// <see cref="IReadOnlyList{T}"/>). This gates the emitter-path choice for every
/// downstream family: if the chosen emitter cannot hit this shape, this test
/// stays red and the package does not ship mutable contracts.
/// </summary>
[Property("Category", "Pipeline")]
public class EmitterShapeTests
{
    /// <summary>
    /// Reflects over the generated <c>PipelineProbe</c> record and asserts it is a
    /// sealed record, every public instance property is init-only (no public
    /// setter), and every collection-typed property is exposed as
    /// <see cref="IReadOnlyList{T}"/> rather than a mutable list or array.
    /// </summary>
    [Test]
    public async Task GeneratedRecord_IsSealed_InitOnly_ReadOnlyCollections()
    {
        var type = typeof(ContractsMarker).Assembly
            .GetTypes()
            .FirstOrDefault(t => t.Name == "PipelineProbe");

        await Assert.That(type).IsNotNull()
            .Because("the C# emitter must produce a PipelineProbe record into the assembly.");

        // INV-6: sealed.
        await Assert.That(type!.IsSealed).IsTrue().Because($"{type.Name} must be sealed (INV-6).");

        // record => has the compiler-synthesised EqualityContract / <Clone> marker.
        var isRecord = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null
            || type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) is not null;
        await Assert.That(isRecord).IsTrue().Because($"{type.Name} must be a record (INV-6).");

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        await Assert.That(props.Length).IsGreaterThan(0);

        foreach (var prop in props)
        {
            // INV-7: init-only — no public setter; the setter (if any) must be init-only.
            var setter = prop.SetMethod;
            await Assert.That(setter).IsNotNull().Because($"{prop.Name} must have an init accessor.");
            await Assert.That(setter!.IsPublic).IsTrue();
            var isInitOnly = setter.ReturnParameter
                .GetRequiredCustomModifiers()
                .Any(m => m == typeof(IsExternalInit));
            await Assert.That(isInitOnly).IsTrue()
                .Because($"{prop.Name} must be init-only, not a mutable set (INV-7).");

            // INV-7: collections exposed as IReadOnlyList<T>, never a mutable list/array.
            if (IsCollectionProperty(prop.PropertyType))
            {
                await Assert.That(IsReadOnlyList(prop.PropertyType)).IsTrue()
                    .Because($"{prop.Name} ({prop.PropertyType.Name}) must be IReadOnlyList<T> (INV-7).");
            }
        }
    }

    private static bool IsCollectionProperty(Type t)
    {
        if (t == typeof(string))
        {
            return false;
        }

        return t.IsArray || (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType);
    }

    private static bool IsReadOnlyList(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
}
