// =============================================================================
// <copyright file="AgentToolAttributeTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

namespace Strategos.Agents.Tests.Unit;

public sealed class AgentToolAttributeTests
{
    [Test]
    public async Task AgentToolAttribute_TargetsMethods_WithOptionalNameOverride()
    {
        var attrType = typeof(AgentToolAttribute);

        await Assert.That(attrType.IsSealed).IsTrue();
        await Assert.That(typeof(Attribute).IsAssignableFrom(attrType)).IsTrue();

        var usage = attrType.GetCustomAttribute<AttributeUsageAttribute>();
        await Assert.That(usage).IsNotNull();
        await Assert.That(usage!.ValidOn).IsEqualTo(AttributeTargets.Method);
        await Assert.That(usage.AllowMultiple).IsFalse();
        await Assert.That(usage.Inherited).IsFalse();

        var nameProp = attrType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(nameProp).IsNotNull();
        await Assert.That(nameProp!.PropertyType).IsEqualTo(typeof(string));

        // Default (no override) leaves Name null; the init-only override round-trips.
        var defaulted = new AgentToolAttribute();
        await Assert.That(defaulted.Name).IsNull();

        var named = new AgentToolAttribute { Name = "summarize" };
        await Assert.That(named.Name).IsEqualTo("summarize");
    }
}
