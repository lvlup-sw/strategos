// =============================================================================
// <copyright file="ConstraintStrengthTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Tests;

// --- Test domain models for constraint strength tests ---

public enum ConstraintTestStatus
{
    Pending,
    Active,
    Complete,
}

public record ConstraintOrder(
    Guid Id,
    string Symbol,
    decimal Quantity,
    ConstraintTestStatus Status);

public record ConstraintTarget(Guid Id, string Name);

public class ConstraintStrengthOntology : DomainOntology
{
    public override string DomainName => "test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ConstraintTarget>(obj =>
        {
            obj.Key(t => t.Id);
            obj.Property(t => t.Name);
        });

        builder.Object<ConstraintOrder>(obj =>
        {
            obj.Key(o => o.Id);
            obj.Property(o => o.Symbol).Required();
            obj.Property(o => o.Quantity);
            obj.Property(o => o.Status);
            obj.HasMany<ConstraintTarget>("Targets");

            // Hard constraint: must be Active
            obj.Action("Execute")
                .Description("Execute the order")
                .Requires(o => o.Status == ConstraintTestStatus.Active)
                .RequiresLink("Targets");

            // Soft constraint: prefer Quantity > 0 but don't block
            obj.Action("Review")
                .Description("Review the order")
                .RequiresSoft(o => o.Quantity > 0)
                .RequiresLinkSoft("Targets");

            // Mixed: hard Status check, soft Quantity check
            obj.Action("Modify")
                .Description("Modify the order")
                .Requires(o => o.Status == ConstraintTestStatus.Active)
                .RequiresSoft(o => o.Quantity > 0);
        });
    }
}

// --- Tests ---

public class ConstraintStrengthTests
{
    [Test]
    public async Task RequiresSoft_SetsConstraintStrengthToSoft()
    {
        var builder = new ActionBuilder<ConstraintOrder>("Review");
        builder.RequiresSoft(o => o.Quantity > 0);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions[0].Strength).IsEqualTo(ConstraintStrength.Soft);
    }

    [Test]
    public async Task Requires_DefaultStrength_IsHard()
    {
        var builder = new ActionBuilder<ConstraintOrder>("Execute");
        builder.Requires(o => o.Status == ConstraintTestStatus.Active);

        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions[0].Strength).IsEqualTo(ConstraintStrength.Hard);
    }

    [Test]
    public async Task RequiresLinkSoft_SetsConstraintStrengthToSoft()
    {
        var builder = new ActionBuilder<ConstraintOrder>("Review");
        builder.RequiresLinkSoft("Targets");

        var descriptor = builder.Build();

        await Assert.That(descriptor.Preconditions[0].Strength).IsEqualTo(ConstraintStrength.Soft);
    }

    [Test]
    public async Task GetValidActions_SoftConstraintUnsatisfied_StillIncludesAction()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<ConstraintStrengthOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        // Quantity = 0, no Targets -- soft constraints unsatisfied
        var knownProps = new Dictionary<string, object?>
        {
            ["Quantity"] = 0m,
            // No "Targets" -- soft link constraint also unsatisfied
        };

        var actions = query.GetValidActions("ConstraintOrder", knownProps);
        var names = actions.Select(a => a.Name).ToList();

        // Review should still be included because its constraints are all soft
        await Assert.That(names).Contains("Review");
    }

    [Test]
    public async Task GetValidActions_HardConstraintUnsatisfied_FiltersAction()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<ConstraintStrengthOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        // Status is Pending, no Targets -- hard constraints unsatisfied
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = ConstraintTestStatus.Pending,
        };

        var actions = query.GetValidActions("ConstraintOrder", knownProps);
        var names = actions.Select(a => a.Name).ToList();

        // Execute requires hard Status==Active, which is not satisfied
        await Assert.That(names).DoesNotContain("Execute");
    }

    [Test]
    public async Task GetValidActions_MixedConstraints_OnlyHardFilters()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<ConstraintStrengthOntology>();
        var graph = graphBuilder.Build();
        var query = new OntologyQueryService(graph);

        // Status Active (hard satisfied), Quantity = 0 (soft unsatisfied)
        var knownProps = new Dictionary<string, object?>
        {
            ["Status"] = ConstraintTestStatus.Active,
            ["Quantity"] = 0m,
        };

        var actions = query.GetValidActions("ConstraintOrder", knownProps);
        var names = actions.Select(a => a.Name).ToList();

        // Modify: hard Status==Active (satisfied), soft Quantity>0 (unsatisfied but ignored)
        await Assert.That(names).Contains("Modify");
    }
}
