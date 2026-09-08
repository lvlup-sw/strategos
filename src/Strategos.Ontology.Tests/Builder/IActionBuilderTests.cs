using System.Linq.Expressions;
using System.Reflection;
using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class IActionBuilderTests
{
    [Test]
    public async Task IActionBuilder_Description_ReturnsSelf()
    {
        var substitute = Substitute.For<IActionBuilder>();
        substitute.Description("desc").Returns(substitute);

        var result = substitute.Description("desc");

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IActionBuilder_Accepts_ReturnsSelf()
    {
        var substitute = Substitute.For<IActionBuilder>();
        substitute.Accepts<string>().Returns(substitute);

        var result = substitute.Accepts<string>();

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IActionBuilder_Returns_ReturnsSelf()
    {
        var substitute = Substitute.For<IActionBuilder>();
        substitute.Returns<string>().Returns(substitute);

        var result = substitute.Returns<string>();

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IActionBuilder_BoundToWorkflow_ReturnsSelf()
    {
        var substitute = Substitute.For<IActionBuilder>();
        substitute.BoundToWorkflow("execute-trade").Returns(substitute);

        var result = substitute.BoundToWorkflow("execute-trade");

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IActionBuilder_BoundToWorkflowReference_ReturnsSelf()
    {
        var substitute = Substitute.For<IActionBuilder>();
        var workflow = new WorkflowBindingReference("execute-trade");
        substitute.BoundToWorkflow(workflow).Returns(substitute);

        var result = substitute.BoundToWorkflow(workflow);

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task IActionBuilder_BoundToTool_ReturnsSelf()
    {
        var substitute = Substitute.For<IActionBuilder>();
        substitute.BoundToTool("tool", "method").Returns(substitute);

        var result = substitute.BoundToTool("tool", "method");

        await Assert.That(result).IsEqualTo(substitute);
    }

    [Test]
    public async Task Requires_ExpressionOverload_IsSupportedAndNotObsolete()
    {
        var method = typeof(IActionBuilder<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m =>
                m.Name == nameof(IActionBuilder<object>.Requires)
                && m.GetParameters()[0].ParameterType.IsGenericType
                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>));

        var obsolete = method.GetCustomAttribute<ObsoleteAttribute>();

        await Assert.That(obsolete).IsNull();
    }
}
