// -----------------------------------------------------------------------
// <copyright file="ProofCatalogWriter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Strategos.Analyzers.Proof;
using Strategos.Ontology.ActionLogic;

namespace Strategos.Generators.Proof;

// =============================================================================
// ProofCatalogV1 emission (#204).
//
// The output is CANONICAL: member order is fixed by this code, collections are
// sorted before they are written, and no whitespace is emitted. Two builds of
// the same declarations therefore produce byte-identical JSON, which is what
// makes the content hash mean "the same declarations" rather than "the same
// build". Skew detection downstream is only as good as that property.
//
// The generator is an isolated netstandard2.0 analyzer with no serializer
// dependency (pinned by WireDtoSchemaConformanceTests), so the writer is
// vendored here, exactly as MinimalJsonReader is on the import path.
// =============================================================================

/// <summary>Serializes a <see cref="ProofCatalogDocument"/> as ProofCatalogV1 JSON.</summary>
internal static class ProofCatalogWriter
{
    /// <summary>The manifest schema version this writer emits and the reader accepts.</summary>
    internal const string SchemaVersion = "1.0";

    /// <summary>
    /// Writes the catalog and stamps it with the SHA-256 of its own unstamped bytes.
    /// </summary>
    /// <param name="document">The catalog to write.</param>
    /// <returns>The stamped catalog and its canonical JSON.</returns>
    /// <remarks>
    /// The hash covers the catalog written WITHOUT its <c>contentHash</c> member, so
    /// a reader recomputes it by dropping that one member rather than by knowing a
    /// separate canonicalization. Prose is excluded from the document entirely — the
    /// catalog carries no descriptions — so the hash cannot move on a comment.
    /// </remarks>
    internal static (ProofCatalogDocument Stamped, string Json) WriteStamped(
        ProofCatalogDocument document)
    {
        var unstamped = Write(document, includeContentHash: false);
        var hash = Sha256Hex(unstamped);
        var stamped = document.WithContentHash(hash);
        return (stamped, Write(stamped, includeContentHash: true));
    }

    /// <summary>Writes the catalog as canonical ProofCatalogV1 JSON.</summary>
    /// <param name="document">The catalog to write.</param>
    /// <param name="includeContentHash">Whether to emit the stamped hash.</param>
    /// <returns>The canonical JSON.</returns>
    internal static string Write(ProofCatalogDocument document, bool includeContentHash)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var builder = new StringBuilder();
        builder.Append('{');
        WriteMember(builder, "schemaVersion", SchemaVersion, first: true);
        WriteMember(builder, "catalogId", document.CatalogId, first: false);
        if (includeContentHash && document.ContentHash is { } hash)
        {
            WriteMember(builder, "contentHash", hash, first: false);
        }

        builder.Append(",\"actions\":{\"actions\":[");
        for (var i = 0; i < document.Actions.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            WriteAction(builder, document.Actions[i]);
        }

        builder.Append("]}");

        if (!document.AuthorityLattices.IsEmpty)
        {
            builder.Append(",\"authorities\":[");
            for (var i = 0; i < document.AuthorityLattices.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                WriteDomainLattice(builder, document.AuthorityLattices[i]);
            }

            builder.Append(']');
        }

        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>Computes the lowercase-hex SHA-256 of a UTF-8 string.</summary>
    /// <param name="value">The text to hash.</param>
    /// <returns>64 lowercase hex characters.</returns>
    internal static string Sha256Hex(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void WriteAction(StringBuilder builder, ProofCatalogAction action)
    {
        builder.Append("{\"subject\":{");
        WriteMember(builder, "domainName", action.Identity.DomainName, first: true);
        WriteMember(builder, "objectTypeName", action.Identity.ObjectTypeName, first: false);
        builder.Append('}');
        WriteMember(builder, "name", action.Identity.ActionName, first: false);

        // requires / ensures are lists on the wire because an action may carry hard
        // and soft requirements. The compilation-local model holds ONE formula for
        // each, so exactly one entry is written and the reader expects at most one.
        builder.Append(",\"requires\":[");
        WriteRequirement(builder, action.Requirement);
        builder.Append("],\"ensures\":[");
        WriteGuarantee(builder, action.Guarantee);
        builder.Append("],\"touches\":[");
        var frame = action.Frame.OrderBy(name => name, StringComparer.Ordinal).ToImmutableArray();
        for (var i = 0; i < frame.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"kind\":\"property\",");
            WriteMember(builder, "name", frame[i], first: true);
            builder.Append('}');
        }

        builder.Append(']');

        if (action.RequiredAuthority is { } authority)
        {
            // The coordinate is what a consumer compares; the name travels alongside
            // as provenance, so a reader holding both can check them against each
            // other. Resolution happened at export, where the declaring lattice was
            // in hand — see ProofCatalogDocument.FromLocalCatalog.
            builder.Append(",\"authority\":{\"coordinates\":[");
            for (var i = 0; i < action.AuthorityCoordinates.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                WriteMember(builder, "axis", action.AuthorityCoordinates[i].Key, first: true);
                WriteMember(builder, "level", action.AuthorityCoordinates[i].Value, first: false);
                builder.Append('}');
            }

            builder.Append("],\"sourceAuthorities\":[");
            WriteString(builder, authority);
            builder.Append("]}");
        }

        if (action.CompensatingActionName is { } inverse)
        {
            builder.Append(",\"inverse\":{");
            WriteMember(builder, "domainName", action.Identity.DomainName, first: true);
            WriteMember(builder, "objectTypeName", action.Identity.ObjectTypeName, first: false);
            WriteMember(builder, "actionName", inverse, first: false);
            builder.Append('}');
        }

        // Idempotence is not part of the compilation-local contract model. The wire
        // requires the member, so it is written as false rather than guessed: an
        // action claimed idempotent on no evidence is a claim a consumer would use.
        builder.Append(",\"idempotent\":false");

        if (!string.IsNullOrEmpty(action.BoundWorkflowName))
        {
            WriteMember(builder, "boundWorkflow", action.BoundWorkflowName!, first: false);
        }

        builder.Append('}');
    }

    private static void WriteRequirement(StringBuilder builder, OntologyPredicateContract predicate)
    {
        builder.Append("{\"predicate\":");
        WritePredicate(builder, predicate.Formula);
        builder.Append(',');
        WriteMember(builder, "expression", PredicateExpression.Render(predicate.Formula), first: true);
        builder.Append(",\"strength\":\"hard\"}");
    }

    private static void WriteGuarantee(StringBuilder builder, OntologyPredicateContract predicate)
    {
        builder.Append("{\"predicate\":");
        WritePredicate(builder, predicate.Formula);
        builder.Append(',');
        WriteMember(builder, "expression", PredicateExpression.Render(predicate.Formula), first: true);
        builder.Append('}');
    }

    private static void WritePredicate(StringBuilder builder, LogicFormula formula)
    {
        switch (formula.Kind)
        {
            case LogicFormulaKind.True:
                builder.Append("{\"kind\":\"true\"}");
                return;

            case LogicFormulaKind.False:
                builder.Append("{\"kind\":\"false\"}");
                return;

            case LogicFormulaKind.Comparison:
                WriteComparison(
                    builder,
                    formula.Resource!,
                    formula.ComparisonOperator,
                    formula.Literal!);
                return;

            case LogicFormulaKind.Not:
                builder.Append("{\"kind\":\"not\",\"predicate\":");
                WritePredicate(builder, formula.Operands[0]);
                builder.Append('}');
                return;

            case LogicFormulaKind.All:
            case LogicFormulaKind.Any:
                builder.Append(formula.Kind == LogicFormulaKind.All
                    ? "{\"kind\":\"all\",\"predicates\":["
                    : "{\"kind\":\"any\",\"predicates\":[");
                for (var i = 0; i < formula.Operands.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    WritePredicate(builder, formula.Operands[i]);
                }

                builder.Append("]}");
                return;

            case LogicFormulaKind.BooleanAtom:
            case LogicFormulaKind.Opaque:
            default:
                // Unreachable: ProofCatalogProjection.DescribeUnexportable refuses
                // every contract carrying one of these before the writer is called.
                // The guard is here so the two stay one rule — a kind added to the
                // formula algebra and not to the gate stops the build rather than
                // being written as something it is not.
                throw new InvalidOperationException(
                    $"Predicate kind '{formula.Kind}' is not exportable and must have been "
                    + "refused before the proof catalog writer ran.");
        }
    }

    private static void WriteComparison(
        StringBuilder builder,
        LogicResource resource,
        LogicComparisonOperator comparison,
        LogicLiteral literal)
    {
        builder.Append("{\"kind\":\"property-comparison\",\"property\":{");
        WriteMember(builder, "name", resource.Key, first: true);
        WriteMember(builder, "scalarKind", ScalarKindToken(resource.ScalarKind), first: false);
        builder.Append(",\"isNullable\":").Append(resource.IsNullable ? "true" : "false");
        if (resource.ScalarTypeName is { } typeName)
        {
            WriteMember(builder, "enumTypeName", typeName, first: false);
        }

        builder.Append("},");
        WriteMember(builder, "operator", OperatorToken(comparison), first: true);
        builder.Append(",\"value\":");
        WriteLiteral(builder, literal);
        builder.Append('}');
    }

    private static void WriteLiteral(StringBuilder builder, LogicLiteral literal)
    {
        switch (literal.Kind)
        {
            case LogicLiteralKind.Null:
                builder.Append("{\"kind\":\"null\"}");
                return;

            case LogicLiteralKind.Boolean:
                builder.Append("{\"kind\":\"boolean\",\"value\":")
                    .Append(string.Equals(literal.CanonicalValue, "true", StringComparison.Ordinal)
                        ? "true"
                        : "false")
                    .Append('}');
                return;

            case LogicLiteralKind.Enum:
                builder.Append("{\"kind\":\"enum\",");
                WriteMember(builder, "typeName", literal.ScalarTypeName!, first: true);
                WriteMember(builder, "memberName", literal.CanonicalValue, first: false);
                builder.Append('}');
                return;

            case LogicLiteralKind.Integer:
            case LogicLiteralKind.Decimal:
            case LogicLiteralKind.String:
            case LogicLiteralKind.Symbol:
                // Integer and decimal values travel as STRINGS on the wire: a JSON
                // number is a double in most readers, and an exact ontology value
                // that round-trips through one is no longer the value that was
                // declared. The canonical string is what the solver compares.
                builder.Append("{\"kind\":\"")
                    .Append(LiteralKindToken(literal.Kind))
                    .Append("\",");
                WriteMember(builder, "value", literal.CanonicalValue, first: true);
                builder.Append('}');
                return;

            default:
                throw new InvalidOperationException(
                    $"Unhandled literal kind '{literal.Kind}' in the proof catalog writer.");
        }
    }

    private static void WriteDomainLattice(StringBuilder builder, OntologyAuthorityLattice lattice)
    {
        builder.Append('{');
        WriteMember(builder, "domainName", lattice.DomainName, first: true);
        builder.Append(",\"lattice\":{\"axes\":[");

        var axes = lattice.Axes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToImmutableArray();
        for (var i = 0; i < axes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append('{');
            WriteMember(builder, "name", axes[i].Key, first: true);
            builder.Append(",\"levels\":[");
            for (var level = 0; level < axes[i].Value.Length; level++)
            {
                if (level > 0)
                {
                    builder.Append(',');
                }

                WriteString(builder, axes[i].Value[level]);
            }

            builder.Append("]}");
        }

        builder.Append("],\"authorities\":[");
        var authorities = lattice.Authorities
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToImmutableArray();
        for (var i = 0; i < authorities.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append('{');
            WriteMember(builder, "name", authorities[i].Key, first: true);
            builder.Append(",\"coordinates\":[");
            var coordinates = authorities[i].Value
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToImmutableArray();
            for (var c = 0; c < coordinates.Length; c++)
            {
                if (c > 0)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                WriteMember(builder, "axis", coordinates[c].Key, first: true);
                WriteMember(builder, "level", coordinates[c].Value, first: false);
                builder.Append('}');
            }

            builder.Append("],\"explicitImplications\":[]}");
        }

        builder.Append("]}}");
    }

    private static void WriteMember(StringBuilder builder, string name, string value, bool first)
    {
        if (!first)
        {
            builder.Append(',');
        }

        WriteString(builder, name);
        builder.Append(':');
        WriteString(builder, value);
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    internal static string ScalarKindToken(LogicScalarKind kind) => kind switch
    {
        LogicScalarKind.Boolean => "boolean",
        LogicScalarKind.Integer => "integer",
        LogicScalarKind.Decimal => "decimal",
        LogicScalarKind.String => "string",
        LogicScalarKind.Enum => "enum",
        LogicScalarKind.Symbol => "symbol",
        _ => throw new InvalidOperationException($"Unhandled scalar kind '{kind}'."),
    };

    internal static string LiteralKindToken(LogicLiteralKind kind) => kind switch
    {
        LogicLiteralKind.Null => "null",
        LogicLiteralKind.Boolean => "boolean",
        LogicLiteralKind.Integer => "integer",
        LogicLiteralKind.Decimal => "decimal",
        LogicLiteralKind.String => "string",
        LogicLiteralKind.Enum => "enum",
        LogicLiteralKind.Symbol => "symbol",
        _ => throw new InvalidOperationException($"Unhandled literal kind '{kind}'."),
    };

    internal static string OperatorToken(LogicComparisonOperator comparison) => comparison switch
    {
        LogicComparisonOperator.Equal => "equal",
        LogicComparisonOperator.NotEqual => "not-equal",
        LogicComparisonOperator.LessThan => "less-than",
        LogicComparisonOperator.LessThanOrEqual => "less-than-or-equal",
        LogicComparisonOperator.GreaterThan => "greater-than",
        LogicComparisonOperator.GreaterThanOrEqual => "greater-than-or-equal",
        _ => throw new InvalidOperationException($"Unhandled comparison operator '{comparison}'."),
    };
}
