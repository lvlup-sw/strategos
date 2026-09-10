// =============================================================================
// <copyright file="IToolSourceContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Unit.Abstractions;

/// <summary>
/// T-004 (DR-6): the generalized <see cref="IToolSource"/> port replaces the old
/// MCP-specific <c>IMcpToolSource</c>. It exposes a single
/// <c>GetToolsAsync(CancellationToken)</c> method and the
/// <c>Strategos.Agents</c> assembly carrying it must stay free of any
/// <c>ModelContextProtocol</c> dependency (INV-3 separation).
/// </summary>
[Property("Category", "Unit")]
public sealed class IToolSourceContractTests
{
    [Test]
    public async Task IToolSource_IsMcpFreePortWithSingleGetToolsAsync()
    {
        // Port shape: single method GetToolsAsync(CancellationToken) → Task<IReadOnlyList<AIFunction>>
        var portType = typeof(IToolSource);
        await Assert.That(portType.IsInterface).IsTrue();

        var methods = portType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        await Assert.That(methods.Length).IsEqualTo(1);

        var method = methods[0];
        await Assert.That(method.Name).IsEqualTo("GetToolsAsync");
        await Assert.That(method.ReturnType).IsEqualTo(typeof(Task<IReadOnlyList<AIFunction>>));

        var parameters = method.GetParameters();
        await Assert.That(parameters.Length).IsEqualTo(1);
        await Assert.That(parameters[0].ParameterType).IsEqualTo(typeof(CancellationToken));

        // Assembly-level invariant: Strategos.Agents must not reference ModelContextProtocol.*
        var agentsAssembly = portType.Assembly;
        var referenced = agentsAssembly.GetReferencedAssemblies();
        foreach (var refName in referenced)
        {
            await Assert.That(refName.Name?.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase) ?? false).IsFalse();
        }
    }

    [Test]
    public async Task IMcpToolSource_DoesNotExist()
    {
        // Clean break (DR-6): the old MCP-specific port must be gone — no [Obsolete] shim.
        var agentsAssembly = typeof(IToolSource).Assembly;
        var legacy = agentsAssembly.GetType("Strategos.Agents.Abstractions.IMcpToolSource", throwOnError: false);
        await Assert.That(legacy).IsNull();
    }
}
