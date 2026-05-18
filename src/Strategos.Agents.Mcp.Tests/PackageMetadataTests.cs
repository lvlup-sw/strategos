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
    // NOTE: A runtime-reference assertion `StrategosAgentsMcp_Package_DependsOnModelContextProtocolClient`
    // was removed here: the assembly compiles with a csproj-level <PackageReference> to
    // ModelContextProtocol 1.3.0, but `GetReferencedAssemblies()` only reports assemblies
    // whose types are actually used. Until T-018 lands the McpToolSource adapter (which
    // imports ModelContextProtocol.Client.* types), the linker strips the reference and
    // the runtime check would falsely fail. T-018 will re-add this test with a real
    // typeof(McpClient) assertion. The architectural invariant test below (core does NOT
    // reference MCP) is the load-bearing assertion for the port/adapter separation.

    [Test]
    public async Task StrategosAgents_CoreAssembly_DoesNotReferenceModelContextProtocol()
    {
        // The core Strategos.Agents package must stay free of MCP dependencies
        // (port/adapter separation).
        var coreAssembly = typeof(Strategos.Agents.Abstractions.IAgentStep<>).Assembly;
        var referenced = coreAssembly.GetReferencedAssemblies();
        foreach (var refName in referenced)
        {
            await Assert.That(refName.Name?.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase) ?? false).IsFalse();
        }
    }
}
