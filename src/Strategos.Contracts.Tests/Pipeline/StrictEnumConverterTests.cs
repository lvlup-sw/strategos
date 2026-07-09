// =============================================================================
// <copyright file="StrictEnumConverterTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Text.Json;
using Strategos.Contracts.Generated;

namespace Strategos.Contracts.Tests.Pipeline;

/// <summary>
/// DR-18 (consumer-safety half): the emitted closed-enum converters stay STRICT.
/// An unknown wire token must throw <see cref="JsonException"/> — there is NO
/// catch-all <c>unknown</c> member that would silently un-close the set. This is
/// what makes the schema-diff NOTICE policy safe: because an added enum member is
/// rejected by an un-upgraded consumer, consumers must upgrade before producers
/// emit the new member (the upgrade-ordering rule paired with the added-member
/// NOTICE — see <see cref="SchemaDiffTests"/>).
/// </summary>
[Property("Category", "Pipeline")]
public class StrictEnumConverterTests
{
    /// <summary>
    /// Deserializing an unknown wire token for a closed enum throws
    /// <see cref="JsonException"/> — the strict, closed-set posture. If this ever
    /// stops throwing, an unknown member is being silently swallowed and the closed
    /// set (and the NOTICE upgrade-ordering guarantee) is broken.
    /// </summary>
    [Test]
    public async Task EmittedEnumConverter_UnknownMember_ThrowsJsonException()
    {
        // "quantum_annealing" is not a member of CodingAttemptOutcome.
        var act = () => JsonSerializer.Deserialize<CodingAttemptOutcome>("\"quantum_annealing\"");

        await Assert.That(act).Throws<JsonException>()
            .Because("emitted closed-enum converters are strict — an unknown member must "
                + "throw, not bind to a catch-all `unknown`; this is the upgrade-ordering "
                + "guarantee behind the DR-18 added-member NOTICE policy.");
    }

    /// <summary>
    /// A known snake_case wire token binds to its PascalCase member — the strict
    /// converter accepts the closed set it is given (guards against the throw above
    /// being a false positive from an unrelated parse error).
    /// </summary>
    [Test]
    public async Task EmittedEnumConverter_KnownMember_BindsToWireToken()
    {
        var value = JsonSerializer.Deserialize<CodingAttemptOutcome>("\"tests_failed\"");

        await Assert.That(value).IsEqualTo(CodingAttemptOutcome.TestsFailed);
    }
}
