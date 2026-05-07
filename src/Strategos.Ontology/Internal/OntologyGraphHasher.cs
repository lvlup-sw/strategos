using System.Security.Cryptography;
using System.Text;

namespace Strategos.Ontology.Internal;

/// <summary>
/// Computes a deterministic SHA-256 hash over the structural fields of an
/// <see cref="OntologyGraph"/>. The hash is exposed via <see cref="OntologyGraph.Version"/>
/// and surfaced (with a "sha256:" prefix added at the wire-emission boundary)
/// as the <c>_meta.ontologyVersion</c> field on MCP tool responses so consumers
/// can invalidate cached schema views on mismatch.
/// </summary>
internal static class OntologyGraphHasher
{
    public static string ComputeVersion(OntologyGraph graph)
    {
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            WriteStableHeader(writer, graph);
        }

        var hash = SHA256.HashData(ms.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    private static void WriteStableHeader(BinaryWriter writer, OntologyGraph graph)
    {
        writer.Write("DOMAINS|");
        var domainNames = graph.Domains.Select(d => d.DomainName).OrderBy(n => n, StringComparer.Ordinal);
        foreach (var name in domainNames)
        {
            WriteString(writer, name);
        }

        writer.Write("|END_DOMAINS");
    }

    private static void WriteString(BinaryWriter writer, string s)
    {
        // Length-prefixed UTF-8 framing prevents accidental cross-field
        // collisions where adjacent fields could otherwise concatenate to the
        // same byte sequence under different splits.
        var bytes = Encoding.UTF8.GetBytes(s);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
