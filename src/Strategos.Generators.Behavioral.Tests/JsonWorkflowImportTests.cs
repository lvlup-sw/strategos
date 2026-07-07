// -----------------------------------------------------------------------
// <copyright file="JsonWorkflowImportTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Generators.Behavioral.Tests.Workflows;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// Task 017 (DR-12 bridge half + DR-3), the JSON import KEYSTONE — semantic proof.
/// </summary>
/// <remarks>
/// <para>
/// These tests need no running host. They are the executable face of the REQUIRED semantic check:
/// the gate-bearing <c>import-gate.workflow.json</c> was bridged to a <see cref="WorkflowModel"/>
/// and lowered through the SAME saga emitters as a C#-authored workflow AT BUILD TIME. That the
/// generated <c>ImportGateSaga</c> / <c>StartImportGateCommand</c> / <c>AddImportGateWorkflow()</c>
/// COMPILED into this test assembly — which these tests reference — is itself the proof that the
/// import bridge produced a valid saga through the one lowering path (INV-1).
/// </para>
/// <para>
/// They additionally pin a bridge-specific concern: the wire IR carries no state type, so the
/// bridge INFERS it from each step's <c>IWorkflowStep&lt;TState&gt;</c>. The generated start command
/// therefore binds the concrete <see cref="ImportState"/>, not <c>object</c>.
/// </para>
/// </remarks>
[Property("Category", "WorkflowIr")]
public sealed class JsonWorkflowImportTests
{
    /// <summary>
    /// The gate-bearing JSON import compiled into a real saga type through the import front-end.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportedGateWorkflow_GeneratedSagaType_Exists()
    {
        // ImportGateSaga is emitted by the generator ONLY from import-gate.workflow.json (there is
        // no C# ImportGate definition). Referencing it here forces it to have compiled — the
        // required semantic check that the bridge lowered a valid saga through the shared emitters.
        var sagaType = typeof(ImportGateSaga);

        await Assert.That(sagaType.Name).IsEqualTo("ImportGateSaga");
        await Assert.That(sagaType.Namespace).IsEqualTo(typeof(ImportState).Namespace)
            .Because("the imported saga is generated into the namespace inferred from its resolved step types.");
    }

    /// <summary>
    /// The wire IR carries no state type; the bridge infers <see cref="ImportState"/> from the
    /// step's <c>IWorkflowStep&lt;ImportState&gt;</c>, so the generated start command binds
    /// <see cref="ImportState"/> — not <c>object</c>.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportedGateWorkflow_StartCommand_BindsInferredState()
    {
        var initialStateProperty = typeof(StartImportGateCommand).GetProperty("InitialState");

        await Assert.That(initialStateProperty).IsNotNull()
            .Because("the generated Start command must carry the InitialState the saga starts from.");
        await Assert.That(initialStateProperty!.PropertyType).IsEqualTo(typeof(ImportState))
            .Because("the bridge must infer the workflow state type from the step's IWorkflowStep<ImportState>, not default to object.");
    }

    /// <summary>
    /// The generated DI extension for the imported workflow registers its step types (resolvable
    /// with the shared invocation log), exactly as a C#-authored workflow's does.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImportedGateWorkflow_DiExtension_RegistersStepTypes()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new WorkflowInvocationLog());

        // The generated Add{Pascal}Workflow() extension — compiled from the JSON import — registers
        // the imported saga's step types and worker handlers.
        services.AddImportGateWorkflow();

        using var provider = services.BuildServiceProvider();

        await Assert.That(provider.GetService<ImportGatePrepareStep>()).IsNotNull();
        await Assert.That(provider.GetService<ImportGateDecisionStep>()).IsNotNull();
        await Assert.That(provider.GetService<ImportGateFinishStep>()).IsNotNull();
    }
}

/// <summary>
/// Task 017 — the JSON import keystone, REAL-HOST proof. Runs the gate-bearing JSON-imported
/// workflow and its gate-free C#-authored twin end-to-end on a real Wolverine + Marten host and
/// asserts they behave identically (DR-3: the gate is inert consumer-plane data).
/// </summary>
/// <remarks>
/// Requires a reachable Docker daemon for the Postgres container (see <see cref="ImportHostFixture"/>).
/// When Postgres is unavailable this class's fixture fails to initialize and the test does not run;
/// the required semantic check is the BUILD compiling the imported saga (pinned by
/// <see cref="JsonWorkflowImportTests"/>), not the runtime execution here.
/// </remarks>
[Property("Category", "Integration")]
[NotInParallel]
[ClassDataSource<ImportHostFixture>(Shared = SharedType.PerClass)]
public sealed class JsonWorkflowImportHostTests
{
    private readonly ImportHostFixture host;

    /// <summary>Initializes a new instance of the <see cref="JsonWorkflowImportHostTests"/> class.</summary>
    /// <param name="host">The shared real-host fixture, injected by TUnit.</param>
    public JsonWorkflowImportHostTests(ImportHostFixture host)
    {
        this.host = host;
    }

    /// <summary>
    /// The gate-bearing JSON import runs its three steps to completion exactly as its gate-free C#
    /// twin — behaviorally identical, proving the JSON workflow lowered through the same execution
    /// machinery (INV-1) and that the gate is inert (DR-3).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GateBearingJsonImport_RunsIdentically_ToGateFreeCSharpTwin()
    {
        this.host.Invocations.Reset();

        var gateId = Guid.NewGuid();
        var gateCompleted = await this.host.RunWorkflowAsync<ImportGateSaga>(
            gateId,
            new StartImportGateCommand(gateId, new ImportState { WorkflowId = gateId }));

        var twinId = Guid.NewGuid();
        var twinCompleted = await this.host.RunWorkflowAsync<ImportTwinSaga>(
            twinId,
            new StartImportTwinCommand(twinId, new ImportState { WorkflowId = twinId }));

        // Both sagas reached their terminal phase (MarkCompleted() removed each saga document).
        await Assert.That(gateCompleted).IsTrue()
            .Because("the JSON-imported gate-bearing saga must run to completion on a real host.");
        await Assert.That(twinCompleted).IsTrue()
            .Because("the gate-free C# twin must run to completion on a real host.");

        // The gate-bearing import ran all three steps exactly once — the gate step ran as an
        // ordinary step (DR-3: it is inert).
        await Assert.That(this.host.Invocations.CountFor(nameof(ImportGatePrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ImportGateDecisionStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ImportGateFinishStep))).IsEqualTo(1);

        // The gate-free twin ran the same three-step shape.
        await Assert.That(this.host.Invocations.CountFor(nameof(ImportTwinPrepareStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ImportTwinDecisionStep))).IsEqualTo(1);
        await Assert.That(this.host.Invocations.CountFor(nameof(ImportTwinFinishStep))).IsEqualTo(1);

        // Identical behavior: three steps each, six total, no replays.
        await Assert.That(this.host.Invocations.TotalCount).IsEqualTo(6)
            .Because("the imported gate-bearing workflow and its gate-free twin must run the identical number of steps.");
    }
}
