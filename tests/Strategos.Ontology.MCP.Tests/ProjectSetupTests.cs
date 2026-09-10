namespace Strategos.Ontology.MCP.Tests;

public class ProjectSetupTests
{
    [Test]
    public async Task Build_StrategosOntologyMcpProject_Compiles()
    {
        // Arrange - reference a type to ensure assembly is loaded
        var markerType = typeof(Strategos.Ontology.MCP.AssemblyMarker);
        var assembly = markerType.Assembly;

        // Act
        var assemblyName = assembly.GetName().Name;

        // Assert - Strategos.Ontology.MCP assembly is loadable
        await Assert.That(assemblyName).IsEqualTo("Strategos.Ontology.MCP");
    }
}
