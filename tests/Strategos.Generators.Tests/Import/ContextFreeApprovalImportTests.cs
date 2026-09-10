// -----------------------------------------------------------------------
// <copyright file="ContextFreeApprovalImportTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis.Text;

namespace Strategos.Generators.Tests.Import;

/// <summary>
/// FIX-5 (M10, DR-14 bucket a) — the import bridge lowers a CONTEXT-FREE approval whose wire
/// <c>approvalPointId</c> is a GUID identity (the shape <c>ApprovalDefinition.Create</c> mints:
/// <c>Guid.NewGuid().ToString("N")</c>). The bug: <c>WireToModelBridge.MapApprovals</c> fed that raw
/// GUID to <c>ApprovalModel.Create</c> as the approval-point NAME; a digit-leading GUID is not a valid
/// C# identifier, so <c>IdentifierValidator</c> threw and crashed the generator (CS8785) and NO saga was
/// emitted. The fix DERIVES a valid identifier from the approver type name via the SAME
/// <c>ApprovalPointNaming.Derive</c> the C#-authoring path uses, and keeps the GUID for identity only.
/// </summary>
/// <remarks>
/// The fixture uses a FIXED digit-leading id (<c>3f25…</c>) so the crash reproduces DETERMINISTICALLY:
/// the builder's random GUID only fails identifier validation when it happens to start with a digit
/// (0-9), so a random id makes the crash intermittent. Reverting the derivation (restoring the raw-GUID
/// name) makes this test go RED on that fixed id — the kill-probe. The generator runs over an in-memory
/// host compilation, so this proves the crash is gone and the DERIVED name is emitted; the REAL compile
/// of the lowered approval saga (which references <c>Strategos.Models.ApprovalDecision</c> and the
/// Wolverine saga base) is proven by the <c>Strategos.Generators.Behavioral.Tests</c> BUILD compiling
/// <c>AddRoundtripApprovalImportWorkflow()</c>.
/// </remarks>
[Property("Category", "WorkflowIr")]
public sealed class ContextFreeApprovalImportTests
{
    // The generator-failed diagnostic id: Roslyn reports CS8785 when a source generator throws.
    private const string GeneratorFailedCode = "CS8785";

    // The task-018 DR-14 context-bearing-approval rejection id (literal ids are permitted in *.Tests).
    private const string ApprovalContextCode = "AGWF031";

    // A FIXED digit-leading GUID (starts with '3'), matching ApprovalDefinition.Create's
    // Guid.NewGuid().ToString("N") shape, chosen so it is NOT a valid C# identifier — the deterministic
    // crash trigger for the pre-fix bridge.
    private const string DigitLeadingApprovalPointId = "3f2504e04f8941d39a0c0305e82c3301";

    /// <summary>Host step + approver types so the context-free approval resolves its monikers and lowers.</summary>
    private const string ApprovalHostTypes = """
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;

        namespace ContextFreeApprovalNs;

        [WorkflowState]
        public sealed record CfaState : IWorkflowState
        {
            public System.Guid WorkflowId { get; init; }
        }

        public sealed class CfaStart : IWorkflowStep<CfaState>
        {
            public Task<StepResult<CfaState>> ExecuteAsync(CfaState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<CfaState>.FromState(s));
        }

        public sealed class CfaEnd : IWorkflowStep<CfaState>
        {
            public Task<StepResult<CfaState>> ExecuteAsync(CfaState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<CfaState>.FromState(s));
        }

        // The approver moniker resolves through the SAME step resolver as any step (the established
        // import contract — see ImportFrontEndRobustnessTests M2, whose resolvable approver is a step).
        // Its simple name (CfaReviewApprover) derives the approval-point name (CfaReview).
        public sealed class CfaReviewApprover : IWorkflowStep<CfaState>
        {
            public Task<StepResult<CfaState>> ExecuteAsync(CfaState s, StepContext c, CancellationToken ct)
                => Task.FromResult(StepResult<CfaState>.FromState(s));
        }
        """;

    // A context-free approval (no hasContext / escalation / rejection): approvalPoints[0] carries only
    // the GUID identity, the approver moniker, and the preceding step id.
    private static readonly string ContextFreeApprovalJson = $$"""
        {
          "schemaVersion": "1.0",
          "name": "context-free-approval-import",
          "steps": [
            { "kind": "skill", "stepId": "s1", "stepName": "CfaStart", "isTerminal": false, "stepType": "CfaStart" },
            { "kind": "skill", "stepId": "s2", "stepName": "CfaEnd", "isTerminal": true, "stepType": "CfaEnd" }
          ],
          "transitions": [
            { "transitionId": "t1", "fromStepId": "s1", "toStepId": "s2", "isDefault": true }
          ],
          "branchPoints": [],
          "loops": [],
          "forkPoints": [],
          "failureHandlers": [],
          "approvalPoints": [
            { "approvalPointId": "{{DigitLeadingApprovalPointId}}", "approverType": "CfaReviewApprover", "precedingStepId": "s1" }
          ],
          "entryStepId": "s1",
          "terminalStepId": "s2"
        }
        """;

    /// <summary>
    /// The context-free approval import lowers a saga: a <c>*Saga.g.cs</c> tree is emitted, the
    /// generator did NOT crash (no CS8785), and no context-rejection (AGWF031) fired. Pre-fix the raw
    /// digit-leading GUID crashed the generator here, so no saga was emitted.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContextFreeApproval_WithGuidApprovalPointId_LowersSaga_WithoutCrash()
    {
        var result = RunGenerator(ApprovalHostTypes, ("context-free-approval.workflow.json", ContextFreeApprovalJson));

        await Assert.That(result.Diagnostics.Any(d => d.Id == GeneratorFailedCode)).IsFalse()
            .Because("the generator must NOT throw (CS8785) on the GUID approvalPointId — the name is derived, not the raw id.");
        await Assert.That(result.Diagnostics.Any(d => d.Id == ApprovalContextCode)).IsFalse()
            .Because("a context-free approval carries no lossy context, so AGWF031 must not fire.");
        await Assert.That(result.GeneratedTrees.Any(t => t.FilePath.EndsWith("Saga.g.cs", StringComparison.Ordinal))).IsTrue()
            .Because("a context-free approval is importable (bucket a): the bridge must lower a saga.");
    }

    /// <summary>
    /// The generated approval commands NAME the approval point by the identifier DERIVED from the
    /// approver type (<c>CfaReviewApprover</c> → <c>CfaReview</c>) — <c>ResumeCfaReviewApprovalCommand</c>
    /// — and NEVER the raw GUID. This pins the specific fix: the wire id is identity-only, and the C#
    /// identifier comes from the shared <c>ApprovalPointNaming.Derive</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContextFreeApproval_GeneratedCommands_UseDerivedName_NotGuid()
    {
        var result = RunGenerator(ApprovalHostTypes, ("context-free-approval.workflow.json", ContextFreeApprovalJson));

        var commands = result.GeneratedTrees
            .Where(t => t.FilePath.EndsWith("Commands.g.cs", StringComparison.Ordinal))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault() ?? string.Empty;

        await Assert.That(commands).Contains("ResumeCfaReviewApprovalCommand")
            .Because("the resume command must be named by the identifier derived from the approver type (CfaReviewApprover → CfaReview).");
        await Assert.That(commands).DoesNotContain(DigitLeadingApprovalPointId)
            .Because("the wire GUID approvalPointId is identity-only and must never surface as the generated point name.");
    }

    private static GeneratorDriverRunResult RunGenerator(string source, params (string Path, string Content)[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "ContextFreeApprovalImportTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: GetReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = additionalTexts
            .Select(t => (AdditionalText)new InMemoryAdditionalText(t.Path, t.Content))
            .ToArray();

        var driver = CSharpGeneratorDriver.Create(
            generators: [new WorkflowIncrementalGenerator().AsSourceGenerator()],
            additionalTexts: texts,
            parseOptions: null,
            optionsProvider: null);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private static List<MetadataReference> GetReferences()
    {
        var references = new List<MetadataReference>();

        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var assembly in new[] { "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references.
                }
            }
        }

        var abstractions = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(abstractions.Location))
        {
            references.Add(MetadataReference.CreateFromFile(abstractions.Location));
        }

        return references;
    }

    /// <summary>An in-memory <see cref="AdditionalText"/> for driving the generator over the fixture.</summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText text;

        public InMemoryAdditionalText(string path, string content)
        {
            this.Path = path;
            this.text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.text;
    }
}
