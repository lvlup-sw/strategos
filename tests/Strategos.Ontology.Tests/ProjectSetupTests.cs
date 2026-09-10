namespace Strategos.Ontology.Tests;

public class ProjectSetupTests
{
    [Test]
    public async Task Build_StrategosOntologyProject_Compiles()
    {
        // Arrange - reference a type to ensure assembly is loaded
        var markerType = typeof(Strategos.Ontology.AssemblyMarker);
        var assembly = markerType.Assembly;

        // Act
        var assemblyName = assembly.GetName().Name;

        // Assert - Strategos.Ontology assembly is loadable
        await Assert.That(assemblyName).IsEqualTo("Strategos.Ontology");
    }
}
