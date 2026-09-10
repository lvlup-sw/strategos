// -----------------------------------------------------------------------
// <copyright file="WireMonikerResolverTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Reflection;

using Strategos.Generators.Import;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// DR-13 (#100) — coverage for <see cref="WireMonikerResolver"/>, the wire simple-name →
/// CLR step-type resolver. Pins: the happy path (single accessible step type), the miss
/// diagnostic (AGWF025) naming the moniker + JSON file, the ambiguity diagnostic (AGWF026)
/// listing ALL candidates in deterministic order, the accessibility and step-contract filters,
/// INV-8 (the moniker is consumed as a string; no CLR <c>System.Type</c> is persisted into
/// contract state), and the CS0101-class non-masking guarantee (a shared moniker resolves to one
/// canonical type — the duplicate-definition collision is preserved, not disguised).
/// </summary>
[Property("Category", "WorkflowIr")]
public sealed class WireMonikerResolverTests
{
    private const string UnresolvableCode = "AGWF025";
    private const string AmbiguousCode = "AGWF026";
    private const string JsonPath = "orders.workflow.json";

    /// <summary>A state type plus a single step type whose simple name is the resolvable moniker.</summary>
    private const string SingleStepSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Steps;

        namespace Sample
        {
            public sealed class OrderState : IWorkflowState
            {
                public Guid WorkflowId { get; }
            }

            public sealed class IntakeStepFixture : IWorkflowStep<OrderState>
            {
                public Task<StepResult<OrderState>> ExecuteAsync(OrderState state, StepContext context, CancellationToken cancellationToken)
                    => throw new NotImplementedException();
            }

            // A plain type that does NOT implement the step contract — must never resolve.
            public sealed class NotAStepFixture
            {
            }
        }
        """;

    /// <summary>Two step types that share a simple name across distinct namespaces (ambiguous).</summary>
    private const string AmbiguousStepSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Steps;

        namespace Sample
        {
            public sealed class OrderState : IWorkflowState
            {
                public Guid WorkflowId { get; }
            }
        }

        namespace Zeta
        {
            public sealed class SharedStepFixture : Strategos.Abstractions.IWorkflowStep<Sample.OrderState>
            {
                public Task<Strategos.Steps.StepResult<Sample.OrderState>> ExecuteAsync(Sample.OrderState state, Strategos.Steps.StepContext context, CancellationToken cancellationToken)
                    => throw new NotImplementedException();
            }
        }

        namespace Alpha
        {
            public sealed class SharedStepFixture : Strategos.Abstractions.IWorkflowStep<Sample.OrderState>
            {
                public Task<Strategos.Steps.StepResult<Sample.OrderState>> ExecuteAsync(Sample.OrderState state, Strategos.Steps.StepContext context, CancellationToken cancellationToken)
                    => throw new NotImplementedException();
            }
        }
        """;

    /// <summary>The only same-named step type is a private-nested (inaccessible) class.</summary>
    private const string PrivateNestedStepSource = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Steps;

        namespace Sample
        {
            public sealed class OrderState : IWorkflowState
            {
                public Guid WorkflowId { get; }
            }

            public class Host
            {
                private sealed class HiddenStepFixture : IWorkflowStep<OrderState>
                {
                    public Task<StepResult<OrderState>> ExecuteAsync(OrderState state, StepContext context, CancellationToken cancellationToken)
                        => throw new NotImplementedException();
                }
            }
        }
        """;

    /// <summary>A moniker naming a single accessible step type resolves to exactly that symbol, no diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_SingleAccessibleStepType_ResolvesWithNoDiagnostic()
    {
        var compilation = BuildCompilation(SingleStepSource);
        await AssertNoCompileErrors(compilation);

        var result = WireMonikerResolver.Resolve(compilation, "IntakeStepFixture", JsonPath);

        await Assert.That(result.IsResolved).IsTrue()
            .Because("a moniker naming one accessible IWorkflowStep<TState> type must resolve.");
        await Assert.That(result.Outcome).IsEqualTo(WireMonikerOutcome.Resolved);
        await Assert.That(result.Symbol).IsNotNull();
        await Assert.That(result.Symbol!.Name).IsEqualTo("IntakeStepFixture")
            .Because("resolution binds the type whose simple name is the moniker.");
        await Assert.That(result.Diagnostic).IsNull()
            .Because("a clean resolution reports no diagnostic.");
    }

    /// <summary>A moniker with no matching step type reports the stable unresolvable diagnostic naming the moniker + file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_NoMatchingType_ReportsUnresolvableDiagnostic()
    {
        var compilation = BuildCompilation(SingleStepSource);

        var result = WireMonikerResolver.Resolve(compilation, "MissingStepFixture", JsonPath);

        await Assert.That(result.IsResolved).IsFalse();
        await Assert.That(result.Outcome).IsEqualTo(WireMonikerOutcome.Unresolvable);
        await Assert.That(result.Symbol).IsNull();
        await Assert.That(result.Diagnostic).IsNotNull();
        await Assert.That(result.Diagnostic!.Id).IsEqualTo(UnresolvableCode)
            .Because("an unresolvable moniker surfaces as the stable AGWF025 diagnostic.");

        var message = result.Diagnostic!.GetMessage();
        await Assert.That(message).Contains("MissingStepFixture")
            .Because("the diagnostic names the offending moniker.");
        await Assert.That(message).Contains(JsonPath)
            .Because("the diagnostic names the JSON import file path.");
    }

    /// <summary>A same-named type that does NOT implement the step contract is not a candidate — the moniker is unresolvable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_TypeNotImplementingStepContract_IsUnresolvable()
    {
        var compilation = BuildCompilation(SingleStepSource);

        var result = WireMonikerResolver.Resolve(compilation, "NotAStepFixture", JsonPath);

        await Assert.That(result.Outcome).IsEqualTo(WireMonikerOutcome.Unresolvable)
            .Because("the step-contract filter excludes same-named types that do not implement IWorkflowStep<TState>.");
        await Assert.That(result.Diagnostic!.Id).IsEqualTo(UnresolvableCode);
    }

    /// <summary>An accessible step type exists in name only behind a private nesting — it is filtered as inaccessible.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_OnlyCandidateInaccessible_IsUnresolvable()
    {
        var compilation = BuildCompilation(PrivateNestedStepSource);

        var result = WireMonikerResolver.Resolve(compilation, "HiddenStepFixture", JsonPath);

        await Assert.That(result.Outcome).IsEqualTo(WireMonikerOutcome.Unresolvable)
            .Because("a private-nested step type is not accessible from the compilation's assembly, so the moniker does not bind.");
        await Assert.That(result.Diagnostic!.Id).IsEqualTo(UnresolvableCode);
    }

    /// <summary>An ambiguous moniker reports the stable diagnostic listing ALL candidates in deterministic (ordinal) order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_TwoCandidatesSharingName_ReportsAmbiguousDiagnostic_DeterministicOrder()
    {
        var compilation = BuildCompilation(AmbiguousStepSource);
        await AssertNoCompileErrors(compilation);

        var result = WireMonikerResolver.Resolve(compilation, "SharedStepFixture", JsonPath);

        await Assert.That(result.Outcome).IsEqualTo(WireMonikerOutcome.Ambiguous)
            .Because("two accessible step types sharing the simple name make the moniker ambiguous.");
        await Assert.That(result.Symbol).IsNull();
        await Assert.That(result.Diagnostic!.Id).IsEqualTo(AmbiguousCode)
            .Because("an ambiguous moniker surfaces as the stable AGWF026 diagnostic.");

        var message = result.Diagnostic!.GetMessage();
        await Assert.That(message).Contains("Alpha.SharedStepFixture")
            .Because("the ambiguity diagnostic lists every candidate.");
        await Assert.That(message).Contains("Zeta.SharedStepFixture")
            .Because("the ambiguity diagnostic lists every candidate.");

        // Determinism: candidates are ordinal-sorted, so "Alpha.*" precedes "Zeta.*"
        // regardless of the source declaration order (Zeta is declared first above) or
        // the symbol-table enumeration order.
        var alphaIndex = message.IndexOf("Alpha.SharedStepFixture", StringComparison.Ordinal);
        var zetaIndex = message.IndexOf("Zeta.SharedStepFixture", StringComparison.Ordinal);
        await Assert.That(alphaIndex).IsLessThan(zetaIndex)
            .Because("candidates are listed in deterministic ordinal order (Alpha before Zeta).");

        // Stability: resolving again yields the identical message.
        var again = WireMonikerResolver.Resolve(compilation, "SharedStepFixture", JsonPath);
        await Assert.That(again.Diagnostic!.GetMessage()).IsEqualTo(message)
            .Because("the deterministic listing is stable across resolutions.");
    }

    /// <summary>
    /// INV-8: resolving CONSUMES the moniker string. The wire DTO's moniker field is unchanged, the
    /// resolution surface exposes a Roslyn symbol (never a CLR <see cref="Type"/>), and no wire-contract
    /// twin carries a CLR <see cref="Type"/> (or Roslyn symbol) property back into contract state.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_ConsumesMonikerString_NoClrTypePersistedIntoContractState()
    {
        var compilation = BuildCompilation(SingleStepSource);

        // Contract state carries the moniker as a plain string.
        var skill = new SkillStep { StepType = "IntakeStepFixture" };

        var result = WireMonikerResolver.Resolve(compilation, skill.StepType!, JsonPath);

        await Assert.That(result.IsResolved).IsTrue();

        // The moniker field is still the ORIGINAL string — nothing wrote a Type back onto the DTO.
        await Assert.That(skill.StepType).IsEqualTo("IntakeStepFixture")
            .Because("resolution consumes the moniker; it does not overwrite the wire DTO's string field.");
        await Assert.That((object)skill.StepType!).IsTypeOf<string>()
            .Because("the moniker stays a string descriptor (INV-8).");

        // The resolution surface exposes a compile-time symbol, not a System.Type. The Roslyn
        // symbol is the INTENDED output — only a CLR System.Type would be an INV-8 leak here.
        await Assert.That(typeof(INamedTypeSymbol).IsAssignableFrom(result.Symbol!.GetType())).IsTrue()
            .Because("the resolved identity is a Roslyn INamedTypeSymbol, not a CLR System.Type.");
        foreach (var property in typeof(WireMonikerResolution).GetProperties())
        {
            await Assert.That(CarriesClrType(property.PropertyType)).IsFalse()
                .Because($"WireMonikerResolution.{property.Name} must not expose a CLR System.Type (INV-8).");
        }

        // Structural INV-8: contract state (the wire-contract twins) must carry neither a CLR
        // System.Type NOR a Roslyn symbol — every type reference on the wire is a plain string moniker.
        var twinTypes = typeof(IWireContractDto).Assembly.GetTypes()
            .Where(t => typeof(IWireContractDto).IsAssignableFrom(t) && !t.IsInterface)
            .ToList();
        await Assert.That(twinTypes).IsNotEmpty()
            .Because("the wire-contract twins must be discoverable for the structural INV-8 guard.");
        foreach (var twin in twinTypes)
        {
            foreach (var property in twin.GetProperties())
            {
                await Assert.That(CarriesTypeOrSymbol(property.PropertyType)).IsFalse()
                    .Because($"{twin.Name}.{property.Name} must not carry a CLR System.Type or Roslyn symbol (INV-8: type references are string monikers).");
            }
        }
    }

    /// <summary>
    /// CS0101-class non-masking: a moniker shared across two workflow definitions resolves to ONE
    /// canonical CLR type both times. The resolver does not rename or synthesize per-import types, so
    /// the one-step-type-per-workflow-definition collision reaches the generator as the same build-error
    /// class C#-authored duplicates produce — the import path does not mask it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_SameMonikerTwice_YieldsSameCanonicalType_DoesNotMaskDuplicateCollision()
    {
        var compilation = BuildCompilation(SingleStepSource);

        var first = WireMonikerResolver.Resolve(compilation, "IntakeStepFixture", JsonPath);
        var second = WireMonikerResolver.Resolve(compilation, "IntakeStepFixture", "other.workflow.json");

        await Assert.That(first.IsResolved).IsTrue();
        await Assert.That(second.IsResolved).IsTrue();

        await Assert.That(SymbolEqualityComparer.Default.Equals(first.Symbol, second.Symbol)).IsTrue()
            .Because("a shared moniker binds the SAME CLR type across workflow definitions — the collision is preserved, not disguised.");
        await Assert.That(first.Symbol!.Name).IsEqualTo("IntakeStepFixture")
            .Because("the resolver returns the real declared type (name == moniker), never a synthesized/renamed stand-in.");
    }

    /// <summary>Whether a type is, or transitively carries, a CLR <see cref="Type"/>.</summary>
    private static bool CarriesClrType(Type type)
    {
        if (type == typeof(Type))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(CarriesClrType);
    }

    /// <summary>Whether a type is, or transitively carries, a CLR <see cref="Type"/> or Roslyn <see cref="ISymbol"/>.</summary>
    private static bool CarriesTypeOrSymbol(Type type)
    {
        if (type == typeof(Type) || typeof(ISymbol).IsAssignableFrom(type))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(CarriesTypeOrSymbol);
    }

    private static async Task AssertNoCompileErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToList();
        await Assert.That(errors).IsEmpty()
            .Because("the fixture source must compile so its step symbols are well-formed: " + string.Join("; ", errors));
    }

    private static Compilation BuildCompilation(string source, string assemblyName = "MonikerResolverTestAssembly")
    {
        var tree = CSharpSyntaxTree.ParseText(source);

        var locations = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Append(typeof(Strategos.Abstractions.IWorkflowState).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var references = locations
            .Select(l => (MetadataReference)MetadataReference.CreateFromFile(l))
            .ToList();

        return CSharpCompilation.Create(
            assemblyName,
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
