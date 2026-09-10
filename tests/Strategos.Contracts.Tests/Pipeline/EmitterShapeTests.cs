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
/// T3 — the C# emitter decision gate. Asserts that every shipped generated record
/// satisfies INV-6 (<c>sealed record</c>; abstract polymorphic bases may be
/// <c>abstract record</c>) and INV-7 (<c>{ get; init; }</c> + collections as
/// <see cref="IReadOnlyList{T}"/>/<see cref="IReadOnlyDictionary{TKey,TValue}"/>).
/// This gates the emitter-path choice for every downstream family: if the chosen
/// emitter cannot hit this shape, this test stays red and the package does not
/// ship mutable contracts.
/// </summary>
[Property("Category", "Pipeline")]
public class EmitterShapeTests
{
    /// <summary>
    /// Reflects over <em>every</em> public concrete record in the
    /// <c>Strategos.Contracts.Generated</c> namespace (events, workflow-IR arms,
    /// invariant/check models — the full shipped contract surface) and asserts the
    /// INV-6/INV-7 shape on each: sealed (or abstract for discriminated-union
    /// bases), every settable property init-only (no <c>set;</c>), and every
    /// collection property exposed as <see cref="IReadOnlyList{T}"/> /
    /// <see cref="IReadOnlyDictionary{TKey,TValue}"/> rather than a mutable
    /// <c>List&lt;&gt;</c>/<c>[]</c>/<c>Dictionary&lt;,&gt;</c>.
    /// </summary>
    [Test]
    public async Task EveryGeneratedRecord_IsSealedOrAbstract_InitOnly_ReadOnlyCollections()
    {
        var records = typeof(ContractsMarker).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true }
                && t.Namespace == "Strategos.Contracts.Generated"
                && IsRecord(t))
            .OrderBy(t => t.Name)
            .ToArray();

        // Sanity: the generated namespace must actually be populated, otherwise the
        // gate would vacuously pass against an empty emitter run.
        await Assert.That(records.Length).IsGreaterThanOrEqualTo(40)
            .Because("the C# emitter must populate the Generated namespace with the full contract surface.");

        foreach (var type in records)
        {
            // INV-6: sealed, OR abstract (allowed only for discriminated-union bases).
            await Assert.That(type.IsSealed || type.IsAbstract).IsTrue()
                .Because($"{type.Name} must be sealed (INV-6), or abstract for a polymorphic base.");

            // An abstract record must declare a JsonPolymorphic discriminator — i.e. it
            // is genuinely a discriminated-union base, not just an unsealed leftover.
            if (type.IsAbstract)
            {
                var isPolymorphicBase = type.GetCustomAttributes()
                    .Any(a => a.GetType().Name == "JsonPolymorphicAttribute");
                await Assert.That(isPolymorphicBase).IsTrue()
                    .Because($"abstract record {type.Name} is only allowed as a [JsonPolymorphic] union base (INV-6).");
            }

            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            foreach (var prop in props)
            {
                // Skip the compiler-synthesised EqualityContract.
                if (prop.Name == "EqualityContract")
                {
                    continue;
                }

                // INV-7: every settable property is init-only — no mutable public setter.
                var setter = prop.SetMethod;
                if (setter is not null)
                {
                    var isInitOnly = setter.ReturnParameter
                        .GetRequiredCustomModifiers()
                        .Any(m => m == typeof(IsExternalInit));
                    await Assert.That(isInitOnly).IsTrue()
                        .Because($"{type.Name}.{prop.Name} must be init-only, not a mutable set (INV-7).");
                }

                // INV-7: collections exposed as IReadOnlyList<T>/IReadOnlyDictionary<,>,
                // never a mutable list/array/dictionary.
                if (IsCollectionProperty(prop.PropertyType))
                {
                    await Assert.That(IsReadOnlyCollection(prop.PropertyType)).IsTrue()
                        .Because($"{type.Name}.{prop.Name} ({prop.PropertyType.Name}) must be " +
                            "IReadOnlyList<T> or IReadOnlyDictionary<,> (INV-7).");
                }
            }
        }
    }

    private static bool IsRecord(Type t) =>
        t.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null
        || t.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic) is not null;

    private static bool IsCollectionProperty(Type t)
    {
        if (t == typeof(string))
        {
            return false;
        }

        return t.IsArray || (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType);
    }

    private static bool IsReadOnlyCollection(Type t)
    {
        if (!t.IsGenericType)
        {
            return false;
        }

        var def = t.GetGenericTypeDefinition();
        return def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyDictionary<,>);
    }
}
