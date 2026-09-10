// -----------------------------------------------------------------------
// <copyright file="StepExtractorInstanceNameTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// TDD Cycle 6: Tests for instance name extraction from DSL syntax.
/// These tests verify that StepExtractor correctly extracts instance names
/// from Then&lt;TStep&gt;("InstanceName") calls in the DSL.
/// </summary>
[Property("Category", "Unit")]
public class StepExtractorInstanceNameTests
{
    // =============================================================================
    // A. Instance Name Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos extracts instance name from Then call.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_WithInstanceName_ExtractsName()
    {
        // Arrange - Workflow with instance name
        const string source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record TestState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class ValidateStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(
                    TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            public class ProcessStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(
                    TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(
                    TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            [Workflow("instance-name-test")]
            public static partial class InstanceNameWorkflow
            {
                public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                    .Create("instance-name-test")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>("CustomProcessing")
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(source);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - ProcessStep should have InstanceName "CustomProcessing"
        var processStep = rawSteps.FirstOrDefault(s => s.StepName == "ProcessStep");
        await Assert.That(processStep).IsNotNull();
        await Assert.That(processStep!.InstanceName).IsEqualTo("CustomProcessing");
    }

    /// <summary>
    /// Verifies that ExtractRawStepInfos returns null InstanceName when not specified.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_WithoutInstanceName_InstanceNameIsNull()
    {
        // Arrange - Workflow without instance names
        const string source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record TestState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class ValidateStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(
                    TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            public class ProcessStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(
                    TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<TestState>
            {
                public Task<StepResult<TestState>> ExecuteAsync(
                    TestState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<TestState>.FromState(state));
            }

            [Workflow("no-instance-name-test")]
            public static partial class NoInstanceNameWorkflow
            {
                public static WorkflowDefinition<TestState> Definition => Workflow<TestState>
                    .Create("no-instance-name-test")
                    .StartWith<ValidateStep>()
                    .Then<ProcessStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(source);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - All steps should have null InstanceName
        await Assert.That(rawSteps.All(s => s.InstanceName == null)).IsTrue();
    }

    // =============================================================================
    // B. EffectiveName Computation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepInfo.EffectiveName returns InstanceName when present.
    /// </summary>
    [Test]
    public async Task StepInfo_EffectiveName_UsesInstanceNameWhenPresent()
    {
        // Arrange
        var stepWithInstance = new StepInfo("ValidateStep", "PreValidation", null, StepContext.Linear);

        // Act & Assert
        await Assert.That(stepWithInstance.EffectiveName).IsEqualTo("PreValidation");
    }

    /// <summary>
    /// Verifies that StepInfo.EffectiveName returns StepName when InstanceName is null.
    /// </summary>
    [Test]
    public async Task StepInfo_EffectiveName_UsesStepNameWhenNoInstanceName()
    {
        // Arrange
        var stepWithoutInstance = new StepInfo("ValidateStep", null, null, StepContext.Linear);

        // Act & Assert
        await Assert.That(stepWithoutInstance.EffectiveName).IsEqualTo("ValidateStep");
    }

    // =============================================================================
    // C. Fork Path Instance Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that instance names are extracted from fork path steps.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_ForkPathWithInstanceNames_ExtractsNames()
    {
        // Arrange - Workflow with instance names in fork paths
        const string source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record AnalysisState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            [Workflow("fork-instance-names")]
            public static partial class ForkInstanceNamesWorkflow
            {
                public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                    .Create("fork-instance-names")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("TechnicalAnalysis"),
                        path => path.Then<AnalyzeStep>("FundamentalAnalysis"))
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(source);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - Should have two AnalyzeStep entries with different instance names
        var analyzeSteps = rawSteps.Where(s => s.StepName == "AnalyzeStep").ToList();
        await Assert.That(analyzeSteps.Count).IsEqualTo(2);

        var instanceNames = analyzeSteps.Select(s => s.InstanceName).OrderBy(n => n).ToList();
        await Assert.That(instanceNames).Contains("FundamentalAnalysis");
        await Assert.That(instanceNames).Contains("TechnicalAnalysis");
    }

    /// <summary>
    /// Verifies that EffectiveName differs for steps with different instance names.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_ForkPathWithInstanceNames_EffectiveNamesDiffer()
    {
        // Arrange - Same as above
        const string source = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record AnalysisState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            [Workflow("fork-instance-names")]
            public static partial class ForkInstanceNamesWorkflow
            {
                public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                    .Create("fork-instance-names")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>("TechnicalAnalysis"),
                        path => path.Then<AnalyzeStep>("FundamentalAnalysis"))
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(source);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - EffectiveNames should be unique
        var analyzeSteps = rawSteps.Where(s => s.StepName == "AnalyzeStep").ToList();
        var effectiveNames = analyzeSteps.Select(s => s.EffectiveName).Distinct().ToList();
        await Assert.That(effectiveNames.Count).IsEqualTo(2);
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    private static FluentDslParseContext CreateContext(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(root, semanticModel, null, CancellationToken.None);
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Add core runtime references
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var coreAssemblies = new[]
        {
            "System.Runtime.dll",
            "System.Private.CoreLib.dll",
            "netstandard.dll",
        };

        foreach (var assembly in coreAssemblies)
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        // Add loaded assemblies (filtering out dynamic ones)
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
                    // Ignore assemblies that can't be loaded as references
                }
            }
        }

        // Add the Workflow library reference
        var workflowAssembly = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(workflowAssembly.Location))
        {
            references.Add(MetadataReference.CreateFromFile(workflowAssembly.Location));
        }

        return references;
    }
}
