// =============================================================================
// <copyright file="IMcpToolSourceContractTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Unit.Abstractions;

[Property("Category", "Unit")]
public sealed class IMcpToolSourceContractTests
{
    [Test]
    public async Task IMcpToolSource_PortShape_NoModelContextProtocolDependency()
    {
        // Port shape: single method GetToolsAsync(CancellationToken) → Task<IReadOnlyList<AIFunction>>
        var portType = typeof(IMcpToolSource);
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
}
