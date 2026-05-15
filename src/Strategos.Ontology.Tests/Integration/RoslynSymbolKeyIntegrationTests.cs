using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Strategos.Ontology;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.TestInfrastructure;

using TUnit.Core;

namespace Strategos.Ontology.Tests.Integration;

/// <summary>
/// DR-9 (Task 22): Roslyn round-trip integration test guarding against
/// drift between Strategos test fixtures and the basileus production
/// SCIP ingester. Compiles a small C# source string, extracts a named
/// type symbol, serializes its identity into the descriptor
/// <see cref="ObjectTypeDescriptor.SymbolKey"/>, and verifies the
/// descriptor reaches the composed <see cref="OntologyGraph"/> with
/// that identity preserved bit-for-bit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deviation from plan wording.</b> Task 22 in the plan suggests
/// <c>SymbolKey.Create(symbol).ToString()</c> but
/// <c>Microsoft.CodeAnalysis.SymbolKey</c> is internal to Roslyn —
/// it is not callable from a test project that references the public
/// <c>Microsoft.CodeAnalysis.CSharp</c> package. The plan explicitly
/// authorizes the public-surface substitute
/// <see cref="ISymbol.GetDocumentationCommentId"/>: it is stable,
/// mechanically derived from the symbol, and is the cref / DocId form
/// that downstream SCIP ingesters already canonicalize on. The
/// identity round-trip we care about — "the string a Roslyn-driven
/// ingester would put on a descriptor survives <see cref="OntologyBuilder.ApplyDelta"/>
/// and graph-freeze unchanged" — is preserved unchanged with this
/// substitution.
/// </para>
/// <para>
/// The test is gated behind the MSBuild property
/// <c>SkipRoslynIntegrationTests</c> via
/// <see cref="SkipIfRoslynIntegrationDisabledAttribute"/>. Set
/// <c>SkipRoslynIntegrationTests=true</c> at build time to short-
/// circuit this fixture on environments where the Roslyn dependency
/// is unwanted (e.g. minimal CI lanes).
/// </para>
/// </remarks>
public class RoslynSymbolKeyIntegrationTests
{
    private const string TradeOrderSource = """
        namespace Trading.Polyglot
        {
            public class TradeOrder
            {
                public string Id { get; set; } = "";
                public decimal Amount { get; set; }
            }
        }
        """;

    [Test]
    [SkipIfRoslynIntegrationDisabled]
    public async Task RoslynSymbolKey_RoundTripThroughBuilder_PreservesIdentity()
    {
        // --- Arrange: compile a small C# snippet via Roslyn ---
        var syntaxTree = CSharpSyntaxTree.ParseText(TradeOrderSource);
        var compilation = CSharpCompilation.Create(
            assemblyName: "RoslynSymbolKeyRoundTrip",
            syntaxTrees: new[] { syntaxTree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var symbol = compilation.GetTypeByMetadataName("Trading.Polyglot.TradeOrder");
        await Assert.That(symbol).IsNotNull();

        // SymbolKey is internal in Roslyn; the public, mechanically-
        // derived alternative is GetDocumentationCommentId() (cref ID).
        // See type-doc remarks above for the rationale.
        var serializedSymbolKey = symbol!.GetDocumentationCommentId();
        await Assert.That(serializedSymbolKey).IsNotNull();
        await Assert.That(serializedSymbolKey!).IsNotEmpty();

        // --- Act: build an ObjectTypeDescriptor that carries the
        // serialized symbol identity, push it through the source-drain
        // path so it lands via OntologyBuilder.ApplyDelta in
        // OntologyGraphBuilder.Build(). ---
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "TradeOrder",
            DomainName = "Trading",
            ClrType = null,
            SymbolKey = serializedSymbolKey,
            SymbolFqn = "Trading.Polyglot.TradeOrder",
            LanguageId = "dotnet",
            Source = DescriptorSource.Ingested,
            SourceId = "roslyn-roundtrip",
        };

        var timestamp = new DateTimeOffset(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
        var source = new TestOntologySource
        {
            SourceId = "roslyn-roundtrip",
            Deltas = ImmutableArray.Create<OntologyDelta>(
                new OntologyDelta.AddObjectType(descriptor)
                {
                    SourceId = "roslyn-roundtrip",
                    Timestamp = timestamp,
                }),
        };

        var graph = new OntologyGraphBuilder()
            .AddSources(new IOntologySource[] { source })
            .Build();

        // --- Assert: the descriptor reached the graph and its
        // SymbolKey survived bit-for-bit. ---
        var landed = graph.ObjectTypes.FirstOrDefault(
            ot => ot.DomainName == "Trading" && ot.Name == "TradeOrder");

        await Assert.That(landed).IsNotNull();
        await Assert.That(landed!.SymbolKey).IsEqualTo(serializedSymbolKey);
        await Assert.That(landed.ClrType).IsNull();
        await Assert.That(landed.Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(landed.SourceId).IsEqualTo("roslyn-roundtrip");
    }
}

/// <summary>
/// Conditional-skip attribute for the Roslyn integration fixture.
/// Two activation paths:
/// <list type="bullet">
///   <item><description>
///     <b>Build-time:</b> the MSBuild property
///     <c>SkipRoslynIntegrationTests=true</c> defines the
///     <c>SKIP_ROSLYN_INTEGRATION_TESTS</c> preprocessor symbol, which
///     unconditionally short-circuits this attribute's
///     <see cref="ShouldSkip"/> to <c>true</c>.
///   </description></item>
///   <item><description>
///     <b>Run-time:</b> the identically-named environment variable
///     (<c>SKIP_ROSLYN_INTEGRATION_TESTS=true|1</c>) skips the test
///     without rebuilding — convenient for ad-hoc CI lanes.
///   </description></item>
/// </list>
/// </summary>
internal sealed class SkipIfRoslynIntegrationDisabledAttribute : SkipAttribute
{
    public SkipIfRoslynIntegrationDisabledAttribute()
        : base("SkipRoslynIntegrationTests is set; skipping Roslyn integration fixture.")
    {
    }

    public override Task<bool> ShouldSkip(TestRegisteredContext context)
    {
#if SKIP_ROSLYN_INTEGRATION_TESTS
        return Task.FromResult(true);
#else
        var raw = Environment.GetEnvironmentVariable("SKIP_ROSLYN_INTEGRATION_TESTS");
        var skip = !string.IsNullOrEmpty(raw)
            && (raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                || raw == "1");
        return Task.FromResult(skip);
#endif
    }
}
