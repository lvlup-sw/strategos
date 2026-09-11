// -----------------------------------------------------------------------
// <copyright file="ProofCatalogReader.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Strategos.Analyzers.Proof;
using Strategos.Generators.Import;
using Strategos.Ontology.ActionLogic;

namespace Strategos.Generators.Proof;

// =============================================================================
// ProofCatalogV1 consumption (#204).
//
// Every path in this reader either produces a catalog or produces a REASON. It
// never produces a partial catalog, and it never skips a member it does not
// recognize. That is the whole discipline: a proof that silently dropped an
// obligation it could not read would be worse than no proof, because the build
// would still be green.
//
// Concretely, all of these are refusals rather than omissions: an unknown
// schemaVersion, an unknown predicate or literal discriminator, an unknown
// comparison operator or scalar kind, a missing required member, a content hash
// that does not match the bytes, and an action whose declared authority the
// catalog's own lattice does not define.
//
// The same discipline applies to a member of the WRONG SHAPE, not only a missing
// one, because each of these narrows a proof input rather than breaking it -- and
// a narrowed input still proves, just against less than the catalog declared:
//
//   * a non-string authority axis level      shifts the rank of every later level
//   * a frame entry with no name             proves the guarantee against a narrower frame
//   * a non-string required authority        removes the authority obligation entirely
//   * a read-set entry with no name          hides the atom from the frame intersection
// =============================================================================

/// <summary>The outcome of reading one referenced assembly's proof catalog.</summary>
internal sealed class ProofCatalogReadResult
{
    private ProofCatalogReadResult(ProofCatalogDocument? document, string? failureReason)
    {
        Document = document;
        FailureReason = failureReason;
    }

    /// <summary>Gets the catalog, when the read succeeded.</summary>
    internal ProofCatalogDocument? Document { get; }

    /// <summary>Gets why the read failed, when it did.</summary>
    internal string? FailureReason { get; }

    /// <summary>Gets a value indicating whether the read produced a usable catalog.</summary>
    internal bool Succeeded => Document is not null;

    internal static ProofCatalogReadResult Success(ProofCatalogDocument document) =>
        new(document, failureReason: null);

    internal static ProofCatalogReadResult Failure(string reason) => new(document: null, reason);
}

/// <summary>Reads a ProofCatalogV1 document emitted by a referenced assembly.</summary>
internal static class ProofCatalogReader
{
    /// <summary>Reads and validates a catalog.</summary>
    /// <param name="json">The catalog JSON carried by the referenced assembly.</param>
    /// <returns>The catalog, or the reason it was refused.</returns>
    internal static ProofCatalogReadResult Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ProofCatalogReadResult.Failure("the catalog is empty");
        }

        JsonValue root;
        try
        {
            root = MinimalJsonReader.Parse(json);
        }
        catch (JsonParseException exception)
        {
            return ProofCatalogReadResult.Failure($"the catalog is not valid JSON: {exception.Message}");
        }

        if (root.Kind != JsonKind.Object)
        {
            return ProofCatalogReadResult.Failure("the catalog root is not a JSON object");
        }

        if (!TryGetString(root, "schemaVersion", out var schemaVersion))
        {
            return ProofCatalogReadResult.Failure("the catalog declares no schemaVersion");
        }

        if (!string.Equals(schemaVersion, ProofCatalogWriter.SchemaVersion, StringComparison.Ordinal))
        {
            return ProofCatalogReadResult.Failure(
                $"the catalog declares schemaVersion '{schemaVersion}', and this compiler reads "
                + $"'{ProofCatalogWriter.SchemaVersion}'. An unknown version is refused rather than "
                + "read on a guess.");
        }

        if (!TryGetString(root, "catalogId", out var catalogId) || string.IsNullOrWhiteSpace(catalogId))
        {
            return ProofCatalogReadResult.Failure("the catalog declares no catalogId");
        }

        var declaredHash = TryGetString(root, "contentHash", out var hash) ? hash : null;
        if (declaredHash is not null)
        {
            var recomputed = ProofCatalogWriter.Sha256Hex(StripContentHash(json, declaredHash));
            if (!string.Equals(recomputed, declaredHash, StringComparison.Ordinal))
            {
                return ProofCatalogReadResult.Failure(
                    $"the catalog's contentHash '{declaredHash}' does not match its content "
                    + $"(recomputed '{recomputed}'). The catalog has been altered since it was emitted, "
                    + "or was emitted by a different version of the writer.");
            }
        }

        var lattices = ImmutableArray<OntologyAuthorityLattice>.Empty;
        if (root.TryGetMember("authorities", out var authorities))
        {
            var latticeResult = ReadLattices(authorities);
            if (latticeResult.Reason is { } latticeReason)
            {
                return ProofCatalogReadResult.Failure(latticeReason);
            }

            lattices = latticeResult.Value;
        }

        if (!root.TryGetMember("actions", out var actionsRoot)
            || actionsRoot.Kind != JsonKind.Object
            || !actionsRoot.TryGetMember("actions", out var actionArray)
            || actionArray.Kind != JsonKind.Array)
        {
            return ProofCatalogReadResult.Failure("the catalog declares no actions array");
        }

        var declaredAuthorities = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.Ordinal);
        foreach (var pair in lattices.SelectMany(lattice => lattice.Authorities))
        {
            declaredAuthorities[pair.Key] = pair.Value;
        }

        var actions = ImmutableArray.CreateBuilder<ProofCatalogAction>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in actionArray.Items)
        {
            var action = ReadAction(element);
            if (action.Reason is { } reason)
            {
                return ProofCatalogReadResult.Failure(reason);
            }

            var identity = action.Value!.Identity.ToString();
            if (!seen.Add(identity))
            {
                return ProofCatalogReadResult.Failure(
                    $"the catalog declares action '{identity}' more than once");
            }

            // An authority the catalog's own lattice does not define is an incomplete
            // lattice. Downstream it would silently widen every authority comparison
            // that names it, so it is refused where it is read.
            if (action.Value.RequiredAuthority is { } required)
            {
                if (!declaredAuthorities.TryGetValue(required, out var declaredCoordinate))
                {
                    return ProofCatalogReadResult.Failure(
                        $"action '{identity}' requires authority '{required}', which the catalog's "
                        + "authority lattices do not define");
                }

                // The wire carries the coordinate AND the name it came from. They are
                // resolved from the same lattice at export, so a disagreement here is
                // not a stale catalog — it is a catalog whose action and lattice were
                // edited apart, and the coordinate is the half a consumer would use.
                var carried = action.Value.AuthorityCoordinates
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToImmutableArray();
                var expected = declaredCoordinate
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToImmutableArray();
                if (!carried.SequenceEqual(expected))
                {
                    return ProofCatalogReadResult.Failure(
                        $"action '{identity}' carries an authority coordinate that disagrees with "
                        + $"the lattice definition of '{required}'");
                }
            }

            actions.Add(action.Value);
        }

        return ProofCatalogReadResult.Success(new ProofCatalogDocument(
            catalogId,
            actions.ToImmutable(),
            lattices,
            declaredHash));
    }

    /// <summary>
    /// Removes the <c>contentHash</c> member so the hash can be recomputed over the
    /// bytes it was taken from.
    /// </summary>
    /// <param name="json">The stamped catalog JSON.</param>
    /// <param name="hash">The declared hash.</param>
    /// <returns>The catalog JSON without its hash member.</returns>
    /// <remarks>
    /// A textual removal rather than a re-serialization, on purpose. Re-serializing
    /// would compare the reader's idea of canonical form against the writer's, so a
    /// writer change would read as tampering; removing the one member compares the
    /// bytes that were actually hashed.
    /// </remarks>
    private static string StripContentHash(string json, string hash)
    {
        var member = ",\"contentHash\":\"" + hash + "\"";
        var index = json.IndexOf(member, StringComparison.Ordinal);
        return index < 0 ? json : json.Remove(index, member.Length);
    }

    private static Result<ImmutableArray<OntologyAuthorityLattice>> ReadLattices(JsonValue value)
    {
        if (value.Kind != JsonKind.Array)
        {
            return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                "the catalog's authorities member is not an array");
        }

        var lattices = ImmutableArray.CreateBuilder<OntologyAuthorityLattice>();
        foreach (var element in value.Items)
        {
            if (!TryGetString(element, "domainName", out var domainName)
                || !element.TryGetMember("lattice", out var lattice)
                || lattice.Kind != JsonKind.Object)
            {
                return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                    "the catalog declares a malformed authority lattice");
            }

            var axes = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
            if (lattice.TryGetMember("axes", out var axesValue) && axesValue.Kind == JsonKind.Array)
            {
                foreach (var axis in axesValue.Items)
                {
                    if (!TryGetString(axis, "name", out var axisName)
                        || !axis.TryGetMember("levels", out var levels)
                        || levels.Kind != JsonKind.Array)
                    {
                        return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                            $"the '{domainName}' authority lattice declares a malformed axis");
                    }

                    var ordered = ImmutableArray.CreateBuilder<string>(levels.Items.Count);
                    foreach (var level in levels.Items)
                    {
                        // Dropping a level would silently shift the rank of every level
                        // after it, and rank is what TryJoinAtMost compares.
                        if (level.AsStringOrNull() is not { } levelName)
                        {
                            return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                                $"the '{domainName}' authority lattice declares a non-string level on axis '{axisName}'");
                        }

                        ordered.Add(levelName);
                    }

                    axes[axisName] = ordered.ToImmutable();
                }
            }

            var authorities = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            if (lattice.TryGetMember("authorities", out var authoritiesValue)
                && authoritiesValue.Kind == JsonKind.Array)
            {
                foreach (var authority in authoritiesValue.Items)
                {
                    if (!TryGetString(authority, "name", out var authorityName)
                        || !authority.TryGetMember("coordinates", out var coordinates)
                        || coordinates.Kind != JsonKind.Array)
                    {
                        return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                            $"the '{domainName}' authority lattice declares a malformed authority");
                    }

                    var coordinate = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var pair in coordinates.Items)
                    {
                        if (!TryGetString(pair, "axis", out var axis)
                            || !TryGetString(pair, "level", out var level))
                        {
                            return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                                $"authority '{authorityName}' declares a malformed coordinate");
                        }

                        if (!axes.TryGetValue(axis, out var levels) || !levels.Contains(level))
                        {
                            return Result<ImmutableArray<OntologyAuthorityLattice>>.Fail(
                                $"authority '{authorityName}' names level '{level}' on axis '{axis}', "
                                + "which the lattice does not declare");
                        }

                        coordinate[axis] = level;
                    }

                    authorities[authorityName] = coordinate;
                }
            }

            lattices.Add(new OntologyAuthorityLattice(
                domainName,
                axes,
                authorities,
                invalidReason: null));
        }

        return Result<ImmutableArray<OntologyAuthorityLattice>>.Ok(lattices.ToImmutable());
    }

    private static Result<ProofCatalogAction> ReadAction(JsonValue value)
    {
        if (value.Kind != JsonKind.Object
            || !value.TryGetMember("subject", out var subject)
            || !TryGetString(subject, "domainName", out var domainName)
            || !TryGetString(subject, "objectTypeName", out var objectTypeName)
            || !TryGetString(value, "name", out var actionName))
        {
            return Result<ProofCatalogAction>.Fail("the catalog declares an action with no identity");
        }

        var identity = new ActionIdentity(domainName, objectTypeName, actionName);

        var requirement = ReadSinglePredicate(value, "requires", identity);
        if (requirement.Reason is { } requirementReason)
        {
            return Result<ProofCatalogAction>.Fail(requirementReason);
        }

        var guarantee = ReadSinglePredicate(value, "ensures", identity);
        if (guarantee.Reason is { } guaranteeReason)
        {
            return Result<ProofCatalogAction>.Fail(guaranteeReason);
        }

        var frame = ImmutableArray<string>.Empty;
        if (value.TryGetMember("touches", out var touches) && touches.Kind == JsonKind.Array)
        {
            var resources = ImmutableArray.CreateBuilder<string>(touches.Items.Count);
            foreach (var resource in touches.Items)
            {
                // The frame is what the guarantee is projected through. A dropped entry
                // proves the action against a narrower frame than it declared.
                if (!TryGetString(resource, "name", out var resourceName))
                {
                    return Result<ProofCatalogAction>.Fail(
                        $"action '{identity}' declares a frame entry with no name");
                }

                resources.Add(resourceName);
            }

            frame = resources.ToImmutable();
        }

        string? requiredAuthority = null;
        var authorityCoordinates = ImmutableArray<KeyValuePair<string, string>>.Empty;
        if (value.TryGetMember("authority", out var authority))
        {
            if (authority.TryGetMember("sourceAuthorities", out var sources)
                && sources.Kind == JsonKind.Array
                && sources.Items.Count > 0)
            {
                // Leaving this null does not weaken the proof a little; it removes the
                // authority obligation and the lattice cross-check that reads it.
                if (sources.Items[0].AsStringOrNull() is not { } declaredAuthority)
                {
                    return Result<ProofCatalogAction>.Fail(
                        $"action '{identity}' declares a non-string required authority");
                }

                requiredAuthority = declaredAuthority;
            }

            if (authority.TryGetMember("coordinates", out var coordinates)
                && coordinates.Kind == JsonKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<KeyValuePair<string, string>>();
                foreach (var pair in coordinates.Items)
                {
                    if (!TryGetString(pair, "axis", out var axis)
                        || !TryGetString(pair, "level", out var level))
                    {
                        return Result<ProofCatalogAction>.Fail(
                            $"action '{identity}' declares a malformed authority coordinate");
                    }

                    builder.Add(new KeyValuePair<string, string>(axis, level));
                }

                authorityCoordinates = builder.ToImmutable();
            }

            if (requiredAuthority is null && authorityCoordinates.IsEmpty)
            {
                return Result<ProofCatalogAction>.Fail(
                    $"action '{identity}' declares an authority with neither a coordinate nor a "
                    + "source authority");
            }
        }

        string? inverse = null;
        if (value.TryGetMember("inverse", out var inverseValue)
            && TryGetString(inverseValue, "actionName", out var inverseName))
        {
            inverse = inverseName;
        }

        var boundWorkflow = TryGetString(value, "boundWorkflow", out var bound) ? bound : null;

        return Result<ProofCatalogAction>.Ok(new ProofCatalogAction(
            identity,
            boundWorkflow,
            requirement.Value!,
            guarantee.Value!,
            frame,
            requiredAuthority,
            authorityCoordinates,
            inverse));
    }

    /// <summary>
    /// Reads the one predicate the compilation-local contract model holds for a
    /// <c>requires</c> or <c>ensures</c> list.
    /// </summary>
    /// <remarks>
    /// The wire permits many entries; the model this reader fills holds one formula.
    /// More than one is refused rather than conjoined: conjoining a hard and a soft
    /// requirement would silently promote the soft one, and conjoining two guarantees
    /// would assert something neither entry claimed.
    /// </remarks>
    private static Result<OntologyPredicateContract> ReadSinglePredicate(
        JsonValue action,
        string member,
        ActionIdentity identity)
    {
        if (!action.TryGetMember(member, out var list) || list.Kind != JsonKind.Array)
        {
            return Result<OntologyPredicateContract>.Fail(
                $"action '{identity}' declares no {member} array");
        }

        if (list.Items.Count == 0)
        {
            return Result<OntologyPredicateContract>.Ok(OntologyPredicateContract.True);
        }

        if (list.Items.Count > 1)
        {
            return Result<OntologyPredicateContract>.Fail(
                $"action '{identity}' declares {list.Items.Count} {member} entries, and this "
                + "compiler reads exactly one");
        }

        if (!list.Items[0].TryGetMember("predicate", out var predicate))
        {
            return Result<OntologyPredicateContract>.Fail(
                $"action '{identity}' declares a {member} entry with no predicate");
        }

        var atomReads = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(
            StringComparer.Ordinal);
        var opaqueKeys = ImmutableArray.CreateBuilder<string>();
        var formula = ReadPredicate(predicate, identity, atomReads, opaqueKeys);
        if (formula.Reason is { } reason)
        {
            return Result<OntologyPredicateContract>.Fail(reason);
        }

        return Result<OntologyPredicateContract>.Ok(OntologyPredicateContract.FromImported(
            formula.Value!,
            ProofCatalogProjection.RebuildAtomReads(formula.Value!, atomReads),
            opaqueKeys.Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToImmutableArray()));
    }

    private static Result<LogicFormula> ReadPredicate(
        JsonValue value,
        ActionIdentity identity,
        ImmutableDictionary<string, ImmutableArray<string>>.Builder atomReads,
        ImmutableArray<string>.Builder opaqueKeys)
    {
        if (!TryGetString(value, "kind", out var kind))
        {
            return Result<LogicFormula>.Fail($"action '{identity}' declares a predicate with no kind");
        }

        switch (kind)
        {
            case "true":
                return Result<LogicFormula>.Ok(LogicFormula.True);

            case "false":
                return Result<LogicFormula>.Ok(LogicFormula.False);

            case "property-comparison":
                return ReadComparison(value, identity);

            case "not":
            {
                if (!value.TryGetMember("predicate", out var operand))
                {
                    return Result<LogicFormula>.Fail(
                        $"action '{identity}' declares a 'not' predicate with no operand");
                }

                var inner = ReadPredicate(operand, identity, atomReads, opaqueKeys);
                return inner.Reason is { } reason
                    ? Result<LogicFormula>.Fail(reason)
                    : Result<LogicFormula>.Ok(LogicFormula.Not(inner.Value!));
            }

            case "all":
            case "any":
            {
                if (!value.TryGetMember("predicates", out var operands)
                    || operands.Kind != JsonKind.Array)
                {
                    return Result<LogicFormula>.Fail(
                        $"action '{identity}' declares an '{kind}' predicate with no operands");
                }

                var parsed = new List<LogicFormula>();
                foreach (var operand in operands.Items)
                {
                    var inner = ReadPredicate(operand, identity, atomReads, opaqueKeys);
                    if (inner.Reason is { } reason)
                    {
                        return Result<LogicFormula>.Fail(reason);
                    }

                    parsed.Add(inner.Value!);
                }

                return Result<LogicFormula>.Ok(string.Equals(kind, "all", StringComparison.Ordinal)
                    ? LogicFormula.All(parsed)
                    : LogicFormula.Any(parsed));
            }

            case "custom":
            {
                if (!TryGetString(value, "evaluatorKey", out var evaluatorKey))
                {
                    return Result<LogicFormula>.Fail(
                        $"action '{identity}' declares a custom predicate with no evaluatorKey");
                }

                var reads = ImmutableArray<string>.Empty;
                if (value.TryGetMember("readSet", out var readSet) && readSet.Kind == JsonKind.Array)
                {
                    var readNames = ImmutableArray.CreateBuilder<string>(readSet.Items.Count);
                    foreach (var resource in readSet.Items)
                    {
                        // The read set is what the frame check intersects to select this
                        // atom. Dropping one hides the atom from the check.
                        if (!TryGetString(resource, "name", out var readName))
                        {
                            return Result<LogicFormula>.Fail(
                                $"action '{identity}' declares a custom predicate read-set entry with no name");
                        }

                        readNames.Add(readName);
                    }

                    reads = readNames
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToImmutableArray();
                }

                atomReads[evaluatorKey] = reads;
                opaqueKeys.Add(evaluatorKey);
                return Result<LogicFormula>.Ok(LogicFormula.Opaque(evaluatorKey));
            }

            case "link-exists":
            case "relation-holds":
                // These arms are part of the wire vocabulary but have no counterpart in
                // the compilation-local proof kernel, which reasons over property
                // resources. Reading one as opaque would quietly weaken every proof
                // that touched it, so it is refused instead.
                return Result<LogicFormula>.Fail(
                    $"action '{identity}' declares a '{kind}' predicate, which this compiler's "
                    + "proof kernel does not reason over");

            default:
                return Result<LogicFormula>.Fail(
                    $"action '{identity}' declares predicate kind '{kind}', which this compiler "
                    + "does not know");
        }
    }

    private static Result<LogicFormula> ReadComparison(JsonValue value, ActionIdentity identity)
    {
        if (!value.TryGetMember("property", out var property)
            || !TryGetString(property, "name", out var name)
            || !TryGetString(property, "scalarKind", out var scalarKindToken)
            || !TryGetString(value, "operator", out var operatorToken)
            || !value.TryGetMember("value", out var literalValue))
        {
            return Result<LogicFormula>.Fail(
                $"action '{identity}' declares a malformed property comparison");
        }

        if (!TryReadScalarKind(scalarKindToken, out var scalarKind))
        {
            return Result<LogicFormula>.Fail(
                $"action '{identity}' declares scalar kind '{scalarKindToken}', which this compiler "
                + "does not know");
        }

        if (!TryReadOperator(operatorToken, out var comparison))
        {
            return Result<LogicFormula>.Fail(
                $"action '{identity}' declares comparison operator '{operatorToken}', which this "
                + "compiler does not know");
        }

        var isNullable = property.TryGetMember("isNullable", out var nullable)
            && nullable.AsBoolOrNull() == true;
        var enumTypeName = TryGetString(property, "enumTypeName", out var typeName) ? typeName : null;

        var literal = ReadLiteral(literalValue, identity);
        if (literal.Reason is { } reason)
        {
            return Result<LogicFormula>.Fail(reason);
        }

        return Result<LogicFormula>.Ok(LogicFormula.Comparison(
            new LogicResource(name, scalarKind, isNullable, enumTypeName),
            comparison,
            literal.Value!));
    }

    private static Result<LogicLiteral> ReadLiteral(JsonValue value, ActionIdentity identity)
    {
        if (!TryGetString(value, "kind", out var kind))
        {
            return Result<LogicLiteral>.Fail($"action '{identity}' declares a literal with no kind");
        }

        switch (kind)
        {
            case "null":
                return Result<LogicLiteral>.Ok(LogicLiteral.Null);

            case "boolean":
            {
                var boolean = value.TryGetMember("value", out var raw) ? raw.AsBoolOrNull() : null;
                return boolean is null
                    ? Result<LogicLiteral>.Fail(
                        $"action '{identity}' declares a boolean literal with no value")
                    : Result<LogicLiteral>.Ok(new LogicLiteral(
                        LogicLiteralKind.Boolean,
                        boolean.Value ? "true" : "false"));
            }

            case "enum":
            {
                if (!TryGetString(value, "typeName", out var typeName)
                    || !TryGetString(value, "memberName", out var memberName))
                {
                    return Result<LogicLiteral>.Fail(
                        $"action '{identity}' declares an enum literal with no type or member");
                }

                return Result<LogicLiteral>.Ok(
                    new LogicLiteral(LogicLiteralKind.Enum, memberName, typeName));
            }

            case "integer":
            case "decimal":
            case "string":
            case "symbol":
            {
                if (!TryGetString(value, "value", out var text))
                {
                    return Result<LogicLiteral>.Fail(
                        $"action '{identity}' declares a '{kind}' literal with no value");
                }

                var literalKind = kind switch
                {
                    "integer" => LogicLiteralKind.Integer,
                    "decimal" => LogicLiteralKind.Decimal,
                    "string" => LogicLiteralKind.String,
                    _ => LogicLiteralKind.Symbol,
                };
                return Result<LogicLiteral>.Ok(new LogicLiteral(literalKind, text));
            }

            default:
                return Result<LogicLiteral>.Fail(
                    $"action '{identity}' declares literal kind '{kind}', which this compiler "
                    + "does not know");
        }
    }

    private static bool TryReadScalarKind(string token, out LogicScalarKind kind)
    {
        switch (token)
        {
            case "boolean": kind = LogicScalarKind.Boolean; return true;
            case "integer": kind = LogicScalarKind.Integer; return true;
            case "decimal": kind = LogicScalarKind.Decimal; return true;
            case "string": kind = LogicScalarKind.String; return true;
            case "enum": kind = LogicScalarKind.Enum; return true;
            case "symbol": kind = LogicScalarKind.Symbol; return true;
            default: kind = default; return false;
        }
    }

    private static bool TryReadOperator(string token, out LogicComparisonOperator comparison)
    {
        switch (token)
        {
            case "equal": comparison = LogicComparisonOperator.Equal; return true;
            case "not-equal": comparison = LogicComparisonOperator.NotEqual; return true;
            case "less-than": comparison = LogicComparisonOperator.LessThan; return true;
            case "less-than-or-equal": comparison = LogicComparisonOperator.LessThanOrEqual; return true;
            case "greater-than": comparison = LogicComparisonOperator.GreaterThan; return true;
            case "greater-than-or-equal":
                comparison = LogicComparisonOperator.GreaterThanOrEqual;
                return true;
            default: comparison = default; return false;
        }
    }

    private static bool TryGetString(JsonValue value, string member, out string result)
    {
        if (value.Kind == JsonKind.Object
            && value.TryGetMember(member, out var element)
            && element.AsStringOrNull() is { } text)
        {
            result = text;
            return true;
        }

        result = string.Empty;
        return false;
    }

    /// <summary>A value or the reason it could not be produced.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    private readonly struct Result<T>
    {
        private Result(T? value, string? reason)
        {
            Value = value;
            Reason = reason;
        }

        internal T? Value { get; }

        internal string? Reason { get; }

        internal static Result<T> Ok(T value) => new(value, reason: null);

        internal static Result<T> Fail(string reason) => new(default, reason);
    }
}
