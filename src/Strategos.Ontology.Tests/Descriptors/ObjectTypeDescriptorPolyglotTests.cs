using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class ObjectTypeDescriptorPolyglotTests
{
    [Test]
    public async Task Ctor_HandAuthoredDefault_LanguageIdIsDotnet()
    {
        // Arrange & Act — positional ctor preserved for backward compat
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        // Assert
        await Assert.That(descriptor.LanguageId).IsEqualTo("dotnet");
    }

    [Test]
    public async Task Ctor_HandAuthoredDefault_SourceIsHandAuthored()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.Source).IsEqualTo(DescriptorSource.HandAuthored);
    }

    [Test]
    public async Task Ctor_HandAuthoredDefault_SymbolKeyAndFqnAreNull()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.SymbolKey).IsNull();
        await Assert.That(descriptor.SymbolFqn).IsNull();
        await Assert.That(descriptor.SourceId).IsNull();
        await Assert.That(descriptor.IngestedAt).IsNull();
    }

    [Test]
    public async Task Ctor_IngestedDescriptor_AcceptsNullClrTypeWithSymbolKey()
    {
        // Property-init form — ingested descriptor with no CLR type, only a SymbolKey
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "User",
            DomainName = "Identity",
            ClrType = null,
            SymbolKey = "scip-typescript . ./src/user.ts#User",
            SymbolFqn = "user.User",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = "marten-typescript",
            IngestedAt = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero),
        };

        await Assert.That(descriptor.ClrType).IsNull();
        await Assert.That(descriptor.SymbolKey).IsEqualTo("scip-typescript . ./src/user.ts#User");
        await Assert.That(descriptor.SymbolFqn).IsEqualTo("user.User");
        await Assert.That(descriptor.LanguageId).IsEqualTo("typescript");
        await Assert.That(descriptor.Source).IsEqualTo(DescriptorSource.Ingested);
        await Assert.That(descriptor.SourceId).IsEqualTo("marten-typescript");
        await Assert.That(descriptor.IngestedAt).IsNotNull();
    }

    [Test]
    public async Task Ctor_BothClrTypeAndSymbolKeyNull_ThrowsInvalidOperationException()
    {
        // Hand-authored polyglot escape: no CLR type and no SymbolKey violates DR-1
        InvalidOperationException? caught = null;
        try
        {
            _ = new ObjectTypeDescriptor
            {
                Name = "Orphan",
                DomainName = "Trading",
                ClrType = null,
                SymbolKey = null,
            };
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Message).Contains("ClrType");
        await Assert.That(caught.Message).Contains("SymbolKey");
    }

    [Test]
    public async Task Ctor_PositionalForm_ProvidesClrType_DoesNotThrow()
    {
        // Backward-compat: positional ctor always carries a non-null ClrType
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.ClrType).IsEqualTo(typeof(string));
    }
}
