// -----------------------------------------------------------------------
// <copyright file="RoundTripApprovalImportCompileTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using Strategos.Generators.Behavioral.Tests.Workflows;
using Strategos.Models;

namespace Strategos.Generators.Behavioral.Tests;

/// <summary>
/// FIX-5 (M10, DR-14 bucket a) — the CONTEXT-FREE approval importable family's bucket-(a) COMPILE
/// proof. The JSON import <c>roundtrip-approval.workflow.json</c> (whose <c>approvalPointId</c> is a
/// FIXED digit-leading GUID — the exact shape that crashed the generator with CS8785 before the
/// name-derivation fix) is bridged and lowered through the SAME saga emitters at BUILD time. This suite
/// REFERENCES the generated surface in compiled code, so it is the compiled-fixture proof the task
/// requires — NOT a parse-only tree-emission proxy:
/// <list type="bullet">
///   <item><description>
///     the generated saga <see cref="RoundtripApprovalImportSaga"/> and its
///     <c>StartRoundtripApprovalImportCommand</c> exist and compiled;
///   </description></item>
///   <item><description>
///     the resume command is named by the identifier DERIVED from the approver type
///     (<c>RtApprovalReviewerApprover</c> → <c>RtApprovalReviewer</c>) — <b>not</b> the raw GUID — and
///     carries the <see cref="ApprovalDecision"/> discriminant, so constructing it here proves both the
///     derivation and that <see cref="ApprovalDecision"/> resolves;
///   </description></item>
///   <item><description>
///     the generated DI extension <c>AddRoundtripApprovalImportWorkflow()</c> registers cleanly.
///   </description></item>
/// </list>
/// It needs NO Postgres/Docker: the generated <c>Add…Workflow(IServiceCollection)</c> only adds
/// transients. Reverting the name derivation makes the generator throw (CS8785) so the generated types
/// vanish and THIS project fails to build — the deterministic, build-level kill-probe.
/// </summary>
[Property("Category", "GeneratedCompile")]
public sealed class RoundTripApprovalImportCompileTests
{
    /// <summary>
    /// The generated context-free approval saga + its start/resume commands compiled, and the resume
    /// command uses the DERIVED approval-point name carrying an <see cref="ApprovalDecision"/> — proving
    /// the imported approval saga compiles (the whole point of the fix).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContextFreeApprovalImport_GeneratedSaga_CompilesAndUsesDerivedName()
    {
        // Referencing these generated types by NAME is a compile-time proof they were emitted and
        // compiled: the saga, its start command, and the resume command named by the DERIVED approval
        // point (RtApprovalReviewer), not the GUID approvalPointId.
        var sagaType = typeof(RoundtripApprovalImportSaga);
        var startCommandType = typeof(StartRoundtripApprovalImportCommand);

        // Constructing the resume command exercises BOTH the derived command name AND that the generated
        // saga's ApprovalDecision discriminant (Strategos.Models.ApprovalDecision) resolves and compiles.
        var resume = new ResumeRtApprovalReviewerApprovalCommand(Guid.NewGuid(), ApprovalDecision.Approved, null, null);

        await Assert.That(sagaType).IsNotNull();
        await Assert.That(startCommandType).IsNotNull();
        await Assert.That(resume.Decision).IsEqualTo(ApprovalDecision.Approved)
            .Because("the resume command carries the ApprovalDecision the saga's approval resume handler switches on.");
    }

    /// <summary>
    /// The generated <c>AddRoundtripApprovalImportWorkflow()</c> DI extension registers without throwing
    /// on a bare <see cref="ServiceCollection"/> — proving the lowered approval workflow's registration
    /// glue compiled and is invocable (no host/Docker required).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContextFreeApprovalImport_DiExtension_RegistersWithoutThrowing()
    {
        var services = new ServiceCollection();

        services.AddRoundtripApprovalImportWorkflow();

        await Assert.That(services.Count).IsGreaterThan(0)
            .Because("the generated approval-workflow registration must add its step and handler services.");
    }
}
