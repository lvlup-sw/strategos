// -----------------------------------------------------------------------
// <copyright file="GeneratorTestHelperTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Diagnostics;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>Tests for the authoritative source-generator fixture harness.</summary>
public sealed class GeneratorTestHelperTests
{
    /// <summary>Generated C# compiler errors cannot be hidden behind an otherwise clean driver run.</summary>
    [Test]
    public async Task RunGeneratorWithValidInput_UncompilableGeneratedOutput_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GeneratorTestHelper.RunGeneratorWithValidInput<UncompilableOutputGenerator>(
                "public sealed class ValidInput { }"));

        await Assert.That(exception.Message).Contains("The generated output does not compile");
        await Assert.That(exception.Message).Contains("CS0246");
        await Assert.That(exception.Message).Contains("MissingGeneratedType");
    }

    /// <summary>Unexpected error diagnostics from the generator driver cannot be ignored.</summary>
    [Test]
    public async Task RunGeneratorWithValidInput_UnexpectedDriverError_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GeneratorTestHelper.RunGeneratorWithValidInput<ErrorReportingGenerator>(
                "public sealed class ValidInput { }"));

        await Assert.That(exception.Message).Contains("unexpected driver errors");
        await Assert.That(exception.Message).Contains(WorkflowDiagnostics.InvalidNamespace.Id);
    }

    /// <summary>Roslyn's warning-severity generator crash diagnostics cannot be ignored.</summary>
    [Test]
    public async Task RunGeneratorWithValidInput_GeneratorCrash_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GeneratorTestHelper.RunGeneratorWithValidInput<CrashingGenerator>(
                "public sealed class ValidInput { }"));

        await Assert.That(exception.Message).Contains("unexpected driver errors");
        await Assert.That(exception.Message).Contains("CS8784");
    }

    /// <summary>An allowed generator error cannot conceal uncompilable generated output.</summary>
    [Test]
    public async Task RunGeneratorWithValidInput_AllowedErrorAndUncompilableOutput_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GeneratorTestHelper.RunGeneratorWithValidInput<ErrorAndUncompilableOutputGenerator>(
                "public sealed class ValidInput { }",
                WorkflowDiagnostics.InvalidNamespace.Id));

        await Assert.That(exception.Message).Contains("The generated output does not compile");
        await Assert.That(exception.Message).Contains("MissingGeneratedType");
    }

    /// <summary>A test-only generator that deliberately emits an unresolved type.</summary>
    public sealed class UncompilableOutputGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static productionContext =>
                productionContext.AddSource(
                    "Broken.g.cs",
                    SourceText.From(
                        "public sealed class Broken { public MissingGeneratedType Value { get; } }",
                        Encoding.UTF8)));
        }
    }

    /// <summary>A test-only generator that deliberately reports an error diagnostic.</summary>
    public sealed class ErrorReportingGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, static (productionContext, _) =>
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.InvalidNamespace,
                    Location.None,
                    "fixture")));
        }
    }

    /// <summary>A test-only generator that deliberately fails during initialization.</summary>
    public sealed class CrashingGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            throw new InvalidOperationException("Deliberate generator initialization failure.");
        }
    }

    /// <summary>A test-only generator that reports an allowed error and emits invalid C#.</summary>
    public sealed class ErrorAndUncompilableOutputGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static productionContext =>
                productionContext.AddSource(
                    "BrokenAfterExpectedError.g.cs",
                    SourceText.From(
                        "public sealed class Broken { public MissingGeneratedType Value { get; } }",
                        Encoding.UTF8)));
            context.RegisterSourceOutput(context.CompilationProvider, static (productionContext, _) =>
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    WorkflowDiagnostics.InvalidNamespace,
                    Location.None,
                    "fixture")));
        }
    }
}
