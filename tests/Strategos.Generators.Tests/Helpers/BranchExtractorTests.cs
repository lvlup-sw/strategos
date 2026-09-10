// -----------------------------------------------------------------------
// <copyright file="BranchExtractorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="BranchExtractor"/>.
/// </summary>
[Property("Category", "Unit")]
public class BranchExtractorTests
{
    // =============================================================================
    // A. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract throws ArgumentNullException when context is null.
    /// </summary>
    [Test]
    public void Extract_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            BranchExtractor.Extract(null!));
    }

    // =============================================================================
    // B. Branch Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Extract returns empty list when no Branch calls exist.
    /// </summary>
    [Test]
    public async Task Extract_NoBranchCalls_ReturnsEmptyList()
    {
        // Arrange
        const string code = @"
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Step1>()
                        .Then<Step2>()
                        .Finally<Step3>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    /// <summary>
    /// Verifies that Extract extracts branch discriminator property path.
    /// </summary>
    [Test]
    public async Task Extract_WithBranch_ExtractsPropertyPath()
    {
        // Arrange
        const string code = @"
            public enum ClaimType { Auto, Home }
            public class State { public ClaimType Type { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Branch(
                            state => state.Type,
                            BranchCase<State, ClaimType>.When(ClaimType.Auto, path => path.Then<ProcessAuto>()),
                            BranchCase<State, ClaimType>.When(ClaimType.Home, path => path.Then<ProcessHome>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "ClaimWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].DiscriminatorPropertyPath).IsEqualTo("Type");
    }

    /// <summary>
    /// Verifies that Extract generates correct branch ID.
    /// </summary>
    [Test]
    public async Task Extract_WithBranch_GeneratesBranchId()
    {
        // Arrange
        const string code = @"
            public enum ClaimType { Auto, Home }
            public class State { public ClaimType Type { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Branch(
                            state => state.Type,
                            BranchCase<State, ClaimType>.When(ClaimType.Auto, path => path.Then<ProcessAuto>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "ClaimWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].BranchId).IsEqualTo("ClaimWorkflow-Branch0-Type");
    }

    /// <summary>
    /// Verifies that Extract extracts multiple cases.
    /// </summary>
    [Test]
    public async Task Extract_MultipleCases_ExtractsAllCases()
    {
        // Arrange
        const string code = @"
            public enum ClaimType { Auto, Home, Life }
            public class State { public ClaimType Type { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Branch(
                            state => state.Type,
                            BranchCase<State, ClaimType>.When(ClaimType.Auto, path => path.Then<ProcessAuto>()),
                            BranchCase<State, ClaimType>.When(ClaimType.Home, path => path.Then<ProcessHome>()),
                            BranchCase<State, ClaimType>.When(ClaimType.Life, path => path.Then<ProcessLife>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "ClaimWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result[0].Cases.Count).IsEqualTo(3);
    }

    // =============================================================================
    // C. Consecutive Branch Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a single branch after a step has PreviousStepName set.
    /// </summary>
    [Test]
    public async Task Extract_SingleBranchAfterStep_HasPreviousStepName()
    {
        // Arrange - Simple case: one branch after a Then step
        const string code = @"
            public class State { public bool Cond { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<ValidateStep>()
                        .Branch(
                            state => state.Cond,
                            BranchCase<State, bool>.When(true, path => path.Then<Step1>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].PreviousStepName).IsEqualTo("ValidateStep");
    }

    /// <summary>
    /// Verifies that consecutive branches have empty PreviousStepName for the second one.
    /// </summary>
    [Test]
    public async Task Extract_ConsecutiveBranches_SecondBranchHasEmptyPreviousStepName()
    {
        // Arrange
        const string code = @"
            public class State
            {
                public bool Cond1 { get; set; }
                public bool Cond2 { get; set; }
            }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<ValidateStep>()
                        .Branch(
                            state => state.Cond1,
                            BranchCase<State, bool>.When(true, path => path.Then<Step1>()))
                        .Branch(
                            state => state.Cond2,
                            BranchCase<State, bool>.When(true, path => path.Then<Step2>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].PreviousStepName).IsEqualTo("ValidateStep"); // First branch has previous step
        await Assert.That(result[1].PreviousStepName).IsEqualTo(string.Empty); // Second branch is consecutive
    }

    /// <summary>
    /// Verifies that consecutive branches are linked via NextConsecutiveBranch.
    /// </summary>
    [Test]
    public async Task Extract_ConsecutiveBranches_HeadBranchLinksToNextConsecutiveBranch()
    {
        // Arrange
        const string code = @"
            public class State
            {
                public bool Cond1 { get; set; }
                public bool Cond2 { get; set; }
            }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<ValidateStep>()
                        .Branch(
                            state => state.Cond1,
                            BranchCase<State, bool>.When(true, path => path.Then<Step1>()))
                        .Branch(
                            state => state.Cond2,
                            BranchCase<State, bool>.When(true, path => path.Then<Step2>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].HasNextConsecutiveBranch).IsTrue();
        await Assert.That(result[0].NextConsecutiveBranch!.DiscriminatorPropertyPath).IsEqualTo("Cond2");
    }

    /// <summary>
    /// Verifies that three consecutive branches are linked correctly.
    /// </summary>
    [Test]
    public async Task Extract_ThreeConsecutiveBranches_LinksChainCorrectly()
    {
        // Arrange
        const string code = @"
            public class State
            {
                public bool Cond1 { get; set; }
                public bool Cond2 { get; set; }
                public bool Cond3 { get; set; }
            }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<ValidateStep>()
                        .Branch(
                            state => state.Cond1,
                            BranchCase<State, bool>.When(true, path => path.Then<Step1>()))
                        .Branch(
                            state => state.Cond2,
                            BranchCase<State, bool>.When(true, path => path.Then<Step2>()))
                        .Branch(
                            state => state.Cond3,
                            BranchCase<State, bool>.When(true, path => path.Then<Step3>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        // Act
        var result = BranchExtractor.Extract(context);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);

        // First branch (head) links to second
        await Assert.That(result[0].HasNextConsecutiveBranch).IsTrue();
        await Assert.That(result[0].NextConsecutiveBranch!.DiscriminatorPropertyPath).IsEqualTo("Cond2");

        // Second in chain links to third
        await Assert.That(result[0].NextConsecutiveBranch!.HasNextConsecutiveBranch).IsTrue();
        await Assert.That(result[0].NextConsecutiveBranch!.NextConsecutiveBranch!.DiscriminatorPropertyPath).IsEqualTo("Cond3");

        // Third (tail) has no next
        await Assert.That(result[0].NextConsecutiveBranch!.NextConsecutiveBranch!.HasNextConsecutiveBranch).IsFalse();
    }

    // =============================================================================
    // D. Instance Name Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a branch case keeps the instance name from <c>Then&lt;T&gt;("Instance")</c>
    /// instead of storing the bare type.
    /// </summary>
    [Test]
    public async Task Extract_BranchCaseWithInstanceName_StoresEffectiveName()
    {
        const string code = @"
            public enum ClaimType { Auto, Home }
            public class State { public ClaimType Type { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Branch(
                            state => state.Type,
                            BranchCase<State, ClaimType>.When(ClaimType.Auto, path => path.Then<ProcessClaim>(""AutoClaim"")),
                            BranchCase<State, ClaimType>.When(ClaimType.Home, path => path.Then<ProcessClaim>(""HomeClaim"")))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "ClaimWorkflow");

        var result = BranchExtractor.Extract(context);

        await Assert.That(result[0].Cases[0].StepNames[0]).IsEqualTo("AutoClaim");
        await Assert.That(result[0].Cases[0].LastStepName).IsEqualTo("AutoClaim");
        await Assert.That(result[0].Cases[1].StepNames[0]).IsEqualTo("HomeClaim");
        await Assert.That(result[0].Cases[1].LastStepName).IsEqualTo("HomeClaim");
    }

    /// <summary>
    /// Verifies that a branch following an instance-named step keys PreviousStepName by
    /// effective name, not bare type.
    /// </summary>
    [Test]
    public async Task Extract_BranchAfterInstanceNamedStep_PreviousStepNameIsEffectiveName()
    {
        const string code = @"
            public class State { public bool Cond { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<ValidateStep>(""PreCheck"")
                        .Branch(
                            state => state.Cond,
                            BranchCase<State, bool>.When(true, path => path.Then<Step1>()))
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        var result = BranchExtractor.Extract(context);

        await Assert.That(result[0].PreviousStepName).IsEqualTo("PreCheck");
    }

    /// <summary>
    /// Verifies that a branch rejoining an instance-named step keys RejoinStepName by
    /// effective name, not bare type.
    /// </summary>
    [Test]
    public async Task Extract_BranchRejoiningInstanceNamedStep_RejoinStepNameIsEffectiveName()
    {
        const string code = @"
            public class State { public bool Cond { get; set; } }
            public class Workflow
            {
                public void Define()
                {
                    builder.StartWith<Init>()
                        .Then<ValidateStep>()
                        .Branch(
                            state => state.Cond,
                            BranchCase<State, bool>.When(true, path => path.Then<Step1>()))
                        .Then<FinalizeStep>(""CloseOut"")
                        .Finally<Complete>();
                }
            }";
        var context = CreateContext(code, "TestWorkflow");

        var result = BranchExtractor.Extract(context);

        await Assert.That(result[0].RejoinStepName).IsEqualTo("CloseOut");
    }

    // =============================================================================
    // Private Helpers
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
