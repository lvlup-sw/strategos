// -----------------------------------------------------------------------
// <copyright file="OntologyActionCatalogFailClosedTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Proof;

/// <summary>Fail-closed coverage for workflow-bound action discovery.</summary>
[Property("Category", "Integration")]
public sealed class OntologyActionCatalogFailClosedTests
{
    /// <summary>A dynamic binding is inventoried and rejected rather than treated as unbound.</summary>
    [Test]
    public async Task DynamicWorkflowBinding_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(WorkflowNames.Resolve())"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("workflow binding name is dynamic");
    }

    /// <summary>A binding split away from its Action root is deliberately unsupported and fails closed.</summary>
    [Test]
    public async Task BindingThroughIntermediateBuilderLocal_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            string.Empty,
            declaration: "var bound = obj.Action(\"fulfill\"); bound.BoundToWorkflow(\"flow\");"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("not part of one direct Action(...) fluent chain");
    }

    /// <summary>An extension hidden in the action chain cannot be assumed contract-neutral.</summary>
    [Test]
    public async Task UnknownFluentHelper_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".Opaque().BoundToWorkflow(\"flow\")"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("outside the closed action-builder grammar");
    }

    /// <summary>Named Object arguments are resolved by parameter identity, not source position.</summary>
    [Test]
    public async Task NamedObjectArguments_AreProved()
    {
        var diagnostics = BindingDiagnostics(Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: "builder.Object<Order>(configure: obj => { ACTIONS }, name: \"Order\");"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A dynamic ontology object identity cannot silently fall back to the CLR type.</summary>
    [Test]
    public async Task DynamicObjectName_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation:
                "builder.Object<Order>(name: WorkflowNames.Resolve(), configure: obj => { ACTIONS });"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "ontology object name is not a compile-time non-empty string");
    }

    /// <summary>An explicitly null ontology object identity cannot silently fall back to the CLR type.</summary>
    [Test]
    public async Task NullObjectName_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: "builder.Object<Order>(name: null, configure: obj => { ACTIONS });"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "ontology object name is not a compile-time non-empty string");
    }

    /// <summary>A blank ontology object identity cannot participate in exact name-based proof.</summary>
    [Test]
    public async Task BlankObjectName_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: "builder.Object<Order>(name: \"   \" , configure: obj => { ACTIONS });"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "ontology object name is not a compile-time non-empty string");
    }

    /// <summary>A partial ontology may declare DomainName and Define in separate source parts.</summary>
    [Test]
    public async Task PartialOntologyDomainName_AcrossDeclarations_IsProved()
    {
        var diagnostics = BindingDiagnostics(Source(
            ".BoundToWorkflow(\"flow\")",
            partialDomain: true));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A workflow-bound direct descriptor with a dynamic contract is retained as invalid.</summary>
    [Test]
    public async Task DirectDescriptorWithDynamicGuarantee_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            bindingSuffix: string.Empty,
            extraDeclaration: """
                public static class DirectAction
                {
                    private static ActionGuarantee[] Guarantees() => [];

                    public static readonly ActionDescriptor Value = new(
                        new ActionSubject("orders", "Order"),
                        "fulfill",
                        "")
                    {
                        BindingType = ActionBindingType.Workflow,
                        BoundWorkflow = new WorkflowBindingReference("flow"),
                        Ensures = Guarantees(),
                    };
                }
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("action guarantee is dynamic");
    }

    /// <summary>
    /// An unrelated descriptor constructor is not an executable ontology declaration and cannot
    /// satisfy a workflow occurrence merely by sharing its three-name identity.
    /// </summary>
    [Test]
    public async Task UnrootedDirectDescriptors_CannotSatisfyWorkflowOccurrences()
    {
        var diagnostics = BindingDiagnostics(Source(
            bindingSuffix: string.Empty,
            declaration: "obj.Action(\"fulfill\").BoundToWorkflow(\"flow\");",
            terminalActionDeclaration: string.Empty,
            extraDeclaration: """
                public static class DeadCatalogEntries
                {
                    public static readonly ActionDescriptor Leaf = new(
                        new ActionSubject("orders", "Order"), "leaf", "");

                    public static readonly ActionDescriptor Done = new(
                        new ActionSubject("orders", "Order"), "done", "");
                }
                """), "AGWF040");

        await Assert.That(diagnostics).HasCount().EqualTo(2);
        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
            await Assert.That(diagnostic.GetMessage()).Contains("resolving to 0 declarations");
        }

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.GetMessage()))
            .Any(message => message.Contains("orders/Order/leaf", StringComparison.Ordinal));
        await Assert.That(diagnostics.Select(diagnostic => diagnostic.GetMessage()))
            .Any(message => message.Contains("orders/Order/done", StringComparison.Ordinal));
    }

    /// <summary>
    /// SymbolKey-only descriptor actions remain provable when their ownership is explicit in the
    /// runtime registration path rooted at <c>DomainOntology.Define</c>.
    /// </summary>
    [Test]
    public async Task InlineObjectTypeDescriptorActions_AreProved()
    {
        var diagnostics = BindingDiagnostics(InlineDescriptorSource());

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>
    /// A single-assignment local keeps SymbolKey-only descriptor ownership explicit and provable.
    /// </summary>
    [Test]
    public async Task ImmutableLocalObjectTypeDescriptorActions_AreProved()
    {
        var diagnostics = BindingDiagnostics(InlineDescriptorSource(descriptorLocal: true));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>An escaped descriptor local is not treated as an immutable catalog declaration.</summary>
    [Test]
    public async Task EscapedLocalObjectTypeDescriptor_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            descriptorLocal: true,
            escapeDescriptorLocal: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "direct ActionDescriptor workflow binding is not rooted");
    }

    /// <summary>
    /// Catalog discovery binds the actual DomainOntology override instead of a same-named overload,
    /// and therefore does not depend on declaration order.
    /// </summary>
    [Test]
    public async Task DefineOverload_DoesNotHideWorkflowBindings_RegardlessOfSourceOrder()
    {
        const string decoy = "private void Define(int _) { }";

        var before = BindingDiagnostics(Source(
            ".BoundToWorkflow(\"flow\")",
            ontologyMembersBeforeDefine: decoy));
        var after = BindingDiagnostics(Source(
            ".BoundToWorkflow(\"flow\")",
            ontologyMembers: decoy));

        await Assert.That(before).IsEmpty();
        await Assert.That(after).IsEmpty();
    }

    /// <summary>
    /// A valid rooted descriptor binding must activate occurrence resolution, so an omitted
    /// descriptor action reports the exact missing occurrence rather than vacuously skipping proof.
    /// </summary>
    [Test]
    public async Task InlineObjectTypeDescriptorBoundAction_WithMissingOccurrence_ReportsAgwf040()
    {
        var diagnostic = SingleBindingDiagnostic(
            "AGWF040",
            InlineDescriptorSource(includeLeafAction: false));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
        await Assert.That(diagnostic.GetMessage()).Contains("orders/Order/leaf");
        await Assert.That(diagnostic.GetMessage()).Contains("resolving to 0 declarations");
    }

    /// <summary>A rooted descriptor action must use its containing object descriptor's subject.</summary>
    [Test]
    public async Task InlineObjectTypeDescriptorSubjectMismatch_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            boundDomainName: "other-orders"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "does not match containing ObjectTypeDescriptor subject");
    }

    /// <summary>A conditional descriptor registration is not a must-execute catalog member.</summary>
    [Test]
    public async Task ConditionalInlineObjectTypeDescriptorRegistration_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            conditionalRegistration: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "Define method contains conditional or non-linear control flow");
    }

    /// <summary>A conditional descriptor Actions collection cannot establish catalog membership.</summary>
    [Test]
    public async Task ConditionalInlineObjectTypeDescriptorActions_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            conditionalActions: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "Actions collection is dynamic or conditionally populated");
    }

    /// <summary>A descriptor registration rooted through a helper cannot masquerade as direct ownership.</summary>
    [Test]
    public async Task EscapedInlineObjectTypeDescriptorBuilder_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            escapedBuilder: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "direct ActionDescriptor workflow binding is not rooted");
    }

    /// <summary>User collection-initializer code cannot manufacture static descriptor membership.</summary>
    [Test]
    public async Task CustomDescriptorActionsCarrier_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            customActionsCarrier: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "Actions collection is dynamic or conditionally populated");
    }

    /// <summary>BoundWorkflow cannot opt a descriptor into workflow dispatch by itself.</summary>
    [Test]
    public async Task DefaultDescriptorBindingTypeWithWorkflow_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            boundBindingType: null));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "BoundWorkflow is present while BindingType is 'Unbound'");
    }

    /// <summary>A tool discriminator cannot be certified as a workflow implementation.</summary>
    [Test]
    public async Task ToolDescriptorBindingTypeWithWorkflow_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", InlineDescriptorSource(
            boundBindingType: "ActionBindingType.Tool"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains(
            "BoundWorkflow is present while BindingType is 'Tool'");
    }

    /// <summary>Transparent grouping cannot hide contract calls made after the binding.</summary>
    [Test]
    public async Task ParenthesizedPostBindingGuarantee_IsIncludedInProof()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF041", Source(
            string.Empty,
            declaration: """
                (obj.Action("fulfill")
                    .Requires(order => order.Stage == 0)
                    .Modifies(order => order.Stage)
                    .BoundToWorkflow("flow"))
                    .Ensures(order => order.Stage == 2);

                obj.Action("leaf")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage);
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage()).Contains("does not establish the bound guarantee");
    }

    /// <summary>A grouped requirement after BoundToWorkflow still constrains workflow entry.</summary>
    [Test]
    public async Task ParenthesizedPostBindingRequirement_IsIncludedInProof()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF041", Source(
            string.Empty,
            declaration: """
                (obj.Action("fulfill")
                    .Modifies(order => order.Stage)
                    .BoundToWorkflow("flow"))
                    .Requires(order => order.Stage == 1);

                obj.Action("leaf")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage);
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF041");
        await Assert.That(diagnostic.GetMessage()).Contains("does not imply entry step 'Leaf' requirement");
    }

    /// <summary>A grouped frame declaration after BoundToWorkflow is not truncated.</summary>
    [Test]
    public async Task ParenthesizedPostBindingFrame_IsIncludedInProof()
    {
        var diagnostics = BindingDiagnostics(Source(
            string.Empty,
            declaration: """
                (obj.Action("fulfill")
                    .Requires(order => order.Stage == 0)
                    .BoundToWorkflow("flow"))
                    .Modifies(order => order.Stage);

                obj.Action("leaf")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage);
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Relation sugar binds named arguments by parameter identity.</summary>
    [Test]
    public async Task ReorderedNamedRelationArguments_AreProved()
    {
        var diagnostics = BindingDiagnostics(Source(
            string.Empty,
            declaration: """
                obj.Action("fulfill")
                    .RequiresRelation(linkPath: new[] { "portfolio" }, relationName: "owner")
                    .EnsuresRelation(linkPath: new[] { "portfolio" }, relationName: "owner")
                    .Touches(ActionResource.Link("portfolio"))
                    .BoundToWorkflow("flow");

                obj.Action("leaf")
                    .RequiresRelation(linkPath: new[] { "portfolio" }, relationName: "owner")
                    .EnsuresRelation(linkPath: new[] { "portfolio" }, relationName: "owner")
                    .Touches(ActionResource.Link("portfolio"));
                """,
            terminalActionDeclaration: """
                obj.Action("done")
                    .RequiresRelation(linkPath: new[] { "portfolio" }, relationName: "owner")
                    .EnsuresRelation(linkPath: new[] { "portfolio" }, relationName: "owner")
                    .Touches(ActionResource.Link("portfolio"));
                """));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A Define helper cannot hide a workflow binding from compilation-wide inventory.</summary>
    [Test]
    public async Task BindingFactoredThroughOntologyBuilderHelper_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: "Register(builder);",
            ontologyMembers: """
                private static void Register(IOntologyBuilder builder)
                {
                    builder.Object<Order>("Order", obj => { ACTIONS });
                }
                """,
            expressionBodiedDefine: true));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("not part of one direct Action(...) fluent chain");
    }

    /// <summary>An Object configure helper cannot hide a workflow binding from inventory.</summary>
    [Test]
    public async Task BindingFactoredThroughObjectBuilderHelper_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: "builder.Object<Order>(\"Order\", obj => ConfigureActions(obj));",
            ontologyMembers: """
                private static void ConfigureActions(IObjectTypeBuilder<Order> obj)
                {
                    ACTIONS
                }
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("not part of one direct Action(...) fluent chain");
    }

    /// <summary>A conditionally declared action is not a must-execute catalog member.</summary>
    [Test]
    public async Task ConditionalBoundAction_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: """
                builder.Object<Order>("Order", obj =>
                {
                    if (DateTime.UtcNow.Ticks > 0)
                    {
                        ACTIONS
                    }
                });
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("conditional or non-linear control flow");
    }

    /// <summary>A conditional authority declaration can change the runtime lattice.</summary>
    [Test]
    public async Task ConditionalAuthorityDeclaration_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: """
                if (DateTime.UtcNow.Ticks > 0)
                {
                    builder.AuthorityAxis("role", "reader", "manager");
                }

                builder.Object<Order>("Order", obj => { ACTIONS });
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("Define method contains conditional");
    }

    /// <summary>A dynamic last-write authority coordinate cannot be certified from an earlier value.</summary>
    [Test]
    public async Task DynamicAuthorityCoordinateOverwrite_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", AuthoritySource(
            """
            builder.AuthorityAxis("role", "reader", "manager");
            builder.Authority("reader").At("role", "reader");
            builder.Authority("manager")
                .At("role", "reader")
                .At("role", WorkflowNames.ResolveLevel());
            """,
            occurrenceAuthority: "manager"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("dynamic axis or level");
    }

    /// <summary>Duplicate axis levels are invalid at runtime and cannot define a proof lattice.</summary>
    [Test]
    public async Task DuplicateAuthorityAxisLevels_ReportAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", AuthoritySource(
            """
            builder.AuthorityAxis("role", "reader", "reader");
            builder.Authority("reader").At("role", "reader");
            """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("duplicate levels");
    }

    /// <summary>Objects registered on a different builder cannot satisfy this ontology's occurrences.</summary>
    [Test]
    public async Task ForeignOntologyBuilderObjects_CannotSatisfyWorkflowOccurrences()
    {
        var diagnostics = BindingDiagnostics(Source(
            bindingSuffix: string.Empty,
            declaration: """
                obj.Action("fulfill")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage)
                    .BoundToWorkflow("flow");
                """,
            objectInvocation: """
                builder.Object<Order>("Order", obj => { ACTIONS });
                OtherBuilder.Object<Order>("Order", foreign =>
                {
                    foreign.Action("leaf")
                        .Requires(order => order.Stage == 0)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                    foreign.Action("done")
                        .Requires(order => order.Stage == 1)
                        .Ensures(order => order.Stage == 1)
                        .Modifies(order => order.Stage);
                });
                """,
            ontologyMembers: "private static IOntologyBuilder OtherBuilder => null!;",
            terminalActionDeclaration: string.Empty), "AGWF040");

        await Assert.That(diagnostics).HasCount().EqualTo(2);
        foreach (var diagnostic in diagnostics)
        {
            await Assert.That(diagnostic.Id).IsEqualTo("AGWF040");
            await Assert.That(diagnostic.GetMessage()).Contains("resolving to 0 declarations");
        }
    }

    /// <summary>Authority declarations on another builder do not alter this ontology's lattice.</summary>
    [Test]
    public async Task ForeignOntologyBuilderAuthority_DoesNotAffectCurrentLattice()
    {
        var diagnostics = BindingDiagnostics(AuthoritySource(
            """
            builder.AuthorityAxis("role", "reader", "manager");
            builder.Authority("reader").At("role", "reader");
            OtherBuilder.AuthorityAxis("foreign", "weak", "strong");
            OtherBuilder.Authority("foreign-reader").At("foreign", "weak");
            """,
            ontologyMembers: "private static IOntologyBuilder OtherBuilder => null!;"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>A workflow binding cannot use a runtime-computed ontology domain identity.</summary>
    [Test]
    public async Task DynamicDomainName_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            domainPropertyOverride: "public override string DomainName => WorkflowNames.Resolve();"));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("ontology domain name is not statically closed");
    }

    /// <summary>A block getter with divergent returns is not mistaken for its first literal.</summary>
    [Test]
    public async Task DomainNameGetterWithMultipleReturns_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            ".BoundToWorkflow(\"flow\")",
            domainPropertyOverride: """
                public override string DomainName
                {
                    get
                    {
                        if (DateTime.UtcNow.Ticks > 0)
                        {
                            return "orders";
                        }

                        return "other-orders";
                    }
                }
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("ontology domain name is not statically closed");
    }

    /// <summary>The default generic object name matches runtime typeof(T).Name metadata identity.</summary>
    [Test]
    public async Task DefaultGenericObjectName_UsesMetadataName()
    {
        var diagnostics = BindingDiagnostics(Source(
            ".BoundToWorkflow(\"flow\")",
            objectInvocation: "builder.Object<GenericOrder<int>>(obj => { ACTIONS });",
            extraDeclaration: "public sealed class GenericOrder<T> { public int Stage { get; set; } }",
            workflowObjectTypeName: "GenericOrder`1"));

        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>An ActionDescriptor with-expression binding cannot escape catalog inventory.</summary>
    [Test]
    public async Task DescriptorWithWorkflowBinding_ReportsAgwf042()
    {
        var diagnostic = SingleBindingDiagnostic("AGWF042", Source(
            bindingSuffix: string.Empty,
            extraDeclaration: """
                public static class WithAction
                {
                    private static readonly ActionDescriptor Original = new(
                        new ActionSubject("orders", "Order"),
                        "factored",
                        "");

                    public static readonly ActionDescriptor Bound = Original with
                    {
                        BindingType = ActionBindingType.Workflow,
                        BoundWorkflow = new WorkflowBindingReference("flow"),
                    };
                }
                """));

        await Assert.That(diagnostic.Id).IsEqualTo("AGWF042");
        await Assert.That(diagnostic.GetMessage()).Contains("ActionDescriptor with-expression");
    }

    private static Diagnostic[] BindingDiagnostics(
        string source,
        params string[] allowedGeneratorErrorIds) =>
        RunBindingGenerator(source, allowedGeneratorErrorIds).Diagnostics
            .Where(diagnostic => diagnostic.Id is "AGWF039" or "AGWF040" or "AGWF041" or "AGWF042")
            .ToArray();

    private static GeneratorDriverRunResult RunBindingGenerator(
        string source,
        params string[] allowedGeneratorErrorIds) =>
        GeneratorTestHelper.RunGeneratorWithValidInput(source, allowedGeneratorErrorIds);

    private static Diagnostic SingleBindingDiagnostic(string expectedId, string source)
    {
        var diagnostics = BindingDiagnostics(source, expectedId);
        if (diagnostics.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one binding diagnostic, found {diagnostics.Length}: "
                + string.Join(" | ", diagnostics.Select(item => item.Id + ": " + item.GetMessage())));
        }

        var diagnostic = diagnostics[0];
        if (!string.Equals(diagnostic.Id, expectedId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected {expectedId}, found {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        return diagnostic;
    }

    private static string Source(
        string bindingSuffix,
        string? declaration = null,
        string? objectInvocation = null,
        bool partialDomain = false,
        string? extraDeclaration = null,
        string? ontologyMembers = null,
        string? ontologyMembersBeforeDefine = null,
        bool expressionBodiedDefine = false,
        string? domainPropertyOverride = null,
        string workflowObjectTypeName = "Order",
        string? terminalActionDeclaration = null)
    {
        var primaryActions = declaration ?? $$"""
            obj.Action("fulfill")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage){{bindingSuffix}};

            obj.Action("leaf")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            """;
        var terminalAction = terminalActionDeclaration ?? """
            obj.Action("done")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            """;
        var actions = primaryActions + Environment.NewLine + terminalAction;
        var objectStatement = (objectInvocation
            ?? "builder.Object<Order>(\"Order\", obj => { ACTIONS });")
            .Replace("ACTIONS", actions, StringComparison.Ordinal);
        var additionalOntologyMembers = (ontologyMembers ?? string.Empty)
            .Replace("ACTIONS", actions, StringComparison.Ordinal);
        var precedingOntologyMembers = (ontologyMembersBeforeDefine ?? string.Empty)
            .Replace("ACTIONS", actions, StringComparison.Ordinal);
        var defineMethod = expressionBodiedDefine
            ? $"protected override void Define(IOntologyBuilder builder) => {objectStatement}"
            : $$"""
                protected override void Define(IOntologyBuilder builder)
                {
                    {{objectStatement}}
                }
                """;
        var domainProperty = domainPropertyOverride ?? (partialDomain
            ? string.Empty
            : "public override string DomainName => \"orders\";");
        var partialProperty = partialDomain
            ? "public sealed partial class OrdersOntology { public override string DomainName => \"orders\"; }"
            : string.Empty;

        return $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Ontology;
            using Strategos.Ontology.Builder;
            using Strategos.Ontology.Descriptors;
            using Strategos.Steps;

            namespace BindingInventory;

            public sealed class Order { public int Stage { get; set; } }

            public static class WorkflowNames
            {
                public static string Resolve() => "flow";
                public static string ResolveLevel() => "manager";
            }

            public static class ActionBuilderExtensions
            {
                public static IActionBuilder<Order> Opaque(this IActionBuilder<Order> builder) => builder;
            }

            public sealed partial class OrdersOntology : DomainOntology
            {
                {{domainProperty}}

                {{precedingOntologyMembers}}

                {{defineMethod}}

                {{additionalOntologyMembers}}
            }

            {{partialProperty}}
            {{extraDeclaration}}

            [WorkflowState]
            public sealed record FlowState : IWorkflowState { public Guid WorkflowId { get; init; } }
            public sealed class Leaf : IWorkflowStep<FlowState>
            {
                public Task<StepResult<FlowState>> ExecuteAsync(
                    FlowState state,
                    StepContext context,
                    CancellationToken cancellationToken) =>
                    Task.FromResult(StepResult<FlowState>.FromState(state));
            }

            public sealed class Done : IWorkflowStep<FlowState>
            {
                public Task<StepResult<FlowState>> ExecuteAsync(
                    FlowState state,
                    StepContext context,
                    CancellationToken cancellationToken) =>
                    Task.FromResult(StepResult<FlowState>.FromState(state));
            }

            [Workflow("flow")]
            public static partial class FlowWorkflowDefinition
            {
                public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                    .Create("flow")
                    .StartWith<Leaf>(step => step.Performs(
                        new WorkflowActionReference("orders", "{{workflowObjectTypeName}}", "leaf")))
                    .Finally<Done>(step => step.Performs(
                        new WorkflowActionReference("orders", "{{workflowObjectTypeName}}", "done")));
            }
            """;
    }

    private static string AuthoritySource(
        string authorityDeclarations,
        string occurrenceAuthority = "reader",
        string? ontologyMembers = null) => Source(
            bindingSuffix: string.Empty,
            declaration: $$"""
                obj.Action("fulfill")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage)
                    .RequiresAuthority("reader")
                    .BoundToWorkflow("flow");

                obj.Action("leaf")
                    .Requires(order => order.Stage == 0)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage)
                    .RequiresAuthority("{{occurrenceAuthority}}");
                """,
            objectInvocation: $$"""
                {{authorityDeclarations}}
                builder.Object<Order>("Order", obj => { ACTIONS });
                """,
            ontologyMembers: ontologyMembers,
            terminalActionDeclaration: $$"""
                obj.Action("done")
                    .Requires(order => order.Stage == 1)
                    .Ensures(order => order.Stage == 1)
                    .Modifies(order => order.Stage)
                    .RequiresAuthority("{{occurrenceAuthority}}");
                """);

    private static string InlineDescriptorSource(
        string boundDomainName = "orders",
        string boundObjectTypeName = "Order",
        bool conditionalRegistration = false,
        bool conditionalActions = false,
        bool escapedBuilder = false,
        bool customActionsCarrier = false,
        string? boundBindingType = "ActionBindingType.Workflow",
        bool includeLeafAction = true,
        bool descriptorLocal = false,
        bool escapeDescriptorLocal = false)
    {
        var receiver = escapedBuilder ? "Capture(builder)" : "builder";
        var registrationOpen = conditionalRegistration
            ? "if (DateTime.UtcNow.Ticks > 0)\n            {"
            : string.Empty;
        var registrationClose = conditionalRegistration ? "\n            }" : string.Empty;
        var actionsOpen = customActionsCarrier
            ? "new EvilActionList\n                    {"
            : conditionalActions
            ? "DateTime.UtcNow.Ticks > 0 ?\n                    ["
            : "[";
        var actionsClose = customActionsCarrier
            ? "}"
            : conditionalActions ? "] : []" : "]";
        var captureMember = escapedBuilder
            ? "private static IOntologyBuilder Capture(IOntologyBuilder builder) => builder;"
            : string.Empty;
        var descriptorPrefix = descriptorLocal
            ? "var descriptor = new ObjectTypeDescriptor"
            : $"{receiver}.ObjectTypeFromDescriptor(new ObjectTypeDescriptor";
        var descriptorSuffix = descriptorLocal
            ? $$"""
                ;
                {{(escapeDescriptorLocal ? "CaptureDescriptor(descriptor);" : string.Empty)}}
                {{receiver}}.ObjectTypeFromDescriptor(descriptor);
                """
            : ");";
        var descriptorCaptureMember = escapeDescriptorLocal
            ? "private static void CaptureDescriptor(ObjectTypeDescriptor descriptor) { }"
            : string.Empty;
        var bindingTypeAssignment = boundBindingType is null
            ? string.Empty
            : $"BindingType = {boundBindingType},";
        var leafAction = includeLeafAction
            ? """
                        new ActionDescriptor(
                            new ActionSubject("orders", "Order"),
                            "leaf",
                            ""),
                """
            : string.Empty;

        return $$"""
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Ontology;
        using Strategos.Ontology.Builder;
        using Strategos.Ontology.Descriptors;
        using Strategos.Steps;

        namespace BindingInventory;

        public sealed class Order { }

        public sealed class EvilActionList : IReadOnlyList<ActionDescriptor>
        {
            public int Count => 0;
            public ActionDescriptor this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
            public void Add(ActionDescriptor item) { }
            public IEnumerator<ActionDescriptor> GetEnumerator() { yield break; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public sealed class OrdersOntology : DomainOntology
        {
            public override string DomainName => "orders";

            protected override void Define(IOntologyBuilder builder)
            {
                {{registrationOpen}}
                {{descriptorPrefix}}
                {
                    Name = "Order",
                    DomainName = "orders",
                    SymbolKey = "scip-typescript npm orders 1.0.0 Order#",
                    LanguageId = "typescript",
                    Actions =
                    {{actionsOpen}}
                        new ActionDescriptor(
                            new ActionSubject("{{boundDomainName}}", "{{boundObjectTypeName}}"),
                            "fulfill",
                            "")
                        {
                            {{bindingTypeAssignment}}
                            BoundWorkflow = new WorkflowBindingReference("flow"),
                        },
                        {{leafAction}}
                        new ActionDescriptor(
                            new ActionSubject("orders", "Order"),
                            "done",
                            ""),
                    {{actionsClose}},
                }{{descriptorSuffix}}
                {{registrationClose}}
            }

            {{captureMember}}
            {{descriptorCaptureMember}}
        }

        [WorkflowState]
        public sealed record FlowState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class TestStep : IWorkflowStep<FlowState>
        {
            public Task<StepResult<FlowState>> ExecuteAsync(
                FlowState state,
                StepContext context,
                CancellationToken cancellationToken) =>
                Task.FromResult(StepResult<FlowState>.FromState(state));
        }

        public sealed class Leaf : TestStep { }
        public sealed class Done : TestStep { }

        [Workflow("flow")]
        public static partial class FlowWorkflowDefinition
        {
            public static WorkflowDefinition<FlowState> Definition => Workflow<FlowState>
                .Create("flow")
                .StartWith<Leaf>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "leaf")))
                .Finally<Done>(step => step.Performs(
                    new WorkflowActionReference("orders", "Order", "done")));
        }
        """;
    }
}
