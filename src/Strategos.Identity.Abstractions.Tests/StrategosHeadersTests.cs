// -----------------------------------------------------------------------
// <copyright file="StrategosHeadersTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Reflection;

namespace Strategos.Identity.Abstractions.Tests;

/// <summary>
/// Verifies the <see cref="StrategosHeaders"/> constant values.
/// </summary>
/// <remarks>
/// Anchors DR-4. Constant values are part of the wire protocol — drift would
/// silently break basileus-side header propagation.
/// </remarks>
[Property("Category", "Unit")]
public class StrategosHeadersTests
{
    /// <summary>
    /// The workflow identity header constant is part of the wire protocol.
    /// </summary>
    [Test]
    public async Task StrategosHeaders_WorkflowIdentity_EqualsExpectedConstantValue()
    {
        await Assert.That(StrategosHeaders.WorkflowIdentity).IsEqualTo("x-strategos-workflow-identity");
    }

    /// <summary>
    /// The agent identity header constant is part of the wire protocol.
    /// </summary>
    [Test]
    public async Task StrategosHeaders_AgentIdentity_EqualsExpectedConstantValue()
    {
        await Assert.That(StrategosHeaders.AgentIdentity).IsEqualTo("x-strategos-agent-identity");
    }

    /// <summary>
    /// All public string constants must follow the <c>x-strategos-</c> prefix convention.
    /// </summary>
    [Test]
    public async Task StrategosHeaders_AllKeys_FollowXStrategosPrefix()
    {
        var fields = typeof(StrategosHeaders)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToList();

        await Assert.That(fields.Count).IsGreaterThan(0);

        foreach (var f in fields)
        {
            var v = (string?)f.GetRawConstantValue();
            await Assert.That(v).IsNotNull();
            await Assert.That(v!.StartsWith("x-strategos-", StringComparison.Ordinal)).IsTrue();
        }
    }

    /// <summary>
    /// All public string constants must be ASCII lower kebab-case so they survive
    /// case-insensitive HTTP header normalization and every transport's serializer.
    /// </summary>
    [Test]
    public async Task StrategosHeaders_AllKeys_AreAsciiLowerKebabCase()
    {
        var fields = typeof(StrategosHeaders)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .ToList();

        foreach (var f in fields)
        {
            var v = (string?)f.GetRawConstantValue();
            await Assert.That(v).IsNotNull();

            foreach (var c in v!)
            {
                var isLowerOrDigit = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                await Assert.That(isLowerOrDigit).IsTrue();
            }
        }
    }
}
