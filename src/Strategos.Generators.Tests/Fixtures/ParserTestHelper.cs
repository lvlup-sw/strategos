// -----------------------------------------------------------------------
// <copyright file="ParserTestHelper.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Strategos.Generators.Tests.Fixtures;

/// <summary>
/// Provides test infrastructure for testing the FluentDslParser directly.
/// </summary>
internal static class ParserTestHelper
{
    /// <summary>
    /// Extracts step models from the provided source code using the FluentDslParser.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <returns>A list of step models extracted from the workflow.</returns>
    public static IReadOnlyList<StepModel> ExtractStepModels(string source) =>
        ExtractStepModelsCore(source, validateCompilation: false);

    /// <summary>
    /// Extracts step models after proving that the supplied parser fixture compiles.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <returns>A list of step models extracted from the workflow.</returns>
    public static IReadOnlyList<StepModel> ExtractStepModelsValidated(string source) =>
        ExtractStepModelsCore(source, validateCompilation: true);

    private static IReadOnlyList<StepModel> ExtractStepModelsCore(
        string source,
        bool validateCompilation)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (validateCompilation)
        {
            EnsureCompiles(compilation);
        }

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        // Find the class with the Workflow attribute
        var workflowClass = root
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(IsWorkflowAttribute));

        if (workflowClass is null)
        {
            return [];
        }

        return FluentDslParser.ExtractStepModels(workflowClass, semanticModel, CancellationToken.None);
    }

    /// <summary>
    /// Extracts loop models from the provided source code using the FluentDslParser.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <param name="workflowName">The workflow name for condition ID generation.</param>
    /// <returns>A list of loop models extracted from the workflow.</returns>
    public static IReadOnlyList<LoopModel> ExtractLoopModels(string source, string workflowName = "TestWorkflow")
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        // Find the class with the Workflow attribute
        var workflowClass = root
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(IsWorkflowAttribute));

        if (workflowClass is null)
        {
            return [];
        }

        return FluentDslParser.ExtractLoopModels(workflowClass, semanticModel, workflowName, CancellationToken.None);
    }

    /// <summary>
    /// Extracts branch models from the provided source code using the FluentDslParser.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <param name="workflowName">The workflow name for branch ID generation.</param>
    /// <returns>A list of branch models extracted from the workflow.</returns>
    public static IReadOnlyList<BranchModel> ExtractBranchModels(string source, string workflowName = "TestWorkflow")
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        // Find the class with the Workflow attribute
        var workflowClass = root
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(IsWorkflowAttribute));

        if (workflowClass is null)
        {
            return [];
        }

        return FluentDslParser.ExtractBranchModels(workflowClass, semanticModel, workflowName, CancellationToken.None);
    }

    /// <summary>
    /// Compiles the provided source and returns the workflow-attributed type declaration together
    /// with its semantic model, so a caller can drive several parser entry points over one parse.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <returns>The workflow type declaration and its semantic model.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the source carries no workflow-attributed type.
    /// </exception>
    public static (TypeDeclarationSyntax WorkflowClass, SemanticModel SemanticModel) CompileWorkflow(string source) =>
        CompileWorkflowCore(source, validateCompilation: false);

    /// <summary>
    /// Compiles validated source and returns its workflow declaration and semantic model.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <returns>The workflow type declaration and its semantic model.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the source does not compile or carries no workflow-attributed type.
    /// </exception>
    public static (TypeDeclarationSyntax WorkflowClass, SemanticModel SemanticModel) CompileWorkflowValidated(
        string source) =>
        CompileWorkflowCore(source, validateCompilation: true);

    private static (TypeDeclarationSyntax WorkflowClass, SemanticModel SemanticModel) CompileWorkflowCore(
        string source,
        bool validateCompilation)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (validateCompilation)
        {
            EnsureCompiles(compilation);
        }

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var workflowClass = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(IsWorkflowAttribute));

        return workflowClass is null
            ? throw new InvalidOperationException("No workflow-attributed type declaration found in source.")
            : (workflowClass, semanticModel);
    }

    /// <summary>
    /// Creates a FluentDslParseContext from the provided source code.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <param name="workflowName">The workflow name for ID generation.</param>
    /// <returns>A FluentDslParseContext for the workflow.</returns>
    public static FluentDslParseContext CreateParseContext(string source, string workflowName = "TestWorkflow") =>
        CreateParseContextCore(source, workflowName, validateCompilation: false);

    /// <summary>
    /// Creates a <see cref="FluentDslParseContext"/> after proving that the supplied fixture compiles.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <param name="workflowName">The workflow name for ID generation.</param>
    /// <returns>A FluentDslParseContext for the workflow.</returns>
    public static FluentDslParseContext CreateParseContextValidated(
        string source,
        string workflowName = "TestWorkflow") =>
        CreateParseContextCore(source, workflowName, validateCompilation: true);

    private static FluentDslParseContext CreateParseContextCore(
        string source,
        string workflowName,
        bool validateCompilation)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        if (validateCompilation)
        {
            EnsureCompiles(compilation);
        }

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        // Find any type declaration (not just ones with Workflow attribute)
        var typeDeclaration = root
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        if (typeDeclaration is null)
        {
            throw new InvalidOperationException("No type declaration found in source.");
        }

        return FluentDslParseContext.Create(typeDeclaration, semanticModel, workflowName, CancellationToken.None);
    }

    /// <summary>
    /// Extracts approval models from the provided source code using the FluentDslParser.
    /// </summary>
    /// <param name="source">The source code containing a workflow definition.</param>
    /// <param name="workflowName">The workflow name for approval ID generation.</param>
    /// <returns>A list of approval models extracted from the workflow.</returns>
    public static IReadOnlyList<ApprovalModel> ExtractApprovalModels(string source, string workflowName = "TestWorkflow")
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        // Find the class with the Workflow attribute
        var workflowClass = root
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(t => t.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(IsWorkflowAttribute));

        if (workflowClass is null)
        {
            return [];
        }

        return FluentDslParser.ExtractApprovalModels(workflowClass, semanticModel, workflowName, CancellationToken.None);
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
            "System.Linq.Expressions.dll",
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

    private static void EnsureCompiles(CSharpCompilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "The parser fixture does not compile: "
            + string.Join(" | ", errors.Select(static diagnostic =>
                diagnostic.Id + ": " + diagnostic.GetMessage())));
    }

    private static bool IsWorkflowAttribute(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name is "Workflow" or "WorkflowAttribute"
            || name.EndsWith(".Workflow", StringComparison.Ordinal)
            || name.EndsWith(".WorkflowAttribute", StringComparison.Ordinal);
    }
}
