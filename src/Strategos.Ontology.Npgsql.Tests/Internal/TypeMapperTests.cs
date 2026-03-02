using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests.Internal;

public class TypeMapperTests
{
    [Test]
    public async Task ToSnakeCase_PascalCase_ConvertsCorrectly()
    {
        var result = TypeMapper.ToSnakeCase("DocumentChunk");
        await Assert.That(result).IsEqualTo("document_chunk");
    }

    [Test]
    public async Task ToSnakeCase_SingleWord_Lowercases()
    {
        var result = TypeMapper.ToSnakeCase("Document");
        await Assert.That(result).IsEqualTo("document");
    }

    [Test]
    public async Task ToSnakeCase_Acronym_HandlesCorrectly()
    {
        var result = TypeMapper.ToSnakeCase("HTTPClient");
        await Assert.That(result).IsEqualTo("http_client");
    }

    [Test]
    public async Task ToSnakeCase_MultipleWords_ConvertsAll()
    {
        var result = TypeMapper.ToSnakeCase("MyLongClassName");
        await Assert.That(result).IsEqualTo("my_long_class_name");
    }

    [Test]
    public async Task ToSnakeCase_AlreadyLower_NoChange()
    {
        var result = TypeMapper.ToSnakeCase("document");
        await Assert.That(result).IsEqualTo("document");
    }

    [Test]
    public async Task ToSnakeCase_EmptyString_ReturnsEmpty()
    {
        var result = TypeMapper.ToSnakeCase(string.Empty);
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ToSnakeCase_SingleChar_Lowercases()
    {
        var result = TypeMapper.ToSnakeCase("A");
        await Assert.That(result).IsEqualTo("a");
    }

    [Test]
    public async Task ToSnakeCase_ConsecutiveAcronyms_HandlesBoundary()
    {
        var result = TypeMapper.ToSnakeCase("XMLHTTPRequest");
        // Should handle consecutive acronyms: "xml_http_request"
        await Assert.That(result).IsEqualTo("xmlhttp_request");
    }

    [Test]
    public async Task GetTableName_ReturnsSnakeCasedTypeName()
    {
        var result = TypeMapper.GetTableName<TestDocument>();
        await Assert.That(result).IsEqualTo("test_document");
    }

    [Test]
    public async Task GetTableName_SimpleType_ReturnsLowered()
    {
        var result = TypeMapper.GetTableName<string>();
        await Assert.That(result).IsEqualTo("string");
    }

    private sealed class TestDocument
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
