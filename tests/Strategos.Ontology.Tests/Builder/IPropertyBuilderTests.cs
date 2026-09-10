using NSubstitute;
using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests.Builder;

public class IPropertyBuilderTests
{
    [Test]
    public async Task IPropertyBuilder_Required_ReturnsSelf()
    {
        // Arrange
        var substitute = Substitute.For<IPropertyBuilder>();
        substitute.Required().Returns(substitute);

        // Act
        var result = substitute.Required();

        // Assert
        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IPropertyBuilder_Computed_ReturnsSelf()
    {
        // Arrange
        var substitute = Substitute.For<IPropertyBuilder>();
        substitute.Computed().Returns(substitute);

        // Act
        var result = substitute.Computed();

        // Assert
        await Assert.That(result).IsEqualTo(substitute);
    }
}
