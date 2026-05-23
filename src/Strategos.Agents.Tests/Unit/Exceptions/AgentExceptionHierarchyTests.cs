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
            // Pick the simplest constructor deterministically (fewest parameters,
            // then ordinal signature) — reflection order is otherwise unspecified
            // and would make this test flaky as constructors evolve.
            var ctor = t.GetConstructors()
                .OrderBy(c => c.GetParameters().Length)
                .ThenBy(c => c.ToString(), StringComparer.Ordinal)
                .FirstOrDefault();
            await Assert.That(ctor).IsNotNull();

            object? instance;
            var parameters = ctor!.GetParameters();
            try
            {
                var args = parameters.Select(CreateValidArgument).ToArray();
                instance = ctor.Invoke(args);
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

    // Produces a valid, non-null argument for a constructor parameter so reflection-based
    // construction survives guard clauses (e.g. ThrowIfNegativeOrZero on int, ThrowIfNull
    // on collection parameters). Without this, synthesizing 0 / null would trip the very
    // guards the production constructors now enforce.
    private static object? CreateValidArgument(ParameterInfo p)
    {
        if (p.HasDefaultValue)
        {
            return p.DefaultValue;
        }

        var type = p.ParameterType;
        if (type == typeof(string))
        {
            return "test";
        }

        if (type.IsValueType)
        {
            // Integral guards reject the zero default, so hand back a positive sentinel.
            return type == typeof(int) ? 1 : Activator.CreateInstance(type);
        }

        if (type.IsInterface && type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>)
                || definition == typeof(IList<>) || definition == typeof(ICollection<>)
                || definition == typeof(IEnumerable<>))
            {
                return Array.CreateInstance(type.GetGenericArguments()[0], 0);
            }
        }

        return null;
    }
}
