// -----------------------------------------------------------------------
// <copyright file="ProofCatalogExport.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Strategos.Analyzers.Proof;
using Strategos.Generators.Diagnostics;
using Strategos.Generators.Helpers;

namespace Strategos.Generators.Proof;

// =============================================================================
// Getting the catalog across the boundary (#204).
//
// The catalog travels as an assembly-level attribute carrying its JSON. That is
// the only channel that works for every consumer this repository has:
//
//   - It survives packaging. A NuGet content file does not reach a REFERENCING
//     compilation's analyzer; assembly metadata does, through the same
//     MetadataReference the consumer already has.
//   - It needs no file I/O, no reflection, and no user code to run. The consumer
//     reads it with Roslyn symbols, which is all an analyzer is allowed.
//   - It carries no CLR identity. The attribute's argument is a string; nothing
//     about the shape is .NET-specific except the envelope.
//
// The attribute TYPE is emitted into the producing assembly rather than shipped
// in a runtime package, because the two producer kinds reference different
// packages — an ontology-only assembly references Strategos.Ontology, which is
// self-contained by U-2 and depends on nothing. A shared attribute type would
// need a shared assembly that does not exist, or a dependency that U-2 forbids.
// Consumers therefore match it by full metadata NAME, which is what a
// cross-assembly analyzer does anyway.
// =============================================================================

/// <summary>Emits and reads the assembly-level proof-catalog attribute.</summary>
internal static class ProofCatalogExport
{
    /// <summary>The namespace of the generated marker attribute.</summary>
    internal const string AttributeNamespace = "Strategos.Generated";

    /// <summary>The unqualified name of the generated marker attribute.</summary>
    internal const string AttributeTypeName = "StrategosProofCatalogAttribute";

    /// <summary>The full metadata name consumers match on.</summary>
    internal const string AttributeFullName = AttributeNamespace + "." + AttributeTypeName;

    /// <summary>The hint name of the generated catalog source file.</summary>
    internal const string HintName = "StrategosProofCatalog.g.cs";

    /// <summary>
    /// Builds this compilation's catalog, reports anything that cannot be exported,
    /// and emits the attribute.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="compilation">The compilation being built.</param>
    /// <param name="catalog">The compilation-local ontology catalog.</param>
    /// <param name="localWorkflowNames">The workflow identities this compilation lowers.</param>
    /// <returns>
    /// The identities this assembly actually exported. A local binding is deferred to
    /// a referencing compilation only when ITS action is in this set — an action the
    /// export refused is an action nobody downstream can read, so its binding is
    /// still unresolved here.
    /// </returns>
    internal static ImmutableHashSet<ActionIdentity> Emit(
        SourceProductionContext context,
        Compilation compilation,
        OntologyActionCatalog catalog,
        ImmutableHashSet<string> localWorkflowNames)
    {
        var catalogId = string.IsNullOrEmpty(compilation.AssemblyName)
            ? "(unnamed assembly)"
            : compilation.AssemblyName!;

        var document = ProofCatalogDocument.FromLocalCatalog(catalogId, catalog, out var failures);

        // A contract that cannot travel only matters when it NEEDS to. An action
        // whose binding this compilation proves on its own was never going to be read
        // anywhere else, and reporting its unexportability would turn a local
        // guarantee into a build error for a boundary that does not exist.
        var boundLocally = catalog.Actions
            .Where(action => action.HasWorkflowBinding
                && action.BoundWorkflowName is { } name
                && localWorkflowNames.Contains(name))
            .Select(action => action.Identity)
            .ToImmutableHashSet();
        var needsExport = catalog.Actions
            .Where(action => action.HasWorkflowBinding)
            .Select(action => action.Identity)
            .ToImmutableHashSet()
            .Except(boundLocally);

        foreach (var (identity, reason) in failures.Where(failure => needsExport.Contains(failure.Identity)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                WorkflowDiagnostics.ProofCatalogExportIncomplete,
                Location.None,
                identity.ToString(),
                reason));
        }

        // Nothing to say is not the same as failing to say it. An assembly that
        // declares no action contract exports no catalog, and a consumer that finds
        // no attribute treats it as an assembly with nothing to contribute — which
        // is exactly true.
        //
        // The test is what this assembly DECLARED, not what survived projection. An
        // assembly whose every contract the gate refused still emits the attribute,
        // carrying an empty action list: it has nothing to contribute and says so.
        // Staying silent there would make absence mean two things at once, and the
        // ontology analyzer's binding-not-exported rule reads absence as "the workflow
        // generator never ran here" — which would then blame a generator that is
        // installed and did run, beside the export-incomplete diagnostic reported just
        // above, which already named the real fault.
        if (catalog.Actions.IsEmpty)
        {
            return ImmutableHashSet<ActionIdentity>.Empty;
        }

        var (_, json) = ProofCatalogWriter.WriteStamped(document);
        GeneratedCodeStamper.AddStampedSource(context, HintName, BuildSource(json));
        return document.Actions
            .Select(action => action.Identity)
            .ToImmutableHashSet();
    }

    /// <summary>
    /// Reads every referenced assembly's proof catalog, in a deterministic order.
    /// </summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>Each referenced assembly that carried a catalog, with the read result.</returns>
    /// <remarks>
    /// Ordered by assembly name so two builds of the same reference set produce the
    /// same diagnostics in the same order. Reference order is a build detail; a
    /// diagnostic that moved with it would be a diagnostic nobody could baseline.
    /// </remarks>
    internal static ImmutableArray<(string AssemblyName, ProofCatalogReadResult Result)> ReadReferenced(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var results = ImmutableArray.CreateBuilder<(string, ProofCatalogReadResult)>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols
            .OrderBy(symbol => symbol.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetCatalogJson(assembly, out var json))
            {
                continue;
            }

            results.Add((assembly.Name, ProofCatalogReader.Read(json)));
        }

        return results.ToImmutable();
    }

    private static bool TryGetCatalogJson(IAssemblySymbol assembly, out string json)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if (!string.Equals(attributeClass.Name, AttributeTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(
                attributeClass.ContainingNamespace?.ToDisplayString(),
                AttributeNamespace,
                StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is string value)
            {
                json = value;
                return true;
            }
        }

        json = string.Empty;
        return false;
    }

    private static string BuildSource(string json)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();

        // The assembly attribute must precede every other element in the file
        // (CS1730), so the catalog is applied first and the type it names is declared
        // below it. C# resolves the type regardless of file order.
        builder.Append("[assembly: global::")
            .Append(AttributeFullName)
            .Append('(')
            .Append(Quote(json))
            .AppendLine(")]");
        builder.AppendLine();
        builder.AppendLine($"namespace {AttributeNamespace}");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Carries this assembly's portable proof catalog (#204) so a referencing");
        builder.AppendLine("    /// compilation can prove a workflow binding whose contract is declared here.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <remarks>");
        builder.AppendLine("    /// Emitted per assembly and matched by full metadata name, not by type");
        builder.AppendLine("    /// identity: the two kinds of producing assembly reference different");
        builder.AppendLine("    /// Strategos packages, and no assembly is common to both.");
        builder.AppendLine("    /// </remarks>");
        builder.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Assembly, AllowMultiple = false)]");
        builder.AppendLine($"    internal sealed class {AttributeTypeName} : global::System.Attribute");
        builder.AppendLine("    {");
        builder.AppendLine("        /// <summary>Initializes a new instance carrying the catalog document.</summary>");
        builder.AppendLine("        /// <param name=\"catalog\">The ProofCatalogV1 JSON.</param>");
        builder.AppendLine($"        public {AttributeTypeName}(string catalog)");
        builder.AppendLine("        {");
        builder.AppendLine("            this.Catalog = catalog;");
        builder.AppendLine("        }");
        builder.AppendLine();
        builder.AppendLine("        /// <summary>Gets the ProofCatalogV1 JSON.</summary>");
        builder.AppendLine("        public string Catalog { get; }");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\r': builder.Append("\\r"); break;
                case '\n': builder.Append("\\n"); break;
                default: builder.Append(c); break;
            }
        }

        return builder.Append('"').ToString();
    }
}
