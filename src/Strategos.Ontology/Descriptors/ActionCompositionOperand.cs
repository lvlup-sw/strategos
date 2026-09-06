namespace Strategos.Ontology.Descriptors;

/// <summary>
/// Closed operand accepted by sequential composition: a concrete action, a
/// nested composite, or a subject-typed empty identity.
/// </summary>
public sealed class ActionCompositionOperand
{
    private ActionCompositionOperand(
        ActionDescriptor? action,
        CompositeActionContract? composite,
        ActionSubject? identitySubject)
    {
        Action = action;
        Composite = composite;
        IdentitySubject = identitySubject;
    }

    internal ActionDescriptor? Action { get; }

    internal CompositeActionContract? Composite { get; }

    internal ActionSubject? IdentitySubject { get; }

    /// <summary>Wraps one concrete action.</summary>
    public static ActionCompositionOperand From(ActionDescriptor action) =>
        new(action ?? throw new ArgumentNullException(nameof(action)), null, null);

    /// <summary>Wraps one previously constructed composite.</summary>
    public static ActionCompositionOperand From(CompositeActionContract composite) =>
        new(null, composite ?? throw new ArgumentNullException(nameof(composite)), null);

    internal static ActionCompositionOperand Identity(ActionSubject subject) =>
        new(null, null, subject ?? throw new ArgumentNullException(nameof(subject)));

    /// <summary>Converts a concrete action into a composition operand.</summary>
    public static implicit operator ActionCompositionOperand(ActionDescriptor action) => From(action);

    /// <summary>Converts a nested composite into a composition operand.</summary>
    public static implicit operator ActionCompositionOperand(CompositeActionContract composite) => From(composite);
}
