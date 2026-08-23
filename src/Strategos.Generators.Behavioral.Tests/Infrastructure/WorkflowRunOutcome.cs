// -----------------------------------------------------------------------
// <copyright file="WorkflowRunOutcome.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// What the harness actually observed while running a generated saga.
/// </summary>
/// <remarks>
/// The removal of the Marten saga document is the signal that a workflow reached its
/// terminal phase — but on its own it is not evidence of anything, because a saga that was
/// never created is absent too. Reporting the invocation count alongside it separates
/// "completed" from "never ran", which a single boolean cannot.
/// </remarks>
/// <param name="Completed">
/// Whether the saga demonstrably ran AND reached its terminal phase: work was recorded and
/// the document was removed.
/// </param>
/// <param name="DocumentRemoved">Whether the saga document was absent at the final poll.</param>
/// <param name="StepInvocations">
/// Step invocations recorded while this run was in flight. Zero means nothing executed, so
/// the document's absence carries no information.
/// </param>
/// <param name="Diagnostic">A human-readable account of what was observed.</param>
public sealed record WorkflowRunOutcome(
    bool Completed,
    bool DocumentRemoved,
    int StepInvocations,
    string Diagnostic);
