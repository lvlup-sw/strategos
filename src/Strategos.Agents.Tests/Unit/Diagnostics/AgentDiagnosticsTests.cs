// =============================================================================
// <copyright file="AgentDiagnosticsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;

namespace Strategos.Agents.Tests.Unit.Diagnostics;

public class AgentDiagnosticsTests
{
    [Test]
    public async Task AgentDiagnostics_AllSixCodes_DeclaredAsConstStringMatchingPattern()
    {
        var diagType = typeof(Strategos.Agents.Diagnostics.AgentDiagnostics);
        await Assert.That(diagType.IsAbstract && diagType.IsSealed).IsTrue(); // public static
        var pattern = new System.Text.RegularExpressions.Regex("^AGAG\\d{3}$");
        foreach (var code in new[] { "AGAG001", "AGAG002", "AGAG003", "AGAG004", "AGAG005", "AGAG006" })
        {
            var field = diagType.GetField(code, BindingFlags.Public | BindingFlags.Static);
            await Assert.That(field).IsNotNull();
            await Assert.That(field!.IsLiteral && !field.IsInitOnly).IsTrue(); // const
            await Assert.That(field.FieldType).IsEqualTo(typeof(string));
            var value = (string)field.GetValue(null)!;
            await Assert.That(pattern.IsMatch(value)).IsTrue();
            await Assert.That(value).IsEqualTo(code); // value == name
        }

        // No additional AGAG* fields beyond the six.
        var allAgag = diagType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.Name.StartsWith("AGAG")).Select(f => f.Name).ToArray();
        await Assert.That(allAgag.Length).IsEqualTo(6);
    }
}
