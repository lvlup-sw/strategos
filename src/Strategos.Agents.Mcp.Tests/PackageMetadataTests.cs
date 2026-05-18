// =============================================================================
// <copyright file="PackageMetadataTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

namespace Strategos.Agents.Mcp.Tests;

[Property("Category", "Unit")]
public sealed class PackageMetadataTests
{
    [Test]
    public async Task StrategosAgentsMcp_Package_DependsOnModelContextProtocolClient()
    {
        // The Strategos.Agents.Mcp assembly must reference ModelContextProtocol packages
        // (it's the whole point of this sub-package).
        var assembly = typeof(Strategos.Agents.Mcp.PackageMarker).Assembly;
        var referenced = assembly.GetReferencedAssemblies();
        var hasMcp = referenced.Any(r => r.Name?.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase) ?? false);
        await Assert.That(hasMcp).IsTrue();
    }

    [Test]
    public async Task StrategosAgents_CoreAssembly_DoesNotReferenceModelContextProtocol()
    {
        // The core Strategos.Agents package must stay free of MCP dependencies
        // (port/adapter separation).
        var coreAssembly = typeof(Strategos.Agents.Abstractions.IAgentStep).Assembly;
        var referenced = coreAssembly.GetReferencedAssemblies();
        foreach (var refName in referenced)
        {
            await Assert.That(refName.Name?.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase) ?? false).IsFalse();
        }
    }
}
