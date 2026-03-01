using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Strategos.Ontology.Generators.Analyzers;

namespace Strategos.Ontology.Generators
{
    /// <summary>
    /// Roslyn diagnostic analyzer for Strategos ontology definitions.
    /// Reports ONTO001-ONTO010 diagnostics at compile time.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class OntologyDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                OntologyDiagnostics.ONTO001_NoKey,
                OntologyDiagnostics.ONTO002_InvalidProperty,
                OntologyDiagnostics.ONTO003_UnknownDomain,
                OntologyDiagnostics.ONTO004_NoActions,
                OntologyDiagnostics.ONTO005_IncompatiblePropertyType,
                OntologyDiagnostics.ONTO006_NoConsumer,
                OntologyDiagnostics.ONTO007_DuplicateObjectType,
                OntologyDiagnostics.ONTO008_UndeclaredEventType,
                OntologyDiagnostics.ONTO009_UndeclaredLink,
                OntologyDiagnostics.ONTO010_NoEventStreamProvider);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            DomainOntologyAnalyzer.Register(context);
            PropertyAnalyzer.Register(context);
            InterfaceAnalyzer.Register(context);
            EventAnalyzer.Register(context);
            CrossDomainLinkAnalyzer.Register(context);
        }
    }
}
