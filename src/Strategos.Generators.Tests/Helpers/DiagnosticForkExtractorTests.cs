// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="DiagnosticForkExtractor"/> — parsing the fluent
/// <c>AllowDiagnosticFork(...)</c> chain into <see cref="DiagnosticForkModel"/> IR (DR-9, #151).
/// </summary>
[Property("Category", "Unit")]
public class DiagnosticForkExtractorTests
{
    // =============================================================================
    // A. Guard clause
    // =============================================================================

    /// <summary>
    /// Verifies that Extract throws ArgumentNullException when context is null.
    /// </summary>
    [Test]
    public void Extract_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DiagnosticForkExtractor.Extract(null!));
    }

    // =============================================================================
    // B. No fork case
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns an empty list when no AllowDiagnosticFork call exists.
    /// </summary>
    [Test]
    public async Task Extract_NoDiagnosticFork_ReturnsEmptyList()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Step1>().Then<Step2>().Finally<Step3>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        var result = DiagnosticForkExtractor.Extract(context);

        await Assert.That(result).IsEmpty();
    }

    // =============================================================================
    // C. Full-fidelity parse
    // =============================================================================

    /// <summary>
    /// Verifies that a well-formed AllowDiagnosticFork chain yields a single model carrying
    /// the correct anchors, permitted triggers (with evidence schema), compensation seed, and
    /// maxForks bound — the canonical DR-7/DR-9 example from the builder docs.
    /// </summary>
    [Test]
    public async Task Extract_CanonicalChain_YieldsModelWithAllFields()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<RatifyDeployment>()
                        .AllowDiagnosticFork(fork => fork
                            .Anchor(""RatifyDeployment"")
                            .PermitTrigger(ForkTrigger.RatificationFailure, ""provisionalStampEventId"")
                            .PermitTrigger(ForkTrigger.GateContradiction, ""leftGateId"", ""rightGateId"")
                            .WithCompensationSeed(""RollbackProvisionalStamp"")
                            .MaxForks(3))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "RatificationWorkflow");

        var result = DiagnosticForkExtractor.Extract(context);

        await Assert.That(result.Count).IsEqualTo(1);

        var model = result[0];
        await Assert.That(model.AnchorStepMonikers).IsEquivalentTo(new[] { "RatifyDeployment" });
        await Assert.That(model.CompensationSeedMoniker).IsEqualTo("RollbackProvisionalStamp");
        await Assert.That(model.MaxForks).IsEqualTo(3);

        await Assert.That(model.PermittedTriggerCount).IsEqualTo(2);

        // Triggers preserved in authored order.
        await Assert.That(model.PermittedTriggers[0].TriggerName).IsEqualTo("RatificationFailure");
        await Assert.That(model.PermittedTriggers[0].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "provisionalStampEventId" });

        await Assert.That(model.PermittedTriggers[1].TriggerName).IsEqualTo("GateContradiction");
        await Assert.That(model.PermittedTriggers[1].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "leftGateId", "rightGateId" });
    }

    /// <summary>
    /// Verifies that multiple anchor monikers (Anchor("a", "b", "c")) are all captured.
    /// </summary>
    [Test]
    public async Task Extract_MultipleAnchors_CapturesAllAnchorMonikers()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .AllowDiagnosticFork(fork => fork
                            .Anchor(""FirstAnchor"", ""SecondAnchor"", ""ThirdAnchor"")
                            .PermitTrigger(ForkTrigger.RatificationFailure, ""evId"")
                            .WithCompensationSeed(""Rollback"")
                            .MaxForks(2))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "MultiAnchorWorkflow");

        var result = DiagnosticForkExtractor.Extract(context);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].AnchorStepMonikers)
            .IsEquivalentTo(new[] { "FirstAnchor", "SecondAnchor", "ThirdAnchor" });
    }

    /// <summary>
    /// Verifies that two AllowDiagnosticFork edges on one workflow yield two models in order.
    /// </summary>
    [Test]
    public async Task Extract_TwoEdges_YieldsTwoModels()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .AllowDiagnosticFork(fork => fork
                            .Anchor(""AnchorOne"")
                            .PermitTrigger(ForkTrigger.RatificationFailure, ""evOne"")
                            .WithCompensationSeed(""RollbackOne"")
                            .MaxForks(1))
                        .AllowDiagnosticFork(fork => fork
                            .Anchor(""AnchorTwo"")
                            .PermitTrigger(ForkTrigger.GateContradiction, ""evTwo"")
                            .WithCompensationSeed(""RollbackTwo"")
                            .MaxForks(5))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TwoEdgeWorkflow");

        var result = DiagnosticForkExtractor.Extract(context);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].AnchorStepMonikers).IsEquivalentTo(new[] { "AnchorOne" });
        await Assert.That(result[0].MaxForks).IsEqualTo(1);
        await Assert.That(result[1].AnchorStepMonikers).IsEquivalentTo(new[] { "AnchorTwo" });
        await Assert.That(result[1].MaxForks).IsEqualTo(5);
        await Assert.That(result[1].PermittedTriggers[0].TriggerName).IsEqualTo("GateContradiction");
    }

    // =============================================================================
    // D. Facade parity — FluentDslParser delegates to the extractor
    // =============================================================================

    /// <summary>
    /// Verifies that the FluentDslParser facade surfaces the same parsed model, so the
    /// incremental generator's call site (which uses the facade) attaches a correct edge.
    /// </summary>
    [Test]
    public async Task FluentDslParser_ExtractDiagnosticForkModels_MatchesExtractor()
    {
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .AllowDiagnosticFork(fork => fork
                            .Anchor(""RatifyDeployment"")
                            .PermitTrigger(ForkTrigger.RatificationFailure, ""provisionalStampEventId"")
                            .WithCompensationSeed(""RollbackProvisionalStamp"")
                            .MaxForks(4))
                        .Finally<Complete>();
                }
            }";
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        var result = FluentDslParser.ExtractDiagnosticForkModels(root, semanticModel, CancellationToken.None);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].AnchorStepMonikers).IsEquivalentTo(new[] { "RatifyDeployment" });
        await Assert.That(result[0].MaxForks).IsEqualTo(4);
        await Assert.That(result[0].CompensationSeedMoniker).IsEqualTo("RollbackProvisionalStamp");
        await Assert.That(result[0].PermittedTriggers[0].TriggerName).IsEqualTo("RatificationFailure");
        await Assert.That(result[0].PermittedTriggers[0].RequiredEvidenceFields)
            .IsEquivalentTo(new[] { "provisionalStampEventId" });
    }

    // =============================================================================
    // Private helpers
    // =============================================================================

    private static FluentDslParseContext CreateContext(string source, string workflowName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var typeDeclaration = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, CancellationToken.None);
    }
}
