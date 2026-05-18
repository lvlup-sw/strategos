// =============================================================================
// <copyright file="AgentExceptionHierarchyTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using Strategos.Agents.Diagnostics;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents.Tests.Unit.Exceptions;

[Property("Category", "Unit")]
public sealed class AgentExceptionHierarchyTests
{
    [Test]
    public async Task AgentException_AbstractBase_HasDiagnosticProperty()
    {
        var baseType = typeof(AgentException);
        await Assert.That(baseType.IsAbstract).IsTrue();
        await Assert.That(baseType.IsClass).IsTrue();
        await Assert.That(typeof(Exception).IsAssignableFrom(baseType)).IsTrue();
        var diagProp = baseType.GetProperty("Diagnostic", BindingFlags.Public | BindingFlags.Instance);
        await Assert.That(diagProp).IsNotNull();
        await Assert.That(diagProp!.PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task AgentExceptionHierarchy_AllSubclasses_DeclareDiagnosticProperty()
    {
        var expected = new (Type type, string code)[]
        {
            (typeof(AgentBuilderValidationException), AgentDiagnostics.AGAG001),
            (typeof(AgentStructuredOutputException),  AgentDiagnostics.AGAG002),
            (typeof(AgentDuplicateToolException),     AgentDiagnostics.AGAG003),
            (typeof(AgentMcpException),               AgentDiagnostics.AGAG004),
            (typeof(AgentToolLoopException),          AgentDiagnostics.AGAG005),
            (typeof(AgentChatResponseException),      AgentDiagnostics.AGAG006),
        };

        foreach (var (t, code) in expected)
        {
            await Assert.That(t.IsSealed).IsTrue();
            await Assert.That(typeof(AgentException).IsAssignableFrom(t)).IsTrue();

            // Each subclass must have at least one constructor we can hit;
            // every constructed instance must report the correct Diagnostic.
            var ctor = t.GetConstructors().FirstOrDefault();
            await Assert.That(ctor).IsNotNull();

            // Try the simplest construction path each exception offers:
            object? instance = null;
            var parameters = ctor!.GetParameters();
            try
            {
                if (parameters.Length == 0)
                {
                    instance = ctor.Invoke(Array.Empty<object?>());
                }
                else
                {
                    var args = parameters.Select(p =>
                        p.HasDefaultValue
                            ? p.DefaultValue
                            : p.ParameterType == typeof(string)
                                ? "test"
                                : p.ParameterType.IsValueType
                                    ? Activator.CreateInstance(p.ParameterType)
                                    : null).ToArray();
                    instance = ctor.Invoke(args);
                }
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            await Assert.That(instance).IsNotNull();
            var diagProp = t.GetProperty("Diagnostic", BindingFlags.Public | BindingFlags.Instance)!;
            var diagValue = (string)diagProp.GetValue(instance)!;
            await Assert.That(diagValue).IsEqualTo(code);
        }
    }
}
