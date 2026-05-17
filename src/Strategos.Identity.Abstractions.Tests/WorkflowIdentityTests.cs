// -----------------------------------------------------------------------
// <copyright file="WorkflowIdentityTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Behavior tests for <see cref="WorkflowIdentity"/>.
/// </summary>
/// <remarks>
/// Anchors DR-2 (sealed record, ASCII-validated value) and DR-8 rows 3 and 6
/// (null/empty/non-ASCII inputs rejected at construction time).
/// </remarks>
[Property("Category", "Unit")]
public class WorkflowIdentityTests
{
    /// <summary>
    /// Verifies that the record stores the supplied value byte-for-byte.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_ConstructedWithValue_StoresValueExactly()
    {
        var identity = new WorkflowIdentity("spiffe://td/workflow/abc-123");

        await Assert.That(identity.Value).IsEqualTo("spiffe://td/workflow/abc-123");
    }

    /// <summary>
    /// A null value violates DR-2 boundary validation.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_NullValue_ThrowsArgumentNullException()
    {
        await Assert.That(() => new WorkflowIdentity(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>
    /// An empty value violates DR-2 boundary validation.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_EmptyValue_ThrowsArgumentException()
    {
        await Assert.That(() => new WorkflowIdentity(string.Empty)).Throws<ArgumentException>();
    }

    /// <summary>
    /// Whitespace-only is treated as empty per DR-2.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_WhitespaceValue_ThrowsArgumentException()
    {
        await Assert.That(() => new WorkflowIdentity("   ")).Throws<ArgumentException>();
    }

    /// <summary>
    /// Non-ASCII characters violate the header-safe ASCII subset enforced by DR-8 row 6.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_NonAsciiValue_ThrowsArgumentException()
    {
        // U+00E9 (é) is outside the 0x20..0x7E printable ASCII subset.
        await Assert.That(() => new WorkflowIdentity("workflow-é")).Throws<ArgumentException>();
    }

    /// <summary>
    /// Control characters are rejected so transports never see un-escapable header bytes.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_ControlCharacterValue_ThrowsArgumentException()
    {
        await Assert.That(() => new WorkflowIdentity("workflow\t1")).Throws<ArgumentException>();
    }

    /// <summary>
    /// INV-6: sealed-by-default invariant must hold via reflection.
    /// </summary>
    [Test]
    public async Task WorkflowIdentity_IsSealedRecord_ViaReflection()
    {
        var t = typeof(WorkflowIdentity);

        await Assert.That(t.IsSealed).IsTrue();
    }
}
