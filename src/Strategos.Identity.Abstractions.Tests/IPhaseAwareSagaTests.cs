// -----------------------------------------------------------------------
// <copyright file="IPhaseAwareSagaTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Verifies the public shape of <see cref="IPhaseAwareSaga"/>.
/// </summary>
/// <remarks>
/// Anchors DR-6 (interface portion). The basileus middleware reads
/// <c>saga.CurrentPhaseName</c> via this interface to derive per-step
/// agent identities, so the property must be a read-only string getter.
/// </remarks>
[Property("Category", "Unit")]
public class IPhaseAwareSagaTests
{
    /// <summary>
    /// Marker test: ensures the type exists and is a public interface.
    /// </summary>
    [Test]
    public async Task IPhaseAwareSaga_IsPublicInterface_ViaReflection()
    {
        var t = typeof(IPhaseAwareSaga);

        await Assert.That(t.IsInterface).IsTrue();
        await Assert.That(t.IsPublic).IsTrue();
    }

    /// <summary>
    /// The middleware contract requires a string CurrentPhaseName getter.
    /// </summary>
    [Test]
    public async Task IPhaseAwareSaga_HasCurrentPhaseNameProperty_AsStringGetter()
    {
        var prop = typeof(IPhaseAwareSaga).GetProperty("CurrentPhaseName", BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(prop.GetGetMethod()).IsNotNull();
    }

    /// <summary>
    /// INV-7 immutability: CurrentPhaseName must NOT expose a setter through this contract.
    /// </summary>
    [Test]
    public async Task IPhaseAwareSaga_CurrentPhaseName_HasNoSetter()
    {
        var prop = typeof(IPhaseAwareSaga).GetProperty("CurrentPhaseName", BindingFlags.Public | BindingFlags.Instance);

        await Assert.That(prop).IsNotNull();
        await Assert.That(prop!.GetSetMethod()).IsNull();
    }
}
