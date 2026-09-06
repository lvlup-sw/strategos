// =============================================================================
// <copyright file="ExternalEnumJsonContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Strategos.EnumJson.SourceGeneration.Tests;

/// <summary>
/// Proves public wire enums can be consumed by a source-generated serializer
/// context in an assembly that has no access to either package's internals.
/// </summary>
public sealed class ExternalEnumJsonContextTests
{
    [Test]
    public async Task ExactEnumConverters_AreUsableFromExternalSourceGeneratedContext()
    {
        var outcome = JsonSerializer.Serialize(
            global::Strategos.Contracts.Generated.CodingAttemptOutcome.TestsFailed,
            ExternalEnumJsonContext.Default.ContractsCodingAttemptOutcome);
        var diagnostic = JsonSerializer.Serialize(
            global::Strategos.Contracts.Generated.AgwfCode.DuplicateCompensationSeed,
            ExternalEnumJsonContext.Default.ContractsAgwfCode);
        var contractsOperator = JsonSerializer.Serialize(
            global::Strategos.Contracts.Generated.ActionComparisonOperatorV1.GreaterThanOrEqual,
            ExternalEnumJsonContext.Default.ContractsComparisonOperator);
        var mcpOperator = JsonSerializer.Serialize(
            global::Strategos.Ontology.MCP.ActionComparisonOperatorV1.NotEqual,
            ExternalEnumJsonContext.Default.McpComparisonOperator);
        var mcpScalar = JsonSerializer.Serialize(
            global::Strategos.Ontology.MCP.ActionPredicateScalarKindV1.Enum,
            ExternalEnumJsonContext.Default.McpScalarKind);
        var mcpStrength = JsonSerializer.Serialize(
            global::Strategos.Ontology.MCP.ActionRequirementStrengthV1.Soft,
            ExternalEnumJsonContext.Default.McpRequirementStrength);

        await Assert.That(outcome).IsEqualTo("\"tests_failed\"");
        await Assert.That(diagnostic).IsEqualTo("\"AGWF038\"");
        await Assert.That(contractsOperator).IsEqualTo("\"greater-than-or-equal\"");
        await Assert.That(mcpOperator).IsEqualTo("\"not-equal\"");
        await Assert.That(mcpScalar).IsEqualTo("\"enum\"");
        await Assert.That(mcpStrength).IsEqualTo("\"soft\"");
    }
}

/// <summary>Source-generated JSON metadata owned by the external consumer.</summary>
[JsonSerializable(
    typeof(global::Strategos.Contracts.Generated.CodingAttemptOutcome),
    TypeInfoPropertyName = "ContractsCodingAttemptOutcome")]
[JsonSerializable(
    typeof(global::Strategos.Contracts.Generated.AgwfCode),
    TypeInfoPropertyName = "ContractsAgwfCode")]
[JsonSerializable(
    typeof(global::Strategos.Contracts.Generated.ActionComparisonOperatorV1),
    TypeInfoPropertyName = "ContractsComparisonOperator")]
[JsonSerializable(
    typeof(global::Strategos.Ontology.MCP.ActionComparisonOperatorV1),
    TypeInfoPropertyName = "McpComparisonOperator")]
[JsonSerializable(
    typeof(global::Strategos.Ontology.MCP.ActionPredicateScalarKindV1),
    TypeInfoPropertyName = "McpScalarKind")]
[JsonSerializable(
    typeof(global::Strategos.Ontology.MCP.ActionRequirementStrengthV1),
    TypeInfoPropertyName = "McpRequirementStrength")]
internal partial class ExternalEnumJsonContext : JsonSerializerContext;
