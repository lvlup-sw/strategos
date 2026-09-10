namespace Strategos.Ontology.Generators.Tests;

public class ProjectSetupTests
{
    [Test]
    public async Task Build_StrategosOntologyGeneratorsProject_Compiles()
    {
        // Arrange - reference a type to ensure assembly is loaded
        var markerType = typeof(Strategos.Ontology.Generators.AssemblyMarker);
        var assembly = markerType.Assembly;

        // Act
        var assemblyName = assembly.GetName().Name;

        // Assert - Strategos.Ontology.Generators assembly is loadable
        await Assert.That(assemblyName).IsEqualTo("Strategos.Ontology.Generators");
    }
}
