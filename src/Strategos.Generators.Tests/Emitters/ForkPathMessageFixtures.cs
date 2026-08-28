// -----------------------------------------------------------------------
// <copyright file="ForkPathMessageFixtures.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Shared workflow models for T1b path-qualified completed-event tests.
/// </summary>
internal static class ForkPathMessageFixtures
{
    public static WorkflowModel SharedTypeInstanceNamed()
    {
        var prepare = StepModel.Create("PrepareStep", "TestNamespace.PrepareStep");
        var technical = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "Technical");
        var fundamental = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "Fundamental");
        var synthesize = StepModel.Create("SynthesizeStep", "TestNamespace.SynthesizeStep");

        return new WorkflowModel(
            WorkflowName: "multi-analysis",
            PascalName: "MultiAnalysis",
            Namespace: "TestNamespace",
            StepNames: ["PrepareStep", "Technical", "Fundamental", "SynthesizeStep"],
            StateTypeName: "AnalysisState",
            Steps: [prepare, technical, fundamental, synthesize],
            Forks: [CreateAnalysisFork(technical, fundamental)]);
    }

    public static WorkflowModel SharedPhaseName()
    {
        var prepare = StepModel.Create("PrepareStep", "TestNamespace.PrepareStep");
        var path0 = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep");
        var path1 = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep");
        var synthesize = StepModel.Create("SynthesizeStep", "TestNamespace.SynthesizeStep");

        return new WorkflowModel(
            WorkflowName: "multi-analysis",
            PascalName: "MultiAnalysis",
            Namespace: "TestNamespace",
            StepNames: ["PrepareStep", "AnalyzeStep", "SynthesizeStep"],
            StateTypeName: "AnalysisState",
            Steps: [prepare, path0, synthesize],
            Forks: [CreateAnalysisFork(path0, path1)]);
    }

    public static WorkflowModel UniqueTypes()
    {
        var prepare = StepModel.Create("PrepareStep", "TestNamespace.PrepareStep");
        var technical = StepModel.Create("TechnicalAnalyzeStep", "TestNamespace.TechnicalAnalyzeStep");
        var fundamental = StepModel.Create("FundamentalAnalyzeStep", "TestNamespace.FundamentalAnalyzeStep");
        var synthesize = StepModel.Create("SynthesizeStep", "TestNamespace.SynthesizeStep");

        return new WorkflowModel(
            WorkflowName: "multi-analysis",
            PascalName: "MultiAnalysis",
            Namespace: "TestNamespace",
            StepNames: ["PrepareStep", "TechnicalAnalyzeStep", "FundamentalAnalyzeStep", "SynthesizeStep"],
            StateTypeName: "AnalysisState",
            Steps: [prepare, technical, fundamental, synthesize],
            Forks: [CreateAnalysisFork(technical, fundamental)]);
    }

    public static WorkflowModel SharedTypeInteriors()
    {
        var prepare = StepModel.Create("PrepareStep", "TestNamespace.PrepareStep");
        var intake0 = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "TechnicalIntake");
        var report0 = StepModel.Create("ReportStep", "TestNamespace.ReportStep", instanceName: "TechReport");
        var intake1 = StepModel.Create("AnalyzeStep", "TestNamespace.AnalyzeStep", instanceName: "FundamentalIntake");
        var report1 = StepModel.Create("ReportStep", "TestNamespace.ReportStep", instanceName: "FundReport");
        var synthesize = StepModel.Create("SynthesizeStep", "TestNamespace.SynthesizeStep");

        var fork = ForkModel.Create(
            forkId: "analysis",
            previousStepName: "PrepareStep",
            paths:
            [
                ForkPathModel.Create(0, [intake0, report0], false, false),
                ForkPathModel.Create(1, [intake1, report1], false, false),
            ],
            joinStepName: "SynthesizeStep");

        return new WorkflowModel(
            WorkflowName: "multi-analysis",
            PascalName: "MultiAnalysis",
            Namespace: "TestNamespace",
            StepNames: ["PrepareStep", "TechnicalIntake", "TechReport", "FundamentalIntake", "FundReport", "SynthesizeStep"],
            StateTypeName: "AnalysisState",
            Steps: [prepare, intake0, report0, intake1, report1, synthesize],
            Forks: [fork]);
    }

    private static ForkModel CreateAnalysisFork(StepModel path0End, StepModel path1End) =>
        ForkModel.Create(
            forkId: "analysis",
            previousStepName: "PrepareStep",
            paths:
            [
                ForkPathModel.Create(0, [path0End], false, false),
                ForkPathModel.Create(1, [path1End], false, false),
            ],
            joinStepName: "SynthesizeStep");
}
