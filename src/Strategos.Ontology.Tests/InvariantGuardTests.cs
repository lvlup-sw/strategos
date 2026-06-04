using System.Reflection;

using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

/// <summary>
/// Standing structural regression guards for the Ontology Edge Foundation
/// load-bearing invariants. These execute by reflecting over the shipped
/// <c>Strategos.Ontology</c> assembly, so they fail the build the moment a
/// future change erodes an invariant rather than at some distant integration
/// point.
/// </summary>
public class InvariantGuardTests
{
    private static Assembly OntologyAssembly => typeof(ObjectTypeDescriptor).Assembly;

    /// <summary>
    /// INV-2 (self-contained): the ontology core must not take a dependency
    /// on Wolverine or Marten. The whole point of the projector living in
    /// <c>Strategos.Ontology</c> is that identity resolution carries no
    /// messaging/persistence baggage.
    /// </summary>
    [Test]
    public async Task Ontology_Assembly_ReferencesNoWolverineOrMarten()
    {
        var offenders = OntologyAssembly.GetReferencedAssemblies()
            .Where(a => a.Name is not null
                && (a.Name.StartsWith("Wolverine", StringComparison.OrdinalIgnoreCase)
                    || a.Name.StartsWith("Marten", StringComparison.OrdinalIgnoreCase)))
            .Select(a => a.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty();
    }

    /// <summary>
    /// INV-6 (sealed): every public concrete type in the
    /// <c>Strategos.Ontology.Identity</c> namespace must be sealed. Interfaces
    /// are exempt — they are non-instantiable and sealing is inapplicable to
    /// them — but no open class/struct is permitted in the identity surface.
    /// </summary>
    [Test]
    public async Task IdentityTypes_AreSealed()
    {
        var unsealed = IdentityPublicTypes()
            .Where(t => !t.IsInterface)
            .Where(t => !t.IsSealed)
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(unsealed).IsEmpty();
    }

    /// <summary>
    /// INV-8 (polyglot identity, structural half): no public member of any
    /// identity type may traffic in <see cref="System.Type"/>. Surfacing a
    /// <see cref="System.Type"/> would invite a reflection-based key lookup,
    /// which is exactly the per-call reflection INV-8 forbids.
    /// </summary>
    [Test]
    public async Task IdentityApi_ExposesNoSystemTypeMembers()
    {
        var offenders = new List<string>();

        foreach (var type in IdentityPublicTypes())
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

            foreach (var method in type.GetMethods(flags))
            {
                // Skip inherited Object members (ToString/Equals/GetHashCode/GetType).
                if (method.DeclaringType != type)
                {
                    continue;
                }

                if (method.ReturnType == typeof(Type))
                {
                    offenders.Add($"{type.FullName}.{method.Name} returns System.Type");
                }

                foreach (var p in method.GetParameters())
                {
                    if (p.ParameterType == typeof(Type))
                    {
                        offenders.Add($"{type.FullName}.{method.Name}({p.Name}) takes System.Type");
                    }
                }
            }

            foreach (var prop in type.GetProperties(flags).Where(p => p.DeclaringType == type))
            {
                if (prop.PropertyType == typeof(Type))
                {
                    offenders.Add($"{type.FullName}.{prop.Name} is System.Type");
                }
            }

            foreach (var ctor in type.GetConstructors(flags))
            {
                foreach (var p in ctor.GetParameters())
                {
                    if (p.ParameterType == typeof(Type))
                    {
                        offenders.Add($"{type.FullName} ctor({p.Name}) takes System.Type");
                    }
                }
            }
        }

        await Assert.That(offenders).IsEmpty();
    }

    private static IEnumerable<Type> IdentityPublicTypes() =>
        OntologyAssembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == "Strategos.Ontology.Identity");
}
