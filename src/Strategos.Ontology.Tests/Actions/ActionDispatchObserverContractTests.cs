using System.Reflection;
using Strategos.Ontology.Actions;

namespace Strategos.Ontology.Tests.Actions;

public class ActionDispatchObserverContractTests
{
    [Test]
    public async Task IActionDispatchObserver_TypeShape_HasOnDispatchedAsync()
    {
        var type = typeof(IActionDispatchObserver);

        await Assert.That(type.IsInterface).IsTrue();

        var method = type.GetMethod(
            "OnDispatchedAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(ActionContext), typeof(ActionResult), typeof(CancellationToken)],
            modifiers: null);

        await Assert.That(method).IsNotNull();
        await Assert.That(method!.ReturnType).IsEqualTo(typeof(Task));
    }
}
