using Microsoft.CodeAnalysis;

namespace Strategos.Ontology.Generators
{
    /// <summary>
    /// Static class containing all ontology diagnostic descriptors (ONTO001-ONTO010).
    /// </summary>
    public static class OntologyDiagnostics
    {
        private const string Category = "Strategos.Ontology";

        /// <summary>ONTO001: Object type has no Key() declaration.</summary>
        public static readonly DiagnosticDescriptor ONTO001_NoKey = new DiagnosticDescriptor(
            id: "ONTO001",
            title: "Object type has no Key() declaration",
            messageFormat: "Object type '{0}' has no Key() declaration",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>ONTO002: Property expression references non-existent member.</summary>
        public static readonly DiagnosticDescriptor ONTO002_InvalidProperty = new DiagnosticDescriptor(
            id: "ONTO002",
            title: "Property expression references non-existent member",
            messageFormat: "Property expression references non-existent member '{0}' on type '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>ONTO003: Cross-domain link references unknown domain.</summary>
        public static readonly DiagnosticDescriptor ONTO003_UnknownDomain = new DiagnosticDescriptor(
            id: "ONTO003",
            title: "Cross-domain link references unknown domain",
            messageFormat: "Cross-domain link '{0}' references unknown domain '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>ONTO004: Object type has no actions (pure data).</summary>
        public static readonly DiagnosticDescriptor ONTO004_NoActions = new DiagnosticDescriptor(
            id: "ONTO004",
            title: "Object type has no actions (pure data)",
            messageFormat: "Object type '{0}' has no actions defined (pure data type)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        /// <summary>ONTO005: Interface mapping references incompatible property types.</summary>
        public static readonly DiagnosticDescriptor ONTO005_IncompatiblePropertyType = new DiagnosticDescriptor(
            id: "ONTO005",
            title: "Interface mapping references incompatible property types",
            messageFormat: "Interface mapping between '{0}' and '{1}' has incompatible property types: '{2}' vs '{3}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>ONTO006: Workflow Produces&lt;T&gt; has no matching Consumes&lt;T&gt; consumer.</summary>
        public static readonly DiagnosticDescriptor ONTO006_NoConsumer = new DiagnosticDescriptor(
            id: "ONTO006",
            title: "Workflow Produces<T> has no matching Consumes<T> consumer",
            messageFormat: "Workflow produces '{0}' but no consumer with Consumes<{0}> was found",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>ONTO007: Duplicate object type registration in same domain.</summary>
        public static readonly DiagnosticDescriptor ONTO007_DuplicateObjectType = new DiagnosticDescriptor(
            id: "ONTO007",
            title: "Duplicate object type registration in same domain",
            messageFormat: "Object type '{0}' is registered more than once in the same domain",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>ONTO008: Event type not declared on any object type.</summary>
        public static readonly DiagnosticDescriptor ONTO008_UndeclaredEventType = new DiagnosticDescriptor(
            id: "ONTO008",
            title: "Event type not declared on any object type",
            messageFormat: "Event type '{0}' is not declared on any object type",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>ONTO009: Event MaterializesLink references undeclared link name.</summary>
        public static readonly DiagnosticDescriptor ONTO009_UndeclaredLink = new DiagnosticDescriptor(
            id: "ONTO009",
            title: "Event MaterializesLink references undeclared link name",
            messageFormat: "Event MaterializesLink references undeclared link '{0}' on type '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>ONTO010: Object type has events but no IEventStreamProvider registered.</summary>
        public static readonly DiagnosticDescriptor ONTO010_NoEventStreamProvider = new DiagnosticDescriptor(
            id: "ONTO010",
            title: "Object type has events but no IEventStreamProvider registered",
            messageFormat: "Object type '{0}' has events but no IEventStreamProvider is registered",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
