using System.Reflection;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Structural regression guard for the core <c>Strategos.Ontology.MCP</c> assembly
/// (F8). INV-6 (sealed-by-default): every concrete class the assembly defines —
/// public or not — must be sealed unless it is explicitly designed for inheritance.
/// Static classes (constants/limits holders), abstract base records (the
/// <see cref="QueryResultUnion"/> discriminated base), and base records that another
/// type in the assembly extends (<c>QueryResult</c>, the base of
/// <see cref="SemanticQueryResult"/>) are exempt; sealing is inapplicable to them.
/// This mirrors the Hosting bridge's <c>HostingInvariantGuardTests</c> and fails the
/// build the moment a future edit adds an unsealed concrete leaf to the MCP surface
/// — covering the DR-15 traversal result shapes (<see cref="TraversalResult"/>,
/// <see cref="TraversalEndpoint"/>, <see cref="TraversalRequest"/>) and the
/// edge/association branches (<see cref="AssociationQueryResult"/>,
/// <see cref="AssociationEdgeRow"/>, and the <see cref="QueryResultUnion"/> derived
/// branches).
/// </summary>
public sealed class McpInvariantGuardTests
{
    private static Assembly McpAssembly => typeof(TraversalResult).Assembly;

    [Test]
    public async Task McpConcreteLeaves_AreSealed()
    {
        var types = McpAssembly.GetTypes();

        // Types that serve as a base for another type in this assembly are
        // intentionally open for inheritance (e.g. QueryResult, extended by
        // SemanticQueryResult). Exempt them — only the leaves must be sealed.
        var baseTypes = types
            .Select(t => t.BaseType)
            .Where(b => b is not null && b.Assembly == McpAssembly)
            .ToHashSet();

        var unsealed = types
            .Where(t => t.IsClass)
            // Static classes surface as abstract+sealed; the C# compiler forbids the
            // `sealed` modifier on them, so they are not candidates for this guard.
            .Where(t => !(t.IsAbstract && t.IsSealed))
            // Abstract base types (the QueryResultUnion discriminated base) are
            // designed for inheritance by their derived branches; exempt them.
            .Where(t => !t.IsAbstract)
            // A type extended by another type in the assembly is an inheritance base
            // by design (QueryResult <- SemanticQueryResult); exempt it.
            .Where(t => !baseTypes.Contains(t))
            // Compiler-generated closures/iterators (display classes, async/iterator
            // state machines) are not authored types.
            .Where(t => t.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null)
            .Where(t => !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(unsealed).IsEmpty();
    }

    [Test]
    public async Task Dr15ResultShapes_AreSealed()
    {
        // The DR-15 traversal/edge result shapes are explicitly named so a rename
        // that drops one from the sweep above is still caught here. Each is a sealed
        // concrete leaf.
        var sealedShapes = new[]
        {
            typeof(AssociationQueryResult),
            typeof(AssociationEdgeRow),
            typeof(TraversalResult),
            typeof(TraversalEndpoint),
            typeof(TraversalRequest),
        };

        foreach (var shape in sealedShapes)
        {
            await Assert.That(shape.IsSealed).IsTrue();
        }
    }

    [Test]
    public async Task QueryResultUnion_BranchPosture_Holds()
    {
        // The union base is the inheritance seam: abstract, never sealed.
        await Assert.That(typeof(QueryResultUnion).IsAbstract).IsTrue();
        await Assert.That(typeof(QueryResultUnion).IsSealed).IsFalse();

        // Its sealed leaf branches.
        await Assert.That(typeof(AssociationQueryResult).IsSealed).IsTrue();
        await Assert.That(typeof(SemanticQueryResult).IsSealed).IsTrue();

        // QueryResult is itself extended by SemanticQueryResult, so it is the one
        // intentionally-open branch — not sealed, by design.
        await Assert.That(typeof(SemanticQueryResult).BaseType).IsEqualTo(typeof(QueryResult));
        await Assert.That(typeof(QueryResult).IsSealed).IsFalse();
    }
}
