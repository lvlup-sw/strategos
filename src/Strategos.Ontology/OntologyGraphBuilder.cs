using System.Collections.Immutable;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology.Builder;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Diagnostics;
using Strategos.Ontology.Extensions;

namespace Strategos.Ontology;

public sealed class OntologyGraphBuilder
{
    private readonly List<DomainOntology> _domainOntologies = [];
    private readonly List<WorkflowMetadataBuilder> _workflowMetadata = [];
    private readonly List<IOntologySource> _sources = [];
    private ILogger<OntologyGraphBuilder> _logger = NullLogger<OntologyGraphBuilder>.Instance;
    private OntologyOptions? _options;

    /// <summary>
    /// DR-7 / DR-10: wires an <see cref="ILogger{T}"/> so graph-freeze
    /// warnings (AONT202, AONT203) and info (AONT204, AONT206) are
    /// surfaced via structured logging in addition to
    /// <see cref="OntologyGraph.NonFatalDiagnostics"/>.
    /// </summary>
    public OntologyGraphBuilder WithLogger(ILogger<OntologyGraphBuilder> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        return this;
    }

    /// <summary>
    /// DR-7 (Task 28): wires opt-in flags (e.g.
    /// <see cref="OntologyOptions.EnableHygieneHints"/> for AONT206).
    /// </summary>
    public OntologyGraphBuilder WithOptions(OntologyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    public OntologyGraphBuilder AddDomain<T>()
        where T : DomainOntology, new()
    {
        _domainOntologies.Add(new T());
        return this;
    }

    internal OntologyGraphBuilder AddDomain(DomainOntology domain)
    {
        _domainOntologies.Add(domain);
        return this;
    }

    internal OntologyGraphBuilder AddWorkflowMetadata(IEnumerable<WorkflowMetadataBuilder> metadata)
    {
        _workflowMetadata.AddRange(metadata);
        return this;
    }

    /// <summary>
    /// DR-3 (Task 13): registers <see cref="IOntologySource"/> instances
    /// to be drained at <see cref="Build"/> time. Each source's
    /// <c>LoadAsync</c> is iterated and emitted deltas applied to the
    /// matching per-domain builder. Sources contribute before composition
    /// so all subsequent validation observes a unified descriptor set.
    /// </summary>
    public OntologyGraphBuilder AddSources(IEnumerable<IOntologySource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        foreach (var source in sources)
        {
            if (source is null)
            {
                throw new ArgumentException(
                    "IOntologySource enumerable contains a null entry; sources must be non-null so the drain loop can attribute exceptions and deltas to a stable SourceId.",
                    nameof(sources));
            }

            _sources.Add(source);
        }

        return this;
    }

    public OntologyGraph Build() => Build(CancellationToken.None);

    /// <summary>
    /// Composes the immutable ontology graph, threading a cancellation
    /// token through the <see cref="IOntologySource"/> drain so a slow or
    /// hung source cannot block startup indefinitely. The hand-authored
    /// <c>DomainOntology.Define()</c> pass is purely in-memory and is not
    /// cancellation-aware; cancellation is checked between phases.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels the source-drain loop (and intermediate phases). On
    /// cancellation, a <see cref="OperationCanceledException"/> is thrown
    /// and no graph is produced.
    /// </param>
    public OntologyGraph Build(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var domains = new List<DomainDescriptor>();
        var allObjectTypes = new List<ObjectTypeDescriptor>();
        var allInterfaces = new List<InterfaceDescriptor>();
        var allCrossDomainLinkDescriptors = new List<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)>();

        // Per-domain builders are kept so source-emitted deltas land in
        // the same OntologyBuilder that the hand-authored Define() pass
        // populated. New domains contributed exclusively by sources get
        // their own builder created on demand below.
        var buildersByDomain = new Dictionary<string, OntologyBuilder>();

        foreach (var domainOntology in _domainOntologies)
        {
            var ontologyBuilder = new OntologyBuilder(domainOntology.DomainName);
            domainOntology.Build(ontologyBuilder);
            buildersByDomain[domainOntology.DomainName] = ontologyBuilder;
        }

        // DR-3 (Task 13): drain registered IOntologySource instances and
        // apply their deltas via the existing per-domain OntologyBuilder.
        // Runs after the hand-authored pass so hand descriptors are in
        // place when ingested deltas reference them (e.g. AddProperty
        // against a hand-defined ObjectType). Sources whose deltas
        // reference a domain unseen by the hand pass cause a new
        // per-domain builder to be created on demand.
        // DR-10 (Task 17): wrap each source's drain in try/catch so the
        // SourceId is surfaced in any propagated composition exception.
        DrainSources(buildersByDomain, cancellationToken);

        foreach (var domainOntology in _domainOntologies)
        {
            var ontologyBuilder = buildersByDomain[domainOntology.DomainName];

            var domainDescriptor = new DomainDescriptor(domainOntology.DomainName)
            {
                ObjectTypes = ontologyBuilder.ObjectTypes.ToArray(),
            };

            domains.Add(domainDescriptor);
            allObjectTypes.AddRange(ontologyBuilder.ObjectTypes);
            allInterfaces.AddRange(ontologyBuilder.Interfaces);

            foreach (var crossDomainLink in ontologyBuilder.CrossDomainLinks)
            {
                allCrossDomainLinkDescriptors.Add((domainOntology.DomainName, crossDomainLink));
            }
        }

        // Source-only domains (no DomainOntology subclass): fold into the
        // top-level lists so AONT040 et al. see their descriptors.
        foreach (var (domainName, ontologyBuilder) in buildersByDomain)
        {
            if (_domainOntologies.Any(d => d.DomainName == domainName))
            {
                continue; // already handled in the hand-authored loop above
            }

            var domainDescriptor = new DomainDescriptor(domainName)
            {
                ObjectTypes = ontologyBuilder.ObjectTypes.ToArray(),
            };

            domains.Add(domainDescriptor);
            allObjectTypes.AddRange(ontologyBuilder.ObjectTypes);
            allInterfaces.AddRange(ontologyBuilder.Interfaces);

            foreach (var crossDomainLink in ontologyBuilder.CrossDomainLinks)
            {
                allCrossDomainLinkDescriptors.Add((domainName, crossDomainLink));
            }
        }

        var domainLookup = domains.ToDictionary(d => d.DomainName);

        // AONT040 — DuplicateObjectTypeName (Track C1)
        // Enforce per-domain uniqueness of descriptor names with an explicit diagnostic,
        // before the ToDictionary call below would otherwise throw a cryptic ArgumentException.
        foreach (var group in allObjectTypes.GroupBy(ot => ot.DomainName))
        {
            var seen = new Dictionary<string, ObjectTypeDescriptor>();
            foreach (var descriptor in group)
            {
                if (seen.TryGetValue(descriptor.Name, out var existing))
                {
                    throw new OntologyCompositionException(
                        $"AONT040: Object type name '{descriptor.Name}' is registered twice in domain '{group.Key}'. " +
                        $"First registration: CLR type '{existing.ClrType?.FullName ?? existing.SymbolKey ?? "<unknown>"}'. " +
                        $"Second registration: CLR type '{descriptor.ClrType?.FullName ?? descriptor.SymbolKey ?? "<unknown>"}'. " +
                        $"Either remove one registration, or specify distinct names via Object<T>(\"name\", ...).");
                }

                seen[descriptor.Name] = descriptor;
            }
        }

        var objectTypeLookup = allObjectTypes
            .GroupBy(ot => ot.DomainName)
            .ToDictionary(g => g.Key, g => g.ToDictionary(ot => ot.Name));

        // Track C2 — reverse index from CLR type → descriptor names in registration order.
        // Built after the AONT040 check so callers can trust name uniqueness-per-domain,
        // and used by C3 below to detect multi-registered types in link positions.
        // DR-1 polyglot: descriptors with null ClrType (ingested-only) are excluded
        // from this CLR-keyed index; the (DomainName, Name)-keyed retarget arrives
        // with DR-8 (AONT041 retarget) in a later task.
        var namesByType = allObjectTypes
            .Where(ot => ot.ClrType is not null)
            .GroupBy(ot => ot.ClrType!)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(ot => ot.Name).ToList().AsReadOnly());

        // AONT041 must run before ResolveCrossDomainLinks: that resolver does a
        // first-wins ClrType match on (sourceDomain, ClrType) which would silently
        // bind a multi-registered source type to one descriptor without surfacing
        // the violation. Fail fast here so the diagnostic points at the original
        // multi-registration rather than a downstream "unresolvable" error.
        ValidateMultiRegisteredTypesNotInLinks(
            allObjectTypes, allCrossDomainLinkDescriptors, namesByType);

        var resolvedLinks = ResolveCrossDomainLinks(
            allCrossDomainLinkDescriptors, domainLookup, objectTypeLookup, allObjectTypes);

        ValidateIsAHierarchy(allObjectTypes);
        ValidateInterfaceImplementations(allObjectTypes, allInterfaces);
        ValidateInterfaceActionMappings(allObjectTypes, allInterfaces);
        ValidateLifecycles(allObjectTypes);
        ComputeTransitiveDerivationChains(allObjectTypes);
        InferPropertyKinds(allObjectTypes);
        ValidateInverseLinks(allObjectTypes);

        var warnings = new List<string>();
        MatchExtensionPoints(allObjectTypes, resolvedLinks, warnings);

        var workflowChains = BuildWorkflowChains(allObjectTypes, _workflowMetadata, warnings);

        // DR-7 + DR-10 (Tasks 23-30): graph-freeze AONT200-series checks.
        // Pre-merge ingested originals are collected from each per-domain
        // builder so hand vs ingested comparisons survive MergeTwo's
        // per-name union. Error-severity diagnostics aggregate into the
        // thrown exception; warning/info land on the returned graph and
        // are mirrored to the structured logger.
        var ingestedOriginals = new Dictionary<(string DomainName, string Name), ObjectTypeDescriptor>();
        foreach (var (_, builder) in buildersByDomain)
        {
            foreach (var kvp in builder.IngestedOriginals)
            {
                ingestedOriginals[kvp.Key] = kvp.Value;
            }
        }

        var fatalDiagnostics = ImmutableArray.CreateBuilder<OntologyDiagnostic>();
        var nonFatalDiagnostics = ImmutableArray.CreateBuilder<OntologyDiagnostic>();
        PerformGraphFreezeChecks(
            allObjectTypes,
            ingestedOriginals,
            fatalDiagnostics,
            nonFatalDiagnostics);

        if (fatalDiagnostics.Count > 0)
        {
            throw new OntologyCompositionException(
                fatalDiagnostics.ToImmutable(),
                nonFatalDiagnostics.ToImmutable());
        }

        return new OntologyGraph(
            domains: domains.ToArray(),
            objectTypes: allObjectTypes.ToArray(),
            interfaces: allInterfaces.ToArray(),
            crossDomainLinks: resolvedLinks.ToArray(),
            workflowChains: workflowChains.ToArray(),
            objectTypeNamesByType: namesByType,
            warnings: warnings.AsReadOnly(),
            nonFatalDiagnostics: nonFatalDiagnostics.ToImmutable());
    }

    /// <summary>
    /// DR-7 (Tasks 23-30): graph-freeze AONT200-series diagnostics.
    /// Iterates merged descriptors against their pre-merge ingested
    /// originals to detect hand-vs-ingested property drift,
    /// type/kind mismatches, missing references, language disagreements,
    /// and other invariants. Error-severity entries land in
    /// <paramref name="fatal"/>; warning + info entries land in
    /// <paramref name="nonFatal"/> and are mirrored to the structured
    /// logger so telemetry pipelines see them even when the build
    /// succeeds.
    /// </summary>
    private void PerformGraphFreezeChecks(
        List<ObjectTypeDescriptor> allObjectTypes,
        IReadOnlyDictionary<(string DomainName, string Name), ObjectTypeDescriptor> ingestedOriginals,
        ImmutableArray<OntologyDiagnostic>.Builder fatal,
        ImmutableArray<OntologyDiagnostic>.Builder nonFatal)
    {
        // AONT201 — hand-declared property missing from ingested
        // descriptor (after MergeTwo). Inspect the merged descriptor for
        // hand-tagged properties and confirm each appears on the
        // pre-merge ingested original. Skip descriptors that never had
        // an ingested contribution (hand-only).
        // AONT202 — hand-declared property type/kind mismatches ingested
        // contribution (same name). Warning-severity; surfaces on the
        // returned graph and is mirrored to the structured logger.
        foreach (var descriptor in allObjectTypes)
        {
            if (!ingestedOriginals.TryGetValue((descriptor.DomainName, descriptor.Name), out var ingested))
            {
                continue;
            }

            var ingestedByName = ingested.Properties.ToDictionary(p => p.Name, StringComparer.Ordinal);

            foreach (var property in descriptor.Properties)
            {
                if (property.Source != DescriptorSource.HandAuthored)
                {
                    continue;
                }

                if (!ingestedByName.TryGetValue(property.Name, out var ingestedProp))
                {
                    fatal.Add(new OntologyDiagnostic(
                        Id: "AONT201",
                        Message:
                            $"AONT201: hand-declared property '{property.Name}' on "
                            + $"'{descriptor.DomainName}.{descriptor.Name}' is missing from the "
                            + $"ingested descriptor. Pass-6b rename matcher may have missed this — "
                            + $"verify the property name on the ingested side.",
                        Severity: OntologyDiagnosticSeverity.Error,
                        DomainName: descriptor.DomainName,
                        TypeName: descriptor.Name,
                        PropertyName: property.Name));
                    continue;
                }

                // AONT202 — type/kind mismatch on a hand-vs-ingested
                // property with the same name. Kind disagreement is
                // sufficient (e.g. hand Scalar vs ingested Reference);
                // a raw PropertyType inequality also trips the warning
                // because ingested-side types may not be loadable as CLR
                // (ReferenceSymbolKey carries the truth) but a non-null
                // PropertyType disagreement is still meaningful drift.
                // AONT206 — opt-in hygiene hint: the property is
                // declared on both sides. Fires only when the consumer
                // has set OntologyOptions.EnableHygieneHints (MSBuild
                // property OntologyEnableHygieneHints). Always emitted
                // alongside any AONT202 mismatch (the two are
                // complementary: 202 says "they disagree", 206 says
                // "you may not need both").
                if (_options is { EnableHygieneHints: true })
                {
                    var hint = new OntologyDiagnostic(
                        Id: "AONT206",
                        Message:
                            $"AONT206: property '{property.Name}' on "
                            + $"'{descriptor.DomainName}.{descriptor.Name}' is declared in hand "
                            + "Define() and also contributed by the ingested side — consider "
                            + "removing the redundant hand declaration.",
                        Severity: OntologyDiagnosticSeverity.Info,
                        DomainName: descriptor.DomainName,
                        TypeName: descriptor.Name,
                        PropertyName: property.Name);

                    nonFatal.Add(hint);
                    LogNonFatal(hint);
                }

                if (property.Kind != ingestedProp.Kind || property.PropertyType != ingestedProp.PropertyType)
                {
                    var diag = new OntologyDiagnostic(
                        Id: "AONT202",
                        Message:
                            $"AONT202: property '{property.Name}' on "
                            + $"'{descriptor.DomainName}.{descriptor.Name}' has hand-declared "
                            + $"type/kind ({property.PropertyType.Name}/{property.Kind}) that "
                            + $"mismatches the ingested side "
                            + $"({ingestedProp.PropertyType.Name}/{ingestedProp.Kind}).",
                        Severity: OntologyDiagnosticSeverity.Warning,
                        DomainName: descriptor.DomainName,
                        TypeName: descriptor.Name,
                        PropertyName: property.Name);

                    nonFatal.Add(diag);
                    LogNonFatal(diag);
                }
            }

            // AONT203 fold — Strict-mode opt-in: ingested-only properties
            // missing from hand Define() emit warning AONT203.
            if (IsStrictDomainEntity(descriptor))
            {
                var handPropNames = new HashSet<string>(
                    descriptor.Properties
                        .Where(p => p.Source == DescriptorSource.HandAuthored)
                        .Select(p => p.Name),
                    StringComparer.Ordinal);

                foreach (var ingestedProp in ingested.Properties)
                {
                    if (handPropNames.Contains(ingestedProp.Name))
                    {
                        continue;
                    }

                    var diag = new OntologyDiagnostic(
                        Id: "AONT203",
                        Message:
                            $"AONT203: property '{ingestedProp.Name}' is present on the "
                            + $"ingested descriptor of '{descriptor.DomainName}.{descriptor.Name}' "
                            + $"but not declared in hand Define(); type is marked "
                            + $"[DomainEntity(Strict = true)].",
                        Severity: OntologyDiagnosticSeverity.Warning,
                        DomainName: descriptor.DomainName,
                        TypeName: descriptor.Name,
                        PropertyName: ingestedProp.Name);

                    nonFatal.Add(diag);
                    LogNonFatal(diag);
                }
            }
        }

        // AONT208 — LanguageId disagreement between MergeTwo origins.
        // After merge, the descriptor's LanguageId mirrors the hand-side
        // value. We compare that against the pre-merge ingested
        // original. The diagnostic fires only when the hand-side opted
        // into a non-default LanguageId (i.e. value != "dotnet"); the
        // common dotnet/typescript polyglot path is the expected
        // composition mode and does not trip.
        foreach (var descriptor in allObjectTypes)
        {
            if (!ingestedOriginals.TryGetValue((descriptor.DomainName, descriptor.Name), out var ingested))
            {
                continue;
            }

            var handLanguage = descriptor.LanguageId;
            var ingestedLanguage = ingested.LanguageId;

            if (string.Equals(handLanguage, "dotnet", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(handLanguage, ingestedLanguage, StringComparison.Ordinal))
            {
                continue;
            }

            fatal.Add(new OntologyDiagnostic(
                Id: "AONT208",
                Message:
                    $"AONT208: descriptor '{descriptor.DomainName}.{descriptor.Name}' has "
                    + $"LanguageId disagreement between hand ('{handLanguage}') and ingested "
                    + $"('{ingestedLanguage}') contributions.",
                Severity: OntologyDiagnosticSeverity.Error,
                DomainName: descriptor.DomainName,
                TypeName: descriptor.Name,
                PropertyName: null));
        }

        // AONT205 (defensive freeze-time surface) — Task 16 catches the
        // common case at delta-apply, but a stress scenario where two
        // ingested sources race intent fields could land a defective
        // descriptor in the merged graph. Any descriptor with
        // Source = Ingested that carries non-empty Actions/Events or a
        // Lifecycle survives this check as an aggregated error.
        foreach (var descriptor in allObjectTypes)
        {
            if (descriptor.Source != DescriptorSource.Ingested)
            {
                continue;
            }

            string? offending = null;
            if (descriptor.Actions.Count > 0)
            {
                offending = "Actions";
            }
            else if (descriptor.Events.Count > 0)
            {
                offending = "Events";
            }
            else if (descriptor.Lifecycle is not null)
            {
                offending = "Lifecycle";
            }

            if (offending is null)
            {
                continue;
            }

            fatal.Add(new OntologyDiagnostic(
                Id: "AONT205",
                Message:
                    $"AONT205: ingested descriptor '{descriptor.DomainName}.{descriptor.Name}' "
                    + $"contributes to intent-only field '{offending}'. Mechanical ingesters must "
                    + "leave Actions, Events, and Lifecycle empty — those are hand-authored intent.",
                Severity: OntologyDiagnosticSeverity.Error,
                DomainName: descriptor.DomainName,
                TypeName: descriptor.Name,
                PropertyName: offending));
        }

        // AONT204 — info-severity hint when a purely ingested descriptor
        // (Source = Ingested at graph-level) is not referenced by any
        // hand-authored descriptor via Links.TargetTypeName,
        // ParentTypeName, or KeyProperty links. Orphan ingested types
        // often indicate a misconfigured ingester or unused contribution
        // worth pruning.
        var handReferencedNames = CollectHandReferencedTypeNames(allObjectTypes);
        foreach (var descriptor in allObjectTypes)
        {
            if (descriptor.Source != DescriptorSource.Ingested)
            {
                continue;
            }

            if (handReferencedNames.Contains((descriptor.DomainName, descriptor.Name)))
            {
                continue;
            }

            // Also consider domain-agnostic name references — the hand
            // link DSL may resolve to a descriptor with the same simple
            // name in another domain (rare, but the resolver does fall
            // back to a name-only scan).
            if (handReferencedNames.Any(k => k.Name == descriptor.Name))
            {
                continue;
            }

            var diag = new OntologyDiagnostic(
                Id: "AONT204",
                Message:
                    $"AONT204: ingested-only descriptor '{descriptor.DomainName}.{descriptor.Name}' "
                    + "is not referenced by any hand-authored type (no Links, ParentType, "
                    + "or KeyProperty references found).",
                Severity: OntologyDiagnosticSeverity.Info,
                DomainName: descriptor.DomainName,
                TypeName: descriptor.Name,
                PropertyName: null);

            nonFatal.Add(diag);
            LogNonFatal(diag);
        }
    }

    /// <summary>
    /// Collects every (DomainName, TargetName) pair that a hand-authored
    /// descriptor references via Links, ParentTypeName, or a property's
    /// ReferenceSymbolKey. Used by AONT204 to detect orphan ingested
    /// descriptors. Same-domain link targets are recorded under the
    /// hand descriptor's own domain; an empty string is used for the
    /// domain slot when the reference is domain-agnostic.
    /// </summary>
    private static HashSet<(string DomainName, string Name)> CollectHandReferencedTypeNames(
        List<ObjectTypeDescriptor> allObjectTypes)
    {
        var refs = new HashSet<(string, string)>();

        foreach (var descriptor in allObjectTypes)
        {
            if (descriptor.Source != DescriptorSource.HandAuthored)
            {
                continue;
            }

            if (descriptor.ParentTypeName is not null)
            {
                refs.Add((descriptor.DomainName, descriptor.ParentTypeName));
            }

            foreach (var link in descriptor.Links)
            {
                // Hand-tagged links (or any link declared by a hand
                // descriptor) point at TargetTypeName in the same domain
                // unless the source is genuinely ingested-only on this
                // descriptor — which is impossible here because we
                // already gated on descriptor.Source.
                refs.Add((descriptor.DomainName, link.TargetTypeName));
            }
        }

        return refs;
    }

    /// <summary>
    /// Reads <see cref="DomainEntityAttribute"/> off a descriptor's
    /// <see cref="ObjectTypeDescriptor.ClrType"/> and reports whether
    /// the type opts in to strict mode. Returns <c>false</c> for
    /// ingested-only descriptors (no CLR type) — strictness is a
    /// hand-side opt-in by design.
    /// </summary>
    private static bool IsStrictDomainEntity(ObjectTypeDescriptor descriptor)
    {
        if (descriptor.ClrType is null)
        {
            return false;
        }

        var attr = (DomainEntityAttribute?)Attribute.GetCustomAttribute(
            descriptor.ClrType,
            typeof(DomainEntityAttribute));

        return attr is { Strict: true };
    }

    /// <summary>
    /// Routes a non-fatal diagnostic through the wired structured logger.
    /// Warnings use <c>LogWarning</c>; info uses <c>LogInformation</c>.
    /// Each call carries structured properties
    /// <c>{DiagnosticId, DomainName, TypeName, PropertyName}</c> so log
    /// pipelines can filter by diagnostic id.
    /// </summary>
    private void LogNonFatal(OntologyDiagnostic diagnostic)
    {
        if (diagnostic.Severity == OntologyDiagnosticSeverity.Warning)
        {
#pragma warning disable CA2254 // Template should be a static expression
            _logger.LogWarning(
                "{DiagnosticId}: {Message} ({DomainName}.{TypeName}.{PropertyName})",
                diagnostic.Id,
                diagnostic.Message,
                diagnostic.DomainName,
                diagnostic.TypeName,
                diagnostic.PropertyName);
#pragma warning restore CA2254
            return;
        }

        if (diagnostic.Severity == OntologyDiagnosticSeverity.Info)
        {
#pragma warning disable CA2254
            _logger.LogInformation(
                "{DiagnosticId}: {Message} ({DomainName}.{TypeName}.{PropertyName})",
                diagnostic.Id,
                diagnostic.Message,
                diagnostic.DomainName,
                diagnostic.TypeName,
                diagnostic.PropertyName);
#pragma warning restore CA2254
        }
    }

    private static List<ResolvedCrossDomainLink> ResolveCrossDomainLinks(
        List<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)> linkDescriptors,
        Dictionary<string, DomainDescriptor> domainLookup,
        Dictionary<string, Dictionary<string, ObjectTypeDescriptor>> objectTypeLookup,
        List<ObjectTypeDescriptor> allObjectTypes)
    {
        var resolved = new List<ResolvedCrossDomainLink>();

        foreach (var (sourceDomain, descriptor) in linkDescriptors)
        {
            if (!domainLookup.ContainsKey(descriptor.TargetDomain))
            {
                throw new OntologyCompositionException(
                    $"Cross-domain link '{descriptor.Name}' references unresolvable domain '{descriptor.TargetDomain}'.");
            }

            if (!objectTypeLookup.TryGetValue(descriptor.TargetDomain, out var targetDomainTypes)
                || !targetDomainTypes.TryGetValue(descriptor.TargetTypeName, out var targetObjectType))
            {
                throw new OntologyCompositionException(
                    $"Cross-domain link '{descriptor.Name}' references unresolvable object type '{descriptor.TargetTypeName}' in domain '{descriptor.TargetDomain}'.");
            }

            var sourceObjectType = allObjectTypes.FirstOrDefault(
                ot => ot.DomainName == sourceDomain && ot.ClrType == descriptor.SourceType);

            if (sourceObjectType is null)
            {
                throw new OntologyCompositionException(
                    $"Cross-domain link '{descriptor.Name}' references unresolvable source type '{descriptor.SourceType.Name}' in domain '{sourceDomain}'.");
            }

            resolved.Add(new ResolvedCrossDomainLink(
                Name: descriptor.Name,
                SourceDomain: sourceDomain,
                SourceObjectType: sourceObjectType,
                TargetDomain: descriptor.TargetDomain,
                TargetObjectType: targetObjectType,
                Cardinality: descriptor.Cardinality,
                EdgeProperties: descriptor.EdgeProperties,
                Description: descriptor.Description));
        }

        return resolved;
    }

    private static void MatchExtensionPoints(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<ResolvedCrossDomainLink> resolvedLinks,
        List<string> warnings)
    {
        for (var i = 0; i < allObjectTypes.Count; i++)
        {
            var objectType = allObjectTypes[i];
            if (objectType.ExternalLinkExtensionPoints.Count == 0)
            {
                continue;
            }

            // Find incoming cross-domain links targeting this object type
            var incomingLinks = resolvedLinks
                .Where(l => l.TargetObjectType.Name == objectType.Name
                         && l.TargetDomain == objectType.DomainName)
                .ToList();

            var updatedExtensionPoints = new List<ExternalLinkExtensionPoint>();

            foreach (var extensionPoint in objectType.ExternalLinkExtensionPoints)
            {
                var matchedNames = new List<string>();

                foreach (var link in incomingLinks)
                {
                    var isMatch = true;

                    // Check domain constraint
                    if (extensionPoint.RequiredSourceDomain is not null
                        && link.SourceDomain != extensionPoint.RequiredSourceDomain)
                    {
                        isMatch = false;
                    }

                    // Check interface constraint
                    if (isMatch && extensionPoint.RequiredSourceInterface is not null)
                    {
                        var sourceImplementsInterface = link.SourceObjectType.ImplementedInterfaces
                            .Any(iface => iface.Name == extensionPoint.RequiredSourceInterface
                                       || iface.InterfaceType.Name == extensionPoint.RequiredSourceInterface);
                        if (!sourceImplementsInterface)
                        {
                            isMatch = false;
                            warnings.Add(
                                $"Cross-domain link '{link.Name}' targets extension point '{extensionPoint.Name}' on '{objectType.Name}' but source type '{link.SourceObjectType.Name}' does not implement required interface '{extensionPoint.RequiredSourceInterface}'.");
                        }
                    }

                    // Check edge property constraints
                    if (isMatch)
                    {
                        foreach (var requiredEdgeProp in extensionPoint.RequiredEdgeProperties)
                        {
                            var hasEdgeProp = link.EdgeProperties
                                .Any(ep => ep.Name == requiredEdgeProp.Name);
                            if (!hasEdgeProp)
                            {
                                warnings.Add(
                                    $"Cross-domain link '{link.Name}' matched extension point '{extensionPoint.Name}' on '{objectType.Name}' but is missing required edge property '{requiredEdgeProp.Name}'.");
                            }
                        }
                    }

                    if (isMatch)
                    {
                        matchedNames.Add(link.Name);
                    }
                }

                updatedExtensionPoints.Add(extensionPoint with
                {
                    MatchedLinkNames = matchedNames.AsReadOnly(),
                });
            }

            allObjectTypes[i] = objectType with
            {
                ExternalLinkExtensionPoints = updatedExtensionPoints.AsReadOnly(),
            };
        }
    }

    private static void ValidateIsAHierarchy(List<ObjectTypeDescriptor> allObjectTypes)
    {
        // Keys are (DomainName, Name) so that two domains can legally host descriptors
        // that share a simple name (enforced by the AONT040 per-domain check above).
        var typesByKey = allObjectTypes.ToDictionary(ot => (ot.DomainName, ot.Name));

        foreach (var objectType in allObjectTypes)
        {
            if (objectType.ParentTypeName is null)
            {
                continue;
            }

            if (!typesByKey.ContainsKey((objectType.DomainName, objectType.ParentTypeName)))
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' declares IS-A relationship with unregistered parent type '{objectType.ParentTypeName}'.");
            }
        }

        // Detect cycles using DFS (domain-scoped)
        foreach (var objectType in allObjectTypes)
        {
            if (objectType.ParentTypeName is null)
            {
                continue;
            }

            var visited = new HashSet<string>();
            var currentName = objectType.Name;
            var currentDomain = objectType.DomainName;

            while (currentName is not null)
            {
                if (!visited.Add(currentName))
                {
                    throw new OntologyCompositionException(
                        $"IS-A hierarchy cycle detected involving type '{currentName}'.");
                }

                if (typesByKey.TryGetValue((currentDomain, currentName), out var currentType))
                {
                    currentName = currentType.ParentTypeName;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private static void ValidateInterfaceImplementations(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<InterfaceDescriptor> allInterfaces)
    {
        var interfaceByName = allInterfaces
            .GroupBy(i => i.Name)
            .ToDictionary(g => g.Key, g => g.First());

        var interfaceByType = allInterfaces
            .GroupBy(i => i.InterfaceType)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var objectType in allObjectTypes)
        {
            foreach (var implementedInterface in objectType.ImplementedInterfaces)
            {
                // Try name-based lookup first, then fall back to type-based
                if (!interfaceByName.TryGetValue(implementedInterface.Name, out var interfaceDescriptor))
                {
                    if (!interfaceByType.TryGetValue(implementedInterface.InterfaceType, out interfaceDescriptor))
                    {
                        continue;
                    }
                }

                var objectPropertyLookup = objectType.Properties
                    .ToDictionary(p => p.Name, p => p.PropertyType);

                // Also include the key property in the lookup (keys are valid interface targets)
                if (objectType.KeyProperty is not null)
                {
                    objectPropertyLookup.TryAdd(objectType.KeyProperty.Name, objectType.KeyProperty.PropertyType);
                }

                // Build a set of interface property names covered by Via() mappings
                var viaMappedTargets = objectType.InterfacePropertyMappings
                    .Where(m => m.InterfaceName == implementedInterface.Name)
                    .ToDictionary(m => m.TargetPropertyName, m => m.SourcePropertyName);

                foreach (var interfaceProperty in interfaceDescriptor.Properties)
                {
                    // Check direct name match first
                    if (objectPropertyLookup.TryGetValue(interfaceProperty.Name, out var objectPropertyType))
                    {
                        if (!interfaceProperty.PropertyType.IsAssignableFrom(objectPropertyType))
                        {
                            throw new OntologyCompositionException(
                                $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but property '{interfaceProperty.Name}' has incompatible type. Expected '{interfaceProperty.PropertyType.Name}', found '{objectPropertyType.Name}'.");
                        }

                        continue;
                    }

                    // Check Via() mapping
                    if (viaMappedTargets.TryGetValue(interfaceProperty.Name, out var sourcePropName))
                    {
                        if (objectPropertyLookup.TryGetValue(sourcePropName, out var sourcePropertyType))
                        {
                            if (!interfaceProperty.PropertyType.IsAssignableFrom(sourcePropertyType))
                            {
                                throw new OntologyCompositionException(
                                    $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but Via() mapped property '{sourcePropName}' has incompatible type for interface property '{interfaceProperty.Name}'. Expected '{interfaceProperty.PropertyType.Name}', found '{sourcePropertyType.Name}'.");
                            }

                            continue;
                        }
                    }

                    throw new OntologyCompositionException(
                        $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but is missing property '{interfaceProperty.Name}'.");
                }
            }
        }
    }

    private static void ValidateInterfaceActionMappings(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<InterfaceDescriptor> allInterfaces)
    {
        // Build lookup by InterfaceType since ObjectTypeBuilder uses typeof(TInterface).Name
        // while InterfaceBuilder uses the user-provided name
        var interfaceByType = allInterfaces
            .GroupBy(i => i.InterfaceType)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var objectType in allObjectTypes)
        {
            foreach (var implementedInterface in objectType.ImplementedInterfaces)
            {
                if (!interfaceByType.TryGetValue(implementedInterface.InterfaceType, out var interfaceDescriptor))
                {
                    continue;
                }

                if (interfaceDescriptor.Actions.Count == 0)
                {
                    continue;
                }

                var mappedActionNames = objectType.InterfaceActionMappings
                    .Select(m => m.InterfaceActionName)
                    .ToHashSet();

                foreach (var interfaceAction in interfaceDescriptor.Actions)
                {
                    if (!mappedActionNames.Contains(interfaceAction.Name))
                    {
                        throw new OntologyCompositionException(
                            $"Object type '{objectType.Name}' implements interface '{implementedInterface.Name}' but does not map interface action '{interfaceAction.Name}'. Use ActionVia() or ActionDefault() in the Implements<> mapping.");
                    }
                }
            }
        }
    }

    private static void ValidateLifecycles(List<ObjectTypeDescriptor> allObjectTypes)
    {
        foreach (var objectType in allObjectTypes)
        {
            if (objectType.Lifecycle is null)
            {
                continue;
            }

            var lifecycle = objectType.Lifecycle;

            // Exactly one initial state
            var initialStates = lifecycle.States.Count(s => s.IsInitial);
            if (initialStates != 1)
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' lifecycle must have exactly 1 initial state, found {initialStates}.");
            }

            // At least one terminal state
            var terminalStates = lifecycle.States.Count(s => s.IsTerminal);
            if (terminalStates < 1)
            {
                throw new OntologyCompositionException(
                    $"Object type '{objectType.Name}' lifecycle must have at least 1 terminal state, found 0.");
            }

            // Validate transition state references
            var stateNames = lifecycle.States.Select(s => s.Name).ToHashSet();
            foreach (var transition in lifecycle.Transitions)
            {
                if (!stateNames.Contains(transition.FromState))
                {
                    throw new OntologyCompositionException(
                        $"Object type '{objectType.Name}' lifecycle transition references undeclared state '{transition.FromState}'.");
                }

                if (!stateNames.Contains(transition.ToState))
                {
                    throw new OntologyCompositionException(
                        $"Object type '{objectType.Name}' lifecycle transition references undeclared state '{transition.ToState}'.");
                }
            }
        }
    }

    private static void ComputeTransitiveDerivationChains(List<ObjectTypeDescriptor> allObjectTypes)
    {
        for (var i = 0; i < allObjectTypes.Count; i++)
        {
            var objectType = allObjectTypes[i];
            var computedProperties = objectType.Properties
                .Where(p => p.IsComputed && p.DerivedFrom.Count > 0)
                .ToList();

            if (computedProperties.Count == 0)
            {
                continue;
            }

            var propertyLookup = objectType.Properties.ToDictionary(p => p.Name);
            var updatedProperties = new List<PropertyDescriptor>();

            foreach (var prop in objectType.Properties)
            {
                if (!prop.IsComputed || prop.DerivedFrom.Count == 0)
                {
                    updatedProperties.Add(prop);
                    continue;
                }

                var transitive = new HashSet<DerivationSource>();
                var visited = new HashSet<string>();
                ComputeTransitiveSources(prop.Name, propertyLookup, transitive, visited);

                updatedProperties.Add(prop with
                {
                    TransitiveDerivedFrom = transitive.ToList().AsReadOnly(),
                });
            }

            allObjectTypes[i] = objectType with
            {
                Properties = updatedProperties.AsReadOnly(),
            };
        }
    }

    private static void ComputeTransitiveSources(
        string propertyName,
        Dictionary<string, PropertyDescriptor> propertyLookup,
        HashSet<DerivationSource> transitive,
        HashSet<string> visited)
    {
        if (!visited.Add(propertyName))
        {
            throw new OntologyCompositionException(
                $"Derivation cycle detected involving property '{propertyName}'.");
        }

        if (!propertyLookup.TryGetValue(propertyName, out var prop))
        {
            return;
        }

        foreach (var source in prop.DerivedFrom)
        {
            transitive.Add(source);

            if (source.Kind == Descriptors.DerivationSourceKind.Local && source.PropertyName is not null)
            {
                ComputeTransitiveSources(source.PropertyName, propertyLookup, transitive, visited);
            }
        }

        visited.Remove(propertyName);
    }

    private static void ValidateInverseLinks(List<ObjectTypeDescriptor> allObjectTypes)
    {
        // Keyed by (DomainName, Name) because descriptor names are unique only within a
        // domain — cross-domain name collisions are legal under the AONT040 invariant.
        var typesByKey = allObjectTypes.ToDictionary(ot => (ot.DomainName, ot.Name));

        foreach (var objectType in allObjectTypes)
        {
            foreach (var link in objectType.Links)
            {
                if (link.InverseLinkName is null)
                {
                    continue;
                }

                if (!typesByKey.TryGetValue((objectType.DomainName, link.TargetTypeName), out var targetType))
                {
                    continue; // Target type not found; other validations handle this
                }

                var inverseLink = targetType.Links.FirstOrDefault(l => l.Name == link.InverseLinkName);
                if (inverseLink is null)
                {
                    throw new OntologyCompositionException(
                        $"Link '{link.Name}' on '{objectType.Name}' declares inverse '{link.InverseLinkName}' but target type '{link.TargetTypeName}' has no link named '{link.InverseLinkName}'.");
                }

                // If the inverse link also declares an inverse, verify symmetry
                if (inverseLink.InverseLinkName is not null && inverseLink.InverseLinkName != link.Name)
                {
                    throw new OntologyCompositionException(
                        $"Asymmetric inverse declaration: '{objectType.Name}.{link.Name}' declares inverse '{link.InverseLinkName}', but '{link.TargetTypeName}.{link.InverseLinkName}' declares inverse '{inverseLink.InverseLinkName}' instead of '{link.Name}'.");
                }
            }
        }
    }

    /// <summary>
    /// AONT041 — MultiRegisteredTypeInLink (Track C3).
    /// Enforces the Option X freeze invariant that a descriptor identity (CLR type or
    /// SCIP SymbolKey) registered under more than one descriptor name cannot participate
    /// in structural links — neither as a link target nor as a link source, and neither
    /// in intra-domain <see cref="LinkDescriptor"/>s nor in <see cref="CrossDomainLinkDescriptor"/>s.
    /// The Basileus happy path is a multi-registered leaf type with no links anywhere —
    /// that remains legal.
    /// </summary>
    /// <remarks>
    /// DR-8 (Task 31) retarget: the lookup is now polyglot-aware. The "identity" of a
    /// descriptor is its <see cref="ObjectTypeDescriptor.ClrType"/> when non-null, else
    /// its <see cref="ObjectTypeDescriptor.SymbolKey"/>. Link targets resolve by
    /// <c>(DomainName, Name)</c> directly — the previous CLR-simple-name pre-filter
    /// produced false negatives for ingested-only descriptors. Cross-domain links keep
    /// their CLR source-type wiring because the From&lt;T&gt;() builder requires a CLR
    /// type, but the target-side scan is now polyglot.
    /// </remarks>
    private static void ValidateMultiRegisteredTypesNotInLinks(
        IReadOnlyList<ObjectTypeDescriptor> allObjectTypes,
        IReadOnlyList<(string SourceDomain, CrossDomainLinkDescriptor Descriptor)> crossDomainLinks,
        IReadOnlyDictionary<Type, IReadOnlyList<string>> namesByType)
    {
        // Polyglot identity reverse index: identity (ClrType-or-SymbolKey, as object) →
        // list of (DomainName, Name) registrations. A descriptor is "multi-registered"
        // when its identity bucket holds more than one entry. Ingested-only descriptors
        // (ClrType == null) participate via SymbolKey identity; descriptors with neither
        // a ClrType nor a SymbolKey are skipped — there is nothing to key against.
        var identityByDescriptor = new Dictionary<(string DomainName, string Name), object>();
        var registrationsByIdentity = new Dictionary<object, List<(string DomainName, string Name)>>();
        foreach (var ot in allObjectTypes)
        {
            object? identity = ot.ClrType is not null ? ot.ClrType : ot.SymbolKey;
            if (identity is null)
            {
                continue;
            }

            identityByDescriptor[(ot.DomainName, ot.Name)] = identity;
            if (!registrationsByIdentity.TryGetValue(identity, out var list))
            {
                list = new List<(string DomainName, string Name)>();
                registrationsByIdentity[identity] = list;
            }

            list.Add((ot.DomainName, ot.Name));
        }

        // Direct (DomainName, Name) → descriptor index. Replaces the prior
        // (DomainName, ClrSimpleName) map and covers polyglot descriptors. Includes
        // every descriptor regardless of whether ClrType is set.
        var descriptorByDomainAndName = allObjectTypes
            .GroupBy(ot => (ot.DomainName, ot.Name))
            .ToDictionary(g => g.Key, g => g.First());

        // CLR-simple-name fallback: HasMany<TLinked>(name) writes typeof(TLinked).Name
        // into LinkDescriptor.TargetTypeName, which need not match any registered
        // descriptor name when the underlying type was registered under one or more
        // explicit names (e.g., "orders" / "open_orders" for TradeOrder). To preserve
        // the legacy multi-registered-CLR-type-as-link-target diagnostic we keep a
        // sidecar index from CLR simple name → registered CLR type for types whose
        // identity bucket holds more than one (Domain, Name) registration.
        var multiRegisteredClrByName = namesByType
            .Where(kvp => kvp.Value.Count > 1)
            .GroupBy(kvp => kvp.Key.Name)
            .ToDictionary(g => g.Key, g => g.First().Key);

        // Sidecar index for the explicit-name sub-rule: a link whose TargetTypeName is a
        // CLR simple name that resolves to a descriptor registered under an *explicit*
        // (non-default) name. The descriptor identity is keyed under its registered name
        // in descriptorByDomainAndName above, but the link target — which carries the CLR
        // simple name from HasMany<TLinked> — needs (DomainName, ClrSimpleName) to find
        // it. Only descriptors with a non-null ClrType participate.
        var descriptorByDomainAndClrSimpleName = allObjectTypes
            .Where(ot => ot.ClrType is not null)
            .GroupBy(ot => (ot.DomainName, ot.ClrType!.Name))
            .ToDictionary(g => g.Key, g => g.First());

        bool IsMultiRegistered(string domain, string name)
        {
            return identityByDescriptor.TryGetValue((domain, name), out var identity)
                && registrationsByIdentity.TryGetValue(identity, out var regs)
                && regs.Count > 1;
        }

        IReadOnlyList<(string DomainName, string Name)> RegistrationsFor(string domain, string name)
        {
            if (identityByDescriptor.TryGetValue((domain, name), out var identity)
                && registrationsByIdentity.TryGetValue(identity, out var regs))
            {
                return regs;
            }

            return Array.Empty<(string, string)>();
        }

        foreach (var descriptor in allObjectTypes)
        {
            // Check: does this descriptor declare an outgoing link whose target resolves
            // (within the same domain) to a multi-registered identity? Intra-domain links
            // identify their target by simple name, so we resolve (descriptor.DomainName,
            // link.TargetTypeName) and consult the polyglot identity index.
            foreach (var link in descriptor.Links)
            {
                // CLR-simple-name fallback first: when no descriptor is registered under
                // link.TargetTypeName in this domain, but the CLR simple name matches a
                // multi-registered CLR type, the HasMany<TLinked>() target would resolve
                // ambiguously at runtime — surface AONT041 instead.
                if (!descriptorByDomainAndName.ContainsKey((descriptor.DomainName, link.TargetTypeName))
                    && multiRegisteredClrByName.TryGetValue(link.TargetTypeName, out var multiClrType))
                {
                    var names = namesByType[multiClrType];
                    throw new OntologyCompositionException(
                        $"AONT041: CLR type '{multiClrType.FullName}' has multiple registrations " +
                        $"({string.Join(", ", names.Select(n => $"'{n}'"))}) but is also referenced as a link target " +
                        $"in '{descriptor.Name}.{link.Name}'. Multi-registered types cannot participate in structural " +
                        $"links. See #32 for a future relaxation path.");
                }

                if (descriptorByDomainAndName.TryGetValue(
                        (descriptor.DomainName, link.TargetTypeName), out var targetDescriptor)
                    && IsMultiRegistered(targetDescriptor.DomainName, targetDescriptor.Name))
                {
                    var regs = RegistrationsFor(targetDescriptor.DomainName, targetDescriptor.Name);
                    var identityLabel = targetDescriptor.ClrType?.FullName
                        ?? $"SymbolKey '{targetDescriptor.SymbolKey}'";
                    throw new OntologyCompositionException(
                        $"AONT041: descriptor identity '{identityLabel}' has multiple registrations " +
                        $"({string.Join(", ", regs.Select(r => $"'{r.DomainName}.{r.Name}'"))}) but is also " +
                        $"referenced as a link target in '{descriptor.Name}.{link.Name}'. " +
                        $"Multi-registered types cannot participate in structural links. " +
                        $"See #32 for a future relaxation path.");
                }

                // AONT041 extension: reject a link whose target type is registered with an
                // explicit (non-default) descriptor name. HasMany<TLinked> writes the CLR
                // simple name into TargetTypeName, so if the registered descriptor name
                // differs from the CLR simple name, reads and writes would diverge silently
                // across two table names. Enforcing single-registration with default name
                // is the Option X minimum-invasiveness fix (see #33 Finding 1).
                // Polyglot note: only meaningful for CLR-typed descriptors — ingested-only
                // descriptors have no CLR-simple-name baseline to diverge from.
                // We consult the CLR-simple-name index here because link.TargetTypeName
                // is the CLR simple name; the explicit-name divergence shows up precisely
                // when that name does not match any registered descriptor by name.
                if (descriptorByDomainAndClrSimpleName.TryGetValue(
                        (descriptor.DomainName, link.TargetTypeName), out var explicitNameTarget)
                    && explicitNameTarget.ClrType is not null
                    && explicitNameTarget.Name != explicitNameTarget.ClrType.Name)
                {
                    throw new OntologyCompositionException(
                        $"AONT041: Link '{descriptor.Name}.{link.Name}' targets CLR type " +
                        $"'{explicitNameTarget.ClrType.FullName}' which is registered with explicit " +
                        $"descriptor name '{explicitNameTarget.Name}' (default would be '{explicitNameTarget.ClrType.Name}'). " +
                        $"A link target registered under a non-default name causes read/write table-name " +
                        $"divergence. Either remove the explicit name or use the default registration. " +
                        $"See #33 Finding 1.");
                }
            }

            // Check: does this descriptor's own identity have multiple registrations AND
            // declare outgoing links? The source side is just as invalid as the target side.
            // Polyglot: works for both CLR-typed and ingested-only descriptors.
            if (descriptor.Links.Count > 0
                && IsMultiRegistered(descriptor.DomainName, descriptor.Name))
            {
                var regs = RegistrationsFor(descriptor.DomainName, descriptor.Name);
                var identityLabel = descriptor.ClrType?.FullName
                    ?? $"SymbolKey '{descriptor.SymbolKey}'";
                throw new OntologyCompositionException(
                    $"AONT041: descriptor identity '{identityLabel}' has multiple registrations " +
                    $"({string.Join(", ", regs.Select(r => $"'{r.DomainName}.{r.Name}'"))}) but also " +
                    $"declares outgoing links ({string.Join(", ", descriptor.Links.Select(l => $"'{l.Name}'"))}). " +
                    $"Multi-registered types cannot participate in structural links. " +
                    $"See #32 for a future relaxation path.");
            }
        }

        // Cross-domain link checks. Carries the source CLR type on the descriptor and
        // identifies the target by (TargetDomain, TargetTypeName). We deliberately reject
        // ANY cross-domain link whose source or target identity appears more than once
        // in the polyglot reverse index, even if the (sourceDomain, ClrType) lookup would
        // itself be unambiguous — Option X says multi-registered types are leaf-only,
        // full stop.
        foreach (var (sourceDomain, link) in crossDomainLinks)
        {
            // Source side: link.SourceType is the CLR type the From<T>() builder set.
            // Cross-domain link sources are still strictly CLR-typed (the From<T>() API
            // requires it), so the source check stays CLR-keyed via namesByType.
            if (namesByType.TryGetValue(link.SourceType, out var srcNames) && srcNames.Count > 1)
            {
                throw new OntologyCompositionException(
                    $"AONT041: CLR type '{link.SourceType.FullName}' has multiple registrations " +
                    $"({string.Join(", ", srcNames.Select(n => $"'{n}'"))}) but is declared as the source " +
                    $"of cross-domain link '{link.Name}' in domain '{sourceDomain}'. Multi-registered " +
                    $"types cannot participate in structural links. See #32 for a future relaxation path.");
            }

            // Target side: resolve the target descriptor by (TargetDomain, TargetTypeName)
            // and check whether its identity (CLR or SymbolKey) is multi-registered. If
            // the target is unresolvable here, leave the diagnostic to
            // ResolveCrossDomainLinks below.
            var targetDescriptor = allObjectTypes.FirstOrDefault(
                ot => ot.DomainName == link.TargetDomain && ot.Name == link.TargetTypeName);

            if (targetDescriptor is not null
                && IsMultiRegistered(targetDescriptor.DomainName, targetDescriptor.Name))
            {
                var tgtRegs = RegistrationsFor(targetDescriptor.DomainName, targetDescriptor.Name);
                var identityLabel = targetDescriptor.ClrType?.FullName
                    ?? $"SymbolKey '{targetDescriptor.SymbolKey}'";
                throw new OntologyCompositionException(
                    $"AONT041: descriptor identity '{identityLabel}' has multiple registrations " +
                    $"({string.Join(", ", tgtRegs.Select(r => $"'{r.DomainName}.{r.Name}'"))}) but is the target of cross-domain link " +
                    $"'{link.Name}' from domain '{sourceDomain}' to '{link.TargetDomain}.{link.TargetTypeName}'. " +
                    $"Multi-registered types cannot participate in structural links. See #32 for a future relaxation path.");
            }
        }
    }

    private static void InferPropertyKinds(List<ObjectTypeDescriptor> allObjectTypes)
    {
        // DR-1 polyglot: ingested-only descriptors (null ClrType) don't participate
        // in CLR-keyed reference-property inference; they reach reference kinds via
        // PropertyDescriptor.ReferenceSymbolKey instead (DR-1, Task 4+).
        var registeredClrTypes = allObjectTypes
            .Select(ot => ot.ClrType)
            .Where(t => t is not null)
            .Select(t => t!)
            .ToHashSet();

        for (var i = 0; i < allObjectTypes.Count; i++)
        {
            var objectType = allObjectTypes[i];
            var updatedProperties = new List<PropertyDescriptor>();
            var hasChanges = false;

            foreach (var prop in objectType.Properties)
            {
                if (prop.Kind == PropertyKind.Vector)
                {
                    updatedProperties.Add(prop);
                    continue;
                }

                PropertyKind kind;

                if (prop.IsComputed)
                {
                    kind = PropertyKind.Computed;
                }
                else if (IsReferenceType(prop.PropertyType, registeredClrTypes))
                {
                    kind = PropertyKind.Reference;
                }
                else
                {
                    kind = PropertyKind.Scalar;
                }

                if (kind != prop.Kind)
                {
                    hasChanges = true;
                    updatedProperties.Add(prop with { Kind = kind });
                }
                else
                {
                    updatedProperties.Add(prop);
                }
            }

            if (hasChanges)
            {
                allObjectTypes[i] = objectType with
                {
                    Properties = updatedProperties.AsReadOnly(),
                };
            }
        }
    }

    private static bool IsReferenceType(Type propertyType, HashSet<Type> registeredClrTypes)
    {
        // Check the direct type
        if (registeredClrTypes.Contains(propertyType))
        {
            return true;
        }

        // Check for Nullable<T>
        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying is not null && registeredClrTypes.Contains(underlying))
        {
            return true;
        }

        return false;
    }

    private static List<WorkflowChain> BuildWorkflowChains(
        List<ObjectTypeDescriptor> allObjectTypes,
        List<WorkflowMetadataBuilder> workflowMetadata,
        List<string> warnings)
    {
        var chains = new List<WorkflowChain>();

        // Workflow metadata uses unqualified type names; under the AONT040 invariant the
        // same simple name can legitimately appear in two domains. Build a multi-valued
        // index so we can detect ambiguity rather than silently first-wins-binding to
        // one descriptor and producing a wrong workflow chain.
        var objectTypesByName = allObjectTypes
            .GroupBy(ot => ot.Name)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Domain-keyed index for metadata that carries a DomainName via InDomain().
        // Keyed by (DomainName, ClrType.Name) so that Consumes<T>()/Produces<T>() — which
        // store typeof(T).Name — resolve to the right descriptor in the specified domain
        // even when the descriptor was registered with an explicit (non-CLR) name.
        // See #33 Finding 4. DR-1 polyglot: ingested-only types skipped here.
        var objectTypesByDomainAndClrName = allObjectTypes
            .Where(ot => ot.ClrType is not null)
            .GroupBy(ot => (ot.DomainName, ot.ClrType!.Name))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var metadata in workflowMetadata)
        {
            if (metadata.ConsumedTypeName is null || metadata.ProducedTypeName is null)
            {
                warnings.Add(
                    $"Workflow '{metadata.WorkflowName}' is missing consumed or produced type metadata; skipping.");
                continue;
            }

            ObjectTypeDescriptor consumedType;
            ObjectTypeDescriptor producedType;

            if (metadata.DomainName is not null)
            {
                if (!TryResolveDomainKeyed(
                        objectTypesByDomainAndClrName, metadata.DomainName, metadata.ConsumedTypeName,
                        metadata.WorkflowName, "consumed", warnings, out consumedType))
                {
                    continue;
                }

                if (!TryResolveDomainKeyed(
                        objectTypesByDomainAndClrName, metadata.DomainName, metadata.ProducedTypeName,
                        metadata.WorkflowName, "produced", warnings, out producedType))
                {
                    continue;
                }
            }
            else
            {
                if (!TryResolveUnambiguous(
                        objectTypesByName, metadata.ConsumedTypeName, metadata.WorkflowName, "consumed", warnings, out consumedType))
                {
                    continue;
                }

                if (!TryResolveUnambiguous(
                        objectTypesByName, metadata.ProducedTypeName, metadata.WorkflowName, "produced", warnings, out producedType))
                {
                    continue;
                }
            }

            chains.Add(new WorkflowChain(metadata.WorkflowName, consumedType, producedType));
        }

        return chains;
    }

    private static bool TryResolveDomainKeyed(
        Dictionary<(string DomainName, string ClrName), ObjectTypeDescriptor> objectTypesByDomainAndClrName,
        string domainName,
        string typeName,
        string workflowName,
        string role,
        List<string> warnings,
        out ObjectTypeDescriptor resolved)
    {
        resolved = null!;

        if (!objectTypesByDomainAndClrName.TryGetValue((domainName, typeName), out resolved!))
        {
            warnings.Add(
                $"Workflow '{workflowName}' references unknown {role} type '{typeName}' in domain '{domainName}'; skipping.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// DR-3 + DR-10 (Tasks 13, 17): drains each registered
    /// <see cref="IOntologySource"/>'s <c>LoadAsync</c> and applies each
    /// emitted delta to the per-domain <see cref="OntologyBuilder"/>.
    /// Synchronous bridging of the async stream is intentional —
    /// <see cref="Build"/> is sync and Strategos 2.5.0 ships only the
    /// startup drain. Source-raised exceptions are wrapped in
    /// <see cref="OntologyCompositionException"/> with the offending
    /// <see cref="IOntologySource.SourceId"/> in the message so logs and
    /// incident reports attribute the failure to the right ingester.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "VSTHRD002:Synchronously waiting on tasks or awaiters",
        Justification =
            "Intentional sync bridge at the OntologyGraphBuilder startup boundary. " +
            "Build() is sync by design and Strategos 2.5.0 only drains LoadAsync " +
            "once at composition; the async surface exists for forward compatibility " +
            "(live invalidation lands in v2.6.0+ via SubscribeAsync). Task.Run + " +
            "ConfigureAwait(false) protects callers running under a captured " +
            "SynchronizationContext (e.g. WPF/UI).")]
    private void DrainSources(
        Dictionary<string, OntologyBuilder> buildersByDomain,
        CancellationToken cancellationToken)
    {
        foreach (var source in _sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Task.Run(
                    async () =>
                        await DrainSourceCoreAsync(source, buildersByDomain, cancellationToken).ConfigureAwait(false),
                    cancellationToken)
                    .GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OntologyCompositionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new OntologyCompositionException(
                    $"IOntologySource '{source.SourceId}' raised during LoadAsync: {ex.Message}",
                    ex);
            }
        }
    }

    private static async Task DrainSourceCoreAsync(
        IOntologySource source,
        Dictionary<string, OntologyBuilder> buildersByDomain,
        CancellationToken cancellationToken)
    {
        await foreach (var delta in source.LoadAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var domainName = ResolveDeltaDomain(delta);
            if (domainName is null)
            {
                continue;
            }

            if (!buildersByDomain.TryGetValue(domainName, out var ontologyBuilder))
            {
                ontologyBuilder = new OntologyBuilder(domainName);
                buildersByDomain[domainName] = ontologyBuilder;
            }

            ontologyBuilder.ApplyDelta(delta);
        }
    }

    private static string? ResolveDeltaDomain(OntologyDelta delta) => delta switch
    {
        OntologyDelta.AddObjectType a => a.Descriptor.DomainName,
        OntologyDelta.UpdateObjectType u => u.Descriptor.DomainName,
        OntologyDelta.RemoveObjectType r => r.DomainName,
        OntologyDelta.AddProperty ap => ap.DomainName,
        OntologyDelta.RenameProperty rp => rp.DomainName,
        OntologyDelta.RemoveProperty rp => rp.DomainName,
        OntologyDelta.AddLink al => al.DomainName,
        OntologyDelta.RemoveLink rl => rl.DomainName,
        _ => null,
    };

    private static bool TryResolveUnambiguous(
        Dictionary<string, List<ObjectTypeDescriptor>> objectTypesByName,
        string typeName,
        string workflowName,
        string role,
        List<string> warnings,
        out ObjectTypeDescriptor resolved)
    {
        resolved = null!;

        if (!objectTypesByName.TryGetValue(typeName, out var matches) || matches.Count == 0)
        {
            warnings.Add(
                $"Workflow '{workflowName}' references unknown {role} type '{typeName}'; skipping.");
            return false;
        }

        if (matches.Count > 1)
        {
            var domains = string.Join(", ", matches.Select(m => $"'{m.DomainName}'"));
            warnings.Add(
                $"Workflow '{workflowName}' references {role} type '{typeName}' which is ambiguous across domains ({domains}); skipping.");
            return false;
        }

        resolved = matches[0];
        return true;
    }
}
