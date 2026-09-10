using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetTypesTests
{
    [Test]
    public async Task ObjectSetInclusion_Flags_CanBeCombined()
    {
        // Arrange & Act
        var schema = ObjectSetInclusion.Schema;

        // Assert
        await Assert.That(schema).IsEqualTo(
            ObjectSetInclusion.Properties |
            ObjectSetInclusion.Actions |
            ObjectSetInclusion.Links |
            ObjectSetInclusion.Interfaces);
    }

    [Test]
    public async Task ObjectSetInclusion_Full_IncludesAll()
    {
        // Arrange & Act
        var full = ObjectSetInclusion.Full;

        // Assert
        await Assert.That(full).IsEqualTo(
            ObjectSetInclusion.Schema |
            ObjectSetInclusion.Events |
            ObjectSetInclusion.LinkedObjects);
    }

    [Test]
    public async Task ObjectSetResult_Create_HasItemsAndTotalCount()
    {
        // Arrange
        var items = new List<string> { "a", "b", "c" };

        // Act
        var result = new ObjectSetResult<string>(items, 3, ObjectSetInclusion.Properties);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(3);
        await Assert.That(result.TotalCount).IsEqualTo(3);
        await Assert.That(result.Inclusion).IsEqualTo(ObjectSetInclusion.Properties);
    }

    [Test]
    public async Task ObjectSetResult_Empty_HasZeroItems()
    {
        // Arrange & Act
        var result = new ObjectSetResult<string>([], 0, ObjectSetInclusion.Properties);

        // Assert
        await Assert.That(result.Items).HasCount().EqualTo(0);
        await Assert.That(result.TotalCount).IsEqualTo(0);
    }
}
