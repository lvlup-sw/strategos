using Strategos.Ontology.Generators.Diagnostics;

namespace Strategos.Ontology.Generators.Tests.Analyzers;

public class LifecycleDiagnosticTests
{
    [Test]
    public async Task AONT014_LifecyclePropertyUndeclared_ReportsError()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecyclePropertyUndeclared);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT014_Declared_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecyclePropertyUndeclared);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT015_NoInitialState_ReportsError()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft);
                lc.State(Status.Active);
                lc.State(Status.Closed).Terminal();
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleInitialStateCount);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT016_NoTerminalState_ReportsError()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Closed);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleNoTerminalState);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT015_AONT016_ValidLifecycle_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active).TriggeredByAction(""Activate"");
                lc.Transition(Status.Active, Status.Closed).TriggeredByAction(""Close"");
            }));
            obj.Action(""Activate"");
            obj.Action(""Close"");
        });
    }
}";

        var initDiags = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleInitialStateCount);
        var termDiags = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleNoTerminalState);

        await Assert.That(initDiags.Length).IsEqualTo(0);
        await Assert.That(termDiags.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT017_TransitionBadState_ReportsError()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleTransitionBadState);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT017_ValidState_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active);
                lc.Transition(Status.Active, Status.Closed);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleTransitionBadState);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT018_ValidAction_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Action(""Activate"");
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Closed).TriggeredByAction(""Activate"");
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleTransitionBadAction);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT019_TriggeredByEventUndeclared_ReportsWarning()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }
public record UndeclaredEvent(Guid Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Closed).TriggeredByEvent<UndeclaredEvent>();
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleTransitionBadEvent);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT019_TriggeredByDeclaredEvent_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }
public record ClosedEvent(Guid Id);

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Event<ClosedEvent>(e => { });
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Closed).TriggeredByEvent<ClosedEvent>();
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleTransitionBadEvent);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT018_TriggeredByUndeclaredAction_ReportsWarning()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Closed).TriggeredByAction(""NonExistentAction"");
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleTransitionBadAction);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT020_UnreachableState_ReportsWarning()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Orphan, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Orphan);
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active);
                lc.Transition(Status.Active, Status.Closed);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleUnreachableState);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT020_AllStatesReachable_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active);
                lc.Transition(Status.Active, Status.Closed);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleUnreachableState);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task AONT021_NonTerminalDeadEnd_ReportsWarning()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Stuck, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Stuck);
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active);
                lc.Transition(Status.Active, Status.Stuck);
                lc.Transition(Status.Active, Status.Closed);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleDeadEndState);

        await Assert.That(diagnostics.Length).IsEqualTo(1);
    }

    [Test]
    public async Task AONT021_TerminalDeadEnd_NoDiagnostic()
    {
        var source = @"
using System;
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public enum Status { Draft, Active, Closed }
public class TestModel { public Guid Id { get; set; } public Status Status { get; set; } }

public class TestDomain : DomainOntology
{
    public override string DomainName => ""test"";
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestModel>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Status);
            obj.Lifecycle(p => p.Status, (Action<ILifecycleBuilder<Status>>)(lc =>
            {
                lc.State(Status.Draft).Initial();
                lc.State(Status.Active);
                lc.State(Status.Closed).Terminal();
                lc.Transition(Status.Draft, Status.Active);
                lc.Transition(Status.Active, Status.Closed);
            }));
        });
    }
}";

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsWithIdAsync(source, OntologyDiagnosticIds.LifecycleDeadEndState);

        await Assert.That(diagnostics.Length).IsEqualTo(0);
    }
}
