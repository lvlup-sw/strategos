// Copyright (c) Levelup Software. All rights reserved.

namespace Strategos.Ontology.Tests.Actions;

public class ConstraintEvaluationLocationTests
{
    [Test]
    public async Task ConstraintEvaluation_LivesInActionsNamespace()
    {
        // The canonical home of ConstraintEvaluation is Strategos.Ontology.Actions.
        var ontologyAssembly = System.Reflection.Assembly.Load("Strategos.Ontology");

        var actionsType = ontologyAssembly.GetType(
            "Strategos.Ontology.Actions.ConstraintEvaluation",
            throwOnError: false);

        await Assert.That(actionsType).IsNotNull();
        await Assert.That(actionsType!.Namespace).IsEqualTo("Strategos.Ontology.Actions");
    }
}
