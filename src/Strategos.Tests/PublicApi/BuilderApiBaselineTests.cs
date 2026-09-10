// =============================================================================
// <copyright file="BuilderApiBaselineTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Steps;
using Strategos.Tests.FixtureExport;

namespace Strategos.Tests.PublicApi;

/// <summary>
/// #51 builder API-stability gate (PR-B), tasks T9/T10.
/// <para>
/// Microsoft.CodeAnalysis.PublicApiAnalyzers tracks an explicit public surface
/// of <c>src/Strategos</c> against its shipped and unshipped baselines. INV-1's
/// historical 7-entrypoint subset remains the surface exarchos's
/// <c>strategos-api-mirror.test.ts</c> consumes. Issue #167 extends the local
/// gate to the 3 continuations changed by typed occurrence configuration, the
/// public <see cref="WorkflowActionReference"/> value object, its
/// <see cref="StepDefinition"/> carrier property, and issue #169's reviewed
/// <see cref="CompensationConfiguration"/> surface, and the explicit rollback
/// identity carried by <see cref="StepContext"/>.
/// </para>
/// <para>
/// These tests assert the historical subset remains intact, the expanded
/// allowlist matches the analyzer config exactly, and no unrelated public type
/// leaks into the baseline. The analyzer itself enforces member-level drift at
/// build time; this suite guards the file-level invariants that scoping depends
/// on, in a fast deterministic unit test.
/// </para>
/// <para>
/// Shares the <c>PublicAPI.Shipped.txt-mutation</c> non-parallel key with
/// <see cref="GateFailClosedTests"/>: that suite transiently mutates the
/// on-disk baseline during its fail-closed proof, so these readers must be
/// serialized against it or they can observe the file mid-mutation and flake.
/// </para>
/// </summary>
[NotInParallel("PublicAPI.Shipped.txt-mutation")]
public sealed class BuilderApiBaselineTests
{
    /// <summary>
    /// The exactly-7 historical entrypoint interfaces established by #51.
    /// This named subset remains the downstream exarchos mirror boundary.
    /// </summary>
    private static readonly Type[] HistoricalEntrypointInterfaces =
    [
        typeof(IWorkflowBuilder<>),
        typeof(IBranchBuilder<>),
        typeof(ILoopBuilder<>),
        typeof(IForkJoinBuilder<>),
        typeof(IApprovalBuilder<,>),
        typeof(IFailureBuilder<>),
        typeof(IStepConfiguration<>),
    ];

    /// <summary>
    /// The continuation interfaces changed by #167. They are locally gated
    /// without redefining the historical seven-entrypoint mirror boundary.
    /// </summary>
    private static readonly Type[] Issue167ContinuationInterfaces =
    [
        typeof(ILoopForkJoinBuilder<>),
        typeof(IApprovalRejectionBuilder<>),
        typeof(IApprovalEscalationBuilder<>),
    ];

    /// <summary>All builder interfaces covered by the local analyzer gate.</summary>
    private static readonly Type[] TrackedBuilderInterfaces =
    [
        .. HistoricalEntrypointInterfaces,
        .. Issue167ContinuationInterfaces,
    ];

    /// <summary>
    /// DIM-5: the historical downstream entrypoint boundary, encoded as an
    /// explicit allowlist of 7 file stems (no <c>.cs</c>).
    /// <para>
    /// The design scopes the cross-product gate to "7 entrypoints" (the
    /// <c>IWorkflowBuilder</c> family). Issue #167 does not broaden what the
    /// exarchos mirror parses; it adds local baseline coverage for three changed
    /// continuations. See docs/designs/2026-05-24-slice-b-convergence-close.md
    /// (DIM-5).
    /// </para>
    /// </summary>
    private static readonly string[] HistoricalEntrypointFileStems =
    [
        "IWorkflowBuilder",
        "IBranchBuilder",
        "ILoopBuilder",
        "IForkJoinBuilder",
        "IApprovalBuilder",
        "IFailureBuilder",
        "IStepConfiguration",
    ];

    /// <summary>The exact <c>Abstractions/</c> analyzer allowlist.</summary>
    private static readonly string[] TrackedBuilderFileStems =
    [
        .. HistoricalEntrypointFileStems,
        "ILoopForkJoinBuilder",
        "IApprovalRejectionBuilder",
        "IApprovalEscalationBuilder",
    ];

    private static readonly string[] TrackedDefinitionFileStems =
    [
        "WorkflowActionReference",
        "StepDefinition",
        "CompensationConfiguration",
    ];

    private static readonly string[] TrackedStepFileStems =
    [
        "StepContext",
    ];

    private static string ShippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Shipped.txt");

    private static string UnshippedBaselinePath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "PublicAPI", "PublicAPI.Unshipped.txt");

    private static string EditorConfigPath { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", ".editorconfig");

    private static string AbstractionsDir { get; } = Path.Combine(
        FixturePaths.RepoRoot, "src", "Strategos", "Abstractions");

    [Test]
    public async Task ShippedBaseline_Exists()
    {
        await Assert.That(File.Exists(ShippedBaselinePath))
            .IsTrue();
    }

    [Test]
    public async Task ShippedBaseline_DeclaresAllSevenHistoricalEntrypoints()
    {
        var baseline = await File.ReadAllTextAsync(ShippedBaselinePath);

        foreach (var iface in HistoricalEntrypointInterfaces)
        {
            // PublicAPI.Shipped.txt records the unbound generic name without the
            // `1/`2 arity suffix, e.g. "Strategos.Builders.IWorkflowBuilder<TState>".
            var simpleName = iface.Name.Split('`')[0];
            var fqnPrefix = iface.Namespace + "." + simpleName;

            await Assert.That(baseline)
                .Contains(fqnPrefix);
        }
    }

    [Test]
    public async Task ShippedBaseline_DeclaresOnlyTrackedBuilderAndStepDefinitionTypes_Inv1()
    {
        var baseline = await File.ReadAllTextAsync(ShippedBaselinePath);
        var nonDirectiveLines = baseline
            .Split('\n')
            .Select(static l => l.Trim())
            .Where(static l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();

        // Every non-directive line belongs either to one of the 10 tracked
        // builder interfaces or to one of the explicitly reviewed definition
        // surfaces covered by this analyzer gate.
        foreach (var line in nonDirectiveLines)
        {
            var hasReviewedOwner = line.StartsWith("Strategos.Builders.", StringComparison.Ordinal) ||
                line.StartsWith("Strategos.Definitions.StepDefinition", StringComparison.Ordinal) ||
                line.StartsWith("static Strategos.Definitions.StepDefinition", StringComparison.Ordinal) ||
                line.StartsWith("Strategos.Definitions.CompensationConfiguration", StringComparison.Ordinal) ||
                line.StartsWith("static Strategos.Definitions.CompensationConfiguration", StringComparison.Ordinal) ||
                line.StartsWith("Strategos.Steps.StepContext", StringComparison.Ordinal) ||
                line.StartsWith("static Strategos.Steps.StepContext", StringComparison.Ordinal) ||
                line.StartsWith("Strategos.Definitions.WorkflowActionReference", StringComparison.Ordinal) ||
                line.StartsWith("static Strategos.Definitions.WorkflowActionReference", StringComparison.Ordinal);

            await Assert.That(hasReviewedOwner).IsTrue();
        }

        // The hard INV-1 contract still tracks exactly the 10 reviewed builder
        // types plus the reviewed non-builder declarations that shipped in the
        // last release: StepDefinition, CompensationConfiguration, StepContext, and
        // WorkflowActionReference (#167's occurrence identity value object, rolled
        // into Shipped for 3.0.0-rc.1; Shipped = present in the last release, see
        // src/Strategos/.editorconfig).
        // A type-declaration line is a bare fully-qualified type name with no
        // member ('.' after the type) and no signature arrow ('->').
        var typeDeclarationLines = nonDirectiveLines
            .Where(static l => !l.Contains("->", StringComparison.Ordinal))
            .ToArray();

        await Assert.That(typeDeclarationLines.Length).IsEqualTo(14);
        await Assert.That(typeDeclarationLines)
            .Contains("Strategos.Definitions.StepDefinition");
        await Assert.That(typeDeclarationLines)
            .Contains("Strategos.Definitions.WorkflowActionReference");
        await Assert.That(typeDeclarationLines)
            .Contains("Strategos.Definitions.CompensationConfiguration");
        await Assert.That(typeDeclarationLines)
            .Contains("Strategos.Steps.StepContext");

        var builderDeclarations = typeDeclarationLines
            .Where(static declaration => declaration.StartsWith("Strategos.Builders.", StringComparison.Ordinal))
            .ToArray();
        await Assert.That(builderDeclarations.Length).IsEqualTo(10);
        foreach (var decl in builderDeclarations)
        {
            await Assert.That(decl).StartsWith("Strategos.Builders.I");
        }
    }

    [Test]
    public async Task ApiBaselines_DeclareEveryPublicMemberOfTrackedInterfaces()
    {
        var shipped = await File.ReadAllTextAsync(ShippedBaselinePath);
        var unshipped = await File.ReadAllTextAsync(UnshippedBaselinePath);

        var baselineLines = (shipped + "\n" + unshipped)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var iface in TrackedBuilderInterfaces)
        {
            // Qualify by the DECLARING interface so a method name shared across
            // builder interfaces (e.g. Build/Complete) can't satisfy the check
            // via a different interface's line — a cross-interface false positive.
            var ownerPrefix = $"{iface.Namespace}.{iface.Name.Split('`')[0]}<";
            var declaredMembers = iface
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(static member => member is not MethodInfo { IsSpecialName: true });

            foreach (var member in declaredMembers)
            {
                // A member absent from both files is exactly the RS0016 that the analyzer
                // raises. New members belong in Unshipped until the release process rolls
                // them into Shipped, so the forcing function must read both authorities.
                var found = baselineLines.Any(l =>
                    l.StartsWith(ownerPrefix, StringComparison.Ordinal) &&
                    l.Contains($".{member.Name}", StringComparison.Ordinal));
                await Assert.That(found).IsTrue();
            }
        }
    }

    /// <summary>
    /// DIM-5 consistency check: the reflection-driven builder-type list and the
    /// file-stem allowlist must describe the same 10 interfaces.
    /// </summary>
    [Test]
    public async Task TrackedBuilderInterfaces_AndFileStems_DescribeSameTen()
    {
        var typeStems = TrackedBuilderInterfaces
            .Select(static t => t.Name.Split('`')[0])
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        var allowlistStems = TrackedBuilderFileStems
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(typeStems).IsEquivalentTo(allowlistStems);
    }

    [Test]
    public async Task HistoricalEntrypoints_RemainExactlySevenAndAreTracked()
    {
        await Assert.That(HistoricalEntrypointInterfaces.Length).IsEqualTo(7);

        var tracked = TrackedBuilderInterfaces.ToHashSet();
        foreach (var iface in HistoricalEntrypointInterfaces)
        {
            await Assert.That(tracked.Contains(iface)).IsTrue();
        }
    }

    /// <summary>
    /// DIM-5: close the INV-1 fail-OPEN scoping hole. The <c>.editorconfig</c>
    /// re-enables the build-breaking RS00xx diagnostics for ONLY a hardcoded
    /// brace-list of filenames under <c>Abstractions/</c>. If a builder
    /// tracked interface is renamed/moved/added without updating that glob, it
    /// silently drops out of the gate (fail-open). This asserts the brace-list
    /// exactly equals the intended allowlist, so any drift fails loudly.
    /// </summary>
    [Test]
    public async Task EditorConfig_ReEnableGlob_ExactlyMatchesTrackedBuilders()
    {
        var glob = await ReadEditorConfigBuilderGlobStemsAsync();

        var expected = TrackedBuilderFileStems
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();
        var actual = glob
            .OrderBy(static s => s, StringComparer.Ordinal)
            .ToArray();

        // Exact set equality: no missing entry (fail-open) and no extra entry
        // (scope creep beyond the reviewed allowlist).
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    public async Task EditorConfig_DefinitionReEnableSections_ExactlyMatchIssues167And169Surface()
    {
        var text = await File.ReadAllTextAsync(EditorConfigPath);
        var matches = Regex.Matches(
            text,
            @"\[Definitions/(?<stem>[^\]/{},]+)\.cs\]",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        var actual = matches
            .Select(static match => match.Groups["stem"].Value)
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(TrackedDefinitionFileStems);
    }

    [Test]
    public async Task EditorConfig_StepReEnableSections_ExactlyMatchIssue169Surface()
    {
        var text = await File.ReadAllTextAsync(EditorConfigPath);
        var matches = Regex.Matches(
            text,
            @"\[Steps/(?<stem>[^\]/{},]+)\.cs\]",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        var actual = matches
            .Select(static match => match.Groups["stem"].Value)
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(TrackedStepFileStems);
    }

    /// <summary>
    /// DIM-5 guard: every file the <c>.editorconfig</c> glob names must actually
    /// exist under <c>Abstractions/</c>. A glob entry that matches no file is a
    /// silent fail-open (the diagnostic is re-enabled for nothing), so a
    /// rename/move that left a stale glob entry behind fails here.
    /// </summary>
    [Test]
    public async Task EditorConfig_ReEnableGlob_NamesOnlyExistingAbstractionsFiles()
    {
        var glob = await ReadEditorConfigBuilderGlobStemsAsync();

        foreach (var stem in glob)
        {
            var path = Path.Combine(AbstractionsDir, stem + ".cs");
            await Assert.That(File.Exists(path))
                .IsTrue();
        }
    }

    [Test]
    public async Task TrackedDefinitionFiles_Exist()
    {
        foreach (var fileStem in TrackedDefinitionFileStems)
        {
            var path = Path.Combine(
                FixturePaths.RepoRoot,
                "src",
                "Strategos",
                "Definitions",
                fileStem + ".cs");

            await Assert.That(File.Exists(path)).IsTrue();
        }
    }

    [Test]
    public async Task TrackedStepFiles_Exist()
    {
        foreach (var fileStem in TrackedStepFileStems)
        {
            var path = Path.Combine(
                FixturePaths.RepoRoot,
                "src",
                "Strategos",
                "Steps",
                fileStem + ".cs");

            await Assert.That(File.Exists(path)).IsTrue();
        }
    }

    [Test]
    public async Task ApiBaselines_DeclareOnlyReviewedTopLevelTypes()
    {
        var shipped = await File.ReadAllTextAsync(ShippedBaselinePath);
        var unshipped = await File.ReadAllTextAsync(UnshippedBaselinePath);
        var declarations = (shipped + "\n" + unshipped)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !line.StartsWith('#') && !line.Contains("->", StringComparison.Ordinal))
            .ToArray();

        var expected = TrackedBuilderInterfaces
            .Select(static type => $"{type.Namespace}.{FormatUnboundGenericName(type)}")
            .Concat(
            [
                typeof(StepDefinition).FullName!,
                typeof(WorkflowActionReference).FullName!,
                typeof(CompensationConfiguration).FullName!,
                typeof(StepContext).FullName!,
            ])
            .ToArray();

        await Assert.That(declarations).IsEquivalentTo(expected);
    }

    [Test]
    public async Task StepDefinition_Action_RemainsAnInitOnlyActionReference()
    {
        var property = typeof(StepDefinition).GetProperty(
            nameof(StepDefinition.Action),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        await Assert.That(property).IsNotNull();
        await Assert.That(property!.PropertyType).IsEqualTo(typeof(WorkflowActionReference));
        await Assert.That(property.GetMethod?.IsPublic).IsTrue();
        await Assert.That(property.SetMethod?.IsPublic).IsTrue();
        await Assert.That(property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers())
            .Contains(typeof(IsExternalInit));
    }

    /// <summary>
    /// Parses the single <c>[Abstractions/{...}.cs]</c> brace-list section header
    /// from <c>src/Strategos/.editorconfig</c> and returns the file stems it names.
    /// </summary>
    private static async Task<string[]> ReadEditorConfigBuilderGlobStemsAsync()
    {
        var text = await File.ReadAllTextAsync(EditorConfigPath);

        // Match the builder re-enable section header, e.g.
        //   [Abstractions/{IWorkflowBuilder,IBranchBuilder,...}.cs]
        var match = Regex.Match(
            text,
            @"\[Abstractions/\{(?<stems>[^}]+)\}\.cs\]",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        await Assert.That(match.Success)
            .IsTrue();

        return match.Groups["stems"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    /// <summary>
    /// Owner prefixes of the four non-builder types the <c>.editorconfig</c> re-enable
    /// block brings into analyzer scope. Every member line under one of these owners
    /// must appear in <see cref="ReviewedMemberLines"/>.
    /// </summary>
    private static readonly string[] ReviewedMemberOwners =
    [
        "Strategos.Definitions.StepDefinition.",
        "Strategos.Definitions.CompensationConfiguration.",
        "Strategos.Definitions.WorkflowActionReference.",
        "Strategos.Steps.StepContext.",
    ];

    /// <summary>
    /// Member-level review allowlist (policy as data, the
    /// <c>UnvalidatedEntryPointRatchetTests</c> idiom). The other assertions in this
    /// file review the baseline at TYPE granularity: a member line such as
    /// <c>CompensationConfiguration.WithTimeout(System.TimeSpan) -> CompensationConfiguration!</c>
    /// satisfies the owner-prefix rule, is filtered out of the type count by its
    /// <c>-></c>, and is invisible to the top-level-type allowlist — which is how a
    /// shipped member with no authoring path entered <c>PublicAPI.Shipped.txt</c>
    /// unreviewed (PR #206 review). A member line under one of
    /// <see cref="ReviewedMemberOwners"/> is now accepted only when it is listed here
    /// verbatim; adding a member means adding its line to this set in the same change.
    /// </summary>
    private static readonly HashSet<string> ReviewedMemberLines = new(StringComparer.Ordinal)
    {
        "Strategos.Definitions.CompensationConfiguration.CompensationStepType.get -> System.Type!",
        "Strategos.Definitions.CompensationConfiguration.CompensationStepType.init -> void",
        "Strategos.Definitions.CompensationConfiguration.InverseAction.get -> Strategos.Definitions.WorkflowActionReference?",
        "Strategos.Definitions.CompensationConfiguration.InverseAction.init -> void",
        "Strategos.Definitions.CompensationConfiguration.RequiredOnFailure.get -> bool",
        "Strategos.Definitions.CompensationConfiguration.RequiredOnFailure.init -> void",
        "Strategos.Definitions.CompensationConfiguration.Timeout.get -> System.TimeSpan?",
        "Strategos.Definitions.CompensationConfiguration.Timeout.init -> void",
        "Strategos.Definitions.CompensationConfiguration.WithTimeout(System.TimeSpan timeout) -> Strategos.Definitions.CompensationConfiguration!",
        "Strategos.Definitions.StepDefinition.Action.get -> Strategos.Definitions.WorkflowActionReference?",
        "Strategos.Definitions.StepDefinition.Action.init -> void",
        "Strategos.Definitions.StepDefinition.AsLoopBodyStep(string! loopId) -> Strategos.Definitions.StepDefinition!",
        "Strategos.Definitions.StepDefinition.AsTerminal() -> Strategos.Definitions.StepDefinition!",
        "Strategos.Definitions.StepDefinition.Configuration.get -> Strategos.Definitions.StepConfigurationDefinition?",
        "Strategos.Definitions.StepDefinition.Configuration.init -> void",
        "Strategos.Definitions.StepDefinition.InstanceName.get -> string?",
        "Strategos.Definitions.StepDefinition.InstanceName.init -> void",
        "Strategos.Definitions.StepDefinition.IsLambdaStep.get -> bool",
        "Strategos.Definitions.StepDefinition.IsLambdaStep.init -> void",
        "Strategos.Definitions.StepDefinition.IsLoopBodyStep.get -> bool",
        "Strategos.Definitions.StepDefinition.IsLoopBodyStep.init -> void",
        "Strategos.Definitions.StepDefinition.IsTerminal.get -> bool",
        "Strategos.Definitions.StepDefinition.IsTerminal.init -> void",
        "Strategos.Definitions.StepDefinition.LambdaDelegate.get -> System.Delegate?",
        "Strategos.Definitions.StepDefinition.LambdaDelegate.init -> void",
        "Strategos.Definitions.StepDefinition.ParentLoopId.get -> string?",
        "Strategos.Definitions.StepDefinition.ParentLoopId.init -> void",
        "Strategos.Definitions.StepDefinition.StepId.get -> string!",
        "Strategos.Definitions.StepDefinition.StepId.init -> void",
        "Strategos.Definitions.StepDefinition.StepName.get -> string!",
        "Strategos.Definitions.StepDefinition.StepName.init -> void",
        "Strategos.Definitions.StepDefinition.StepType.get -> System.Type!",
        "Strategos.Definitions.StepDefinition.StepType.init -> void",
        "Strategos.Definitions.StepDefinition.StepTypeName.get -> string!",
        "Strategos.Definitions.StepDefinition.WithConfiguration(Strategos.Definitions.StepConfigurationDefinition! configuration) -> Strategos.Definitions.StepDefinition!",
        "Strategos.Steps.StepContext.CorrelationId.get -> string!",
        "Strategos.Steps.StepContext.CorrelationId.init -> void",
        "Strategos.Steps.StepContext.CurrentPhase.get -> string!",
        "Strategos.Steps.StepContext.CurrentPhase.init -> void",
        "Strategos.Steps.StepContext.ExecutionId.get -> System.Guid",
        "Strategos.Steps.StepContext.ExecutionId.init -> void",
        "Strategos.Steps.StepContext.IsCompensation.get -> bool",
        "Strategos.Steps.StepContext.RetryCount.get -> int",
        "Strategos.Steps.StepContext.RetryCount.init -> void",
        "Strategos.Steps.StepContext.RollbackId.get -> System.Guid?",
        "Strategos.Steps.StepContext.RollbackId.init -> void",
        "Strategos.Steps.StepContext.StepName.get -> string!",
        "Strategos.Steps.StepContext.StepName.init -> void",
        "Strategos.Steps.StepContext.Timestamp.get -> System.DateTimeOffset",
        "Strategos.Steps.StepContext.Timestamp.init -> void",
        "Strategos.Steps.StepContext.WorkflowId.get -> System.Guid",
        "Strategos.Steps.StepContext.WorkflowId.init -> void",
        "static Strategos.Definitions.CompensationConfiguration.Create(System.Type! stepType) -> Strategos.Definitions.CompensationConfiguration!",
        "static Strategos.Definitions.CompensationConfiguration.Create(System.Type! stepType, Strategos.Definitions.WorkflowActionReference! inverseAction) -> Strategos.Definitions.CompensationConfiguration!",
        "static Strategos.Definitions.CompensationConfiguration.Create<TStep>() -> Strategos.Definitions.CompensationConfiguration!",
        "static Strategos.Definitions.CompensationConfiguration.Create<TStep>(Strategos.Definitions.WorkflowActionReference! inverseAction) -> Strategos.Definitions.CompensationConfiguration!",
        "static Strategos.Definitions.StepDefinition.Create(System.Type! stepType, string? customName = null, string? instanceName = null) -> Strategos.Definitions.StepDefinition!",
        "static Strategos.Definitions.StepDefinition.CreateFromLambda(string! stepName, System.Delegate! lambdaDelegate) -> Strategos.Definitions.StepDefinition!",
        "static Strategos.Steps.StepContext.Create(System.Guid workflowId, string! stepName, string! currentPhase) -> Strategos.Steps.StepContext!",
        "Strategos.Definitions.WorkflowActionReference.ActionName.get -> string!",
        "Strategos.Definitions.WorkflowActionReference.Deconstruct(out string! domainName, out string! objectTypeName, out string! actionName) -> void",
        "Strategos.Definitions.WorkflowActionReference.DomainName.get -> string!",
        "Strategos.Definitions.WorkflowActionReference.ObjectTypeName.get -> string!",
        "Strategos.Definitions.WorkflowActionReference.WorkflowActionReference(string! domainName, string! objectTypeName, string! actionName) -> void",
    };

    [Test]
    public async Task ApiBaselines_DeclareOnlyReviewedMembersOfDefinitionAndStepTypes()
    {
        var shipped = await File.ReadAllTextAsync(ShippedBaselinePath);
        var unshipped = await File.ReadAllTextAsync(UnshippedBaselinePath);
        var baselineLines = (shipped + "\n" + unshipped)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var (unreviewed, missing) = ReviewMemberLines(baselineLines);

        await Assert.That(unreviewed).IsEmpty();
        await Assert.That(missing).IsEmpty();
    }

    /// <summary>
    /// Kill fixture for the member-level review: a baseline that carries one member
    /// line nobody reviewed must be reported, and a reviewed member that disappears
    /// must be reported too. Proves the predicate can fail, so a green
    /// <see cref="ApiBaselines_DeclareOnlyReviewedMembersOfDefinitionAndStepTypes"/>
    /// means the baseline matched the allowlist rather than that nothing was compared.
    /// </summary>
    [Test]
    public async Task ReviewMemberLines_ReportsAnUnreviewedMemberAndAMissingReviewedMember()
    {
        const string injected = "Strategos.Steps.StepContext.Unreviewed.get -> int";
        var reviewed = ReviewedMemberLines.ToList();
        var dropped = reviewed[0];
        var mutated = reviewed.Skip(1).Append(injected).ToArray();

        var (unreviewed, missing) = ReviewMemberLines(mutated);

        await Assert.That(unreviewed).IsEquivalentTo([injected]);
        await Assert.That(missing).IsEquivalentTo([dropped]);

        // The committed allowlist itself is accepted without noise.
        var (cleanUnreviewed, cleanMissing) = ReviewMemberLines(reviewed);
        await Assert.That(cleanUnreviewed).IsEmpty();
        await Assert.That(cleanMissing).IsEmpty();
    }

    /// <summary>
    /// Splits baseline lines under <see cref="ReviewedMemberOwners"/> into those absent
    /// from <see cref="ReviewedMemberLines"/> (unreviewed) and reviewed lines absent
    /// from the baseline (missing).
    /// </summary>
    private static (IReadOnlyList<string> Unreviewed, IReadOnlyList<string> Missing) ReviewMemberLines(
        IEnumerable<string> baselineLines)
    {
        var memberLines = baselineLines
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#') && line.Contains("->", StringComparison.Ordinal))
            .Where(static line =>
            {
                var bare = line.StartsWith("static ", StringComparison.Ordinal) ? line["static ".Length..] : line;
                return ReviewedMemberOwners.Any(owner => bare.StartsWith(owner, StringComparison.Ordinal));
            })
            .ToHashSet(StringComparer.Ordinal);

        var unreviewed = memberLines.Where(line => !ReviewedMemberLines.Contains(line)).Order(StringComparer.Ordinal).ToArray();
        var missing = ReviewedMemberLines.Where(line => !memberLines.Contains(line)).Order(StringComparer.Ordinal).ToArray();
        return (unreviewed, missing);
    }

    private static string FormatUnboundGenericName(Type type)
    {
        var simpleName = type.Name.Split('`')[0];
        var arguments = type.GetGenericArguments()
            .Select(static argument => argument.Name);
        return $"{simpleName}<{string.Join(", ", arguments)}>";
    }
}
