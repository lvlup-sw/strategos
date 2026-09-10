using System.Reflection;
using Strategos.Ontology.Npgsql.Internal;

namespace Strategos.Ontology.Npgsql.Tests.Internal;

public class TypeMapperTests
{
    // -----------------------------------------------------------------------
    // Track E5 — TypeMapper.GetTableName<T>() removal guard.
    //
    // After F4 and F5 land, the graph-backed dispatch helpers on
    // PgVectorObjectSetProvider handle all write-path table-name resolution,
    // and the legacy generic GetTableName<T>() footgun — which silently
    // collapsed to typeof(T).Name and routed writes to the wrong physical
    // table when a CLR type was registered under multiple descriptors
    // (bug #31) — can be removed. This reflection guard fails loudly if a
    // future change re-introduces it.
    // -----------------------------------------------------------------------

    [Test]
    public async Task TypeMapper_GetTableName_Of_T_Is_Removed()
    {
        var method = typeof(TypeMapper)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "GetTableName" && m.IsGenericMethod);

        await Assert.That(method).IsNull();
    }

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
        // Consecutive acronyms are kept together: "xmlhttp_request"
        await Assert.That(result).IsEqualTo("xmlhttp_request");
    }

    // Note: the prior GetTableName_ReturnsSnakeCasedTypeName /
    // GetTableName_SimpleType_ReturnsLowered tests were removed alongside
    // TypeMapper.GetTableName<T>() itself (E5). Callers now route through
    // PgVectorObjectSetProvider.ResolveTableNameForDefaultOverload<T>
    // (graph-backed with a typeof(T).Name fallback) or
    // ResolveTableNameForDescriptor (explicit-name writes).
}
