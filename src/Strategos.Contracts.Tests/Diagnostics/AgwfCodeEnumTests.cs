// =============================================================================
// <copyright file="AgwfCodeEnumTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Text.Json;

namespace Strategos.Contracts.Tests.Diagnostics;

/// <summary>
/// T4 — the generated <c>AgwfCode</c> C# enum. Asserts it has exactly 30
/// members carrying <em>symbolic</em> names (the wire identity every consumer
/// binds against by name, never by ordinal; INV-5), each serializing to its <c>AGWF0xx</c> wire string
/// via the <c>[JsonStringEnumMemberName]</c> path.
/// </summary>
[Property("Category", "Diagnostics")]
public sealed class AgwfCodeEnumTests
{
    // name -> wire value (the ground-truth identity map, symbolic names + gaps).
    private static readonly (string Name, string Wire)[] Expected =
    [
        ("EmptyWorkflowName", "AGWF001"),
        ("NoStepsFound", "AGWF002"),
        ("DuplicateStepName", "AGWF003"),
        ("InvalidNamespace", "AGWF004"),
        ("MissingStartWith", "AGWF009"),
        ("MissingFinally", "AGWF010"),
        ("ForkWithoutJoin", "AGWF012"),
        ("LoopWithoutBody", "AGWF014"),
        ("InvalidPersistenceMode", "AGWF015"),
        ("EventSourcedRequiresState", "AGWF016"),
        ("CompensateNotAStep", "AGWF017"),
        ("ConfidenceThresholdOutOfRange", "AGWF018"),
        ("RequireConfidenceWithoutHandler", "AGWF019"),
        ("RetryMaxAttemptsBelowOne", "AGWF020"),
        ("NonPositiveTimeout", "AGWF021"),
        ("DeclaredButInert", "AGWF022"),
        ("MalformedWorkflowJson", "AGWF023"),
        ("UnsupportedSchemaVersion", "AGWF024"),
        ("UnresolvableStepMoniker", "AGWF025"),
        ("AmbiguousStepMoniker", "AGWF026"),
        ("ImportRejectedDelegateStep", "AGWF027"),
        ("ImportRejectedBranchPoint", "AGWF028"),
        ("ImportRejectedLoop", "AGWF029"),
        ("ImportRejectedValidationPredicate", "AGWF030"),
        ("ImportRejectedApprovalContext", "AGWF031"),
        ("ImportDanglingGateId", "AGWF032"),
        ("ImportReliabilityBearingGate", "AGWF033"),
        ("ImportForkTriggerWithoutEvidence", "AGWF034"),
        ("UnreachableTermination", "AGWF035"),
        ("PathEndTypeCollision", "AGWF036"),
    ];

    /// <summary>
    /// Reflects over the generated <c>AgwfCode</c> enum and asserts the symbolic
    /// member set and the wire round-trip (serialize → <c>AGWF0xx</c>, back).
    /// </summary>
    [Test]
    public async Task AgwfCodeEnum_AllMembers_RoundTripsWireValues()
    {
        var enumType = typeof(ContractsMarker).Assembly
            .GetTypes()
            .FirstOrDefault(t => t.IsEnum
                && t.Namespace == "Strategos.Contracts.Generated"
                && t.Name == "AgwfCode");

        await Assert.That(enumType).IsNotNull()
            .Because("the codegen must emit a Strategos.Contracts.Generated.AgwfCode enum.");

        var members = Enum.GetNames(enumType!);
        await Assert.That(members.Length).IsEqualTo(Expected.Length)
            .Because("AgwfCode must have exactly 30 members.");

        var options = Strategos.Contracts.ContractsJson.Options;
        foreach (var (name, wire) in Expected)
        {
            await Assert.That(members).Contains(name)
                .Because($"AgwfCode must carry the symbolic member '{name}' (INV-5: name-based contract).");

            var value = Enum.Parse(enumType!, name);

            // Serialize → wire string.
            var json = JsonSerializer.Serialize(value, enumType!, options);
            await Assert.That(json).IsEqualTo($"\"{wire}\"")
                .Because($"AgwfCode.{name} must serialize to \"{wire}\".");

            // Deserialize wire string → member.
            var back = JsonSerializer.Deserialize($"\"{wire}\"", enumType!, options);
            await Assert.That(back!.ToString()).IsEqualTo(name)
                .Because($"\"{wire}\" must deserialize back to AgwfCode.{name}.");
        }
    }
}
