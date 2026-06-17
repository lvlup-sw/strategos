using System.Reflection;

namespace Strategos.Ontology.Npgsql.Tests;

/// <summary>
/// Standing structural regression guard for INV-6 (sealed-by-default) over the
/// new edge/provider plan types introduced by the v2.9.0 edge-layer completion
/// (T9/T10/T11). Mirrors <c>EdgeStoreTypes_AreSealed</c> in
/// <c>Strategos.Ontology.Tests/InvariantGuardTests.cs</c>: it reflects over the
/// shipped <c>Strategos.Ontology.Npgsql</c> assembly so the build fails the
/// moment a future edit unseals one of these resolver-plan records, rather than
/// at some distant integration point.
/// </summary>
/// <remarks>
/// These types are <c>internal</c> records nested inside
/// <see cref="PgVectorObjectSetProvider"/>; they are reachable for reflection
/// because the provider grants <c>InternalsVisibleTo</c> to this test assembly.
/// Their reflection <see cref="System.Type.FullName"/> uses the <c>+</c> nested-type
/// separator. <c>EndpointCardinality</c> (T4) is deliberately excluded — it is an
/// <c>enum</c> and lives in the core <c>Strategos.Ontology</c> assembly, not here;
/// enums are not sealable. The expanded <c>SqlGenerator</c> (T8–T11) is a
/// <c>static class</c> exposing no instantiable reference types, so it has no
/// sealable surface to guard.
/// </remarks>
public class EdgeProviderTypesSealedTests
{
    private static Assembly NpgsqlAssembly => typeof(PgVectorObjectSetProvider).Assembly;

    /// <summary>
    /// INV-6 (sealed), DR-4/DR-7/DR-10 provider surface: the resolved-plan
    /// records the edge provider feeds into the SQL builders must be sealed.
    /// Resolved by reflection name (nested-type <c>+</c> form) so the
    /// <c>internal</c> records are covered. Interfaces are exempt — sealing is
    /// inapplicable to them — and any unsealed offender is collected by name.
    /// </summary>
    [Test]
    public async Task EdgeProviderTypes_AreSealed()
    {
        var names = new[]
        {
            // RelateEndpoint (T9): resolved relate/unrelate endpoint operands.
            "Strategos.Ontology.Npgsql.PgVectorObjectSetProvider+RelateEndpoint",
            // TraversalHop (T10): resolved instance-anchored traversal join operands.
            "Strategos.Ontology.Npgsql.PgVectorObjectSetProvider+TraversalHop",
            // AssociationRelatePlan (T11): resolved attributed-relate plan.
            "Strategos.Ontology.Npgsql.PgVectorObjectSetProvider+AssociationRelatePlan",
            // OntologySchemaIdentifierException (DR-11/T1): typed collision error
            // for the 63-byte identifier guard.
            "Strategos.Ontology.Npgsql.Schema.OntologySchemaIdentifierException",
        };

        var offenders = new List<string>();
        foreach (var name in names)
        {
            var type = NpgsqlAssembly.GetType(name);
            if (type is null)
            {
                // Not-found is an offense, not a silent pass: a renamed or
                // removed type must surface here, never weaken the guard.
                offenders.Add($"{name} (type not found)");
                continue;
            }

            if (!type.IsInterface && !type.IsSealed)
            {
                offenders.Add($"{type.FullName} is not sealed");
            }
        }

        await Assert.That(offenders).IsEmpty();
    }
}
