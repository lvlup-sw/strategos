// -----------------------------------------------------------------------
// <copyright file="SagaPropertiesEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits the property declarations for a Wolverine saga class.
/// </summary>
/// <remarks>
/// <para>
/// This emitter generates the following properties:
/// <list type="bullet">
///   <item><description>WorkflowId - The saga identity with both [SagaIdentity] and [Identity] attributes</description></item>
///   <item><description>Version - Optimistic concurrency control with [Version] attribute</description></item>
///   <item><description>Phase - Current workflow phase with NotStarted default</description></item>
///   <item><description>State - Workflow state (if StateTypeName is specified)</description></item>
///   <item><description>Iteration counters - One per loop (if loops are defined)</description></item>
///   <item><description>StartedAt - Timestamp when workflow started</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SagaPropertiesEmitter : ISagaComponentEmitter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var phaseEnumName = model.PhaseEnumName;

        // WorkflowId with both attributes.
        // The Marten document-identity attribute is JasperFx.IdentityAttribute,
        // surfaced via `using Marten.Schema;`. It is written fully qualified as
        // [JasperFx.Identity]: consumers that also reference
        // Strategos.Identity.Abstractions bring the Strategos.Identity namespace
        // into scope, and an unqualified [Identity] would bind to that namespace
        // instead of the attribute (CS0616).
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets or sets the workflow identifier.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [SagaIdentity]");
        sb.AppendLine("    [JasperFx.Identity]");
        sb.AppendLine("    public Guid WorkflowId { get; set; }");
        sb.AppendLine();

        // Version for optimistic concurrency.
        // Typed as long: Marten 9 widened numeric document revisions from int to
        // long and rejects an int [Version] property at mapping time. This
        // shadows the Wolverine Saga.Version (int) base property with `new`.
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets or sets the version for optimistic concurrency control.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    [Version]");
        sb.AppendLine("    public new long Version { get; set; }");
        sb.AppendLine();

        // Phase property
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets or sets the current workflow phase.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public {phaseEnumName} Phase {{ get; set; }} = {phaseEnumName}.NotStarted;");
        sb.AppendLine();

        // IPhaseAwareSaga.CurrentPhaseName — the ONLY identity-related emit per DR-6
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the current saga phase as a stable string identifier (Phase.ToString()).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public string CurrentPhaseName => Phase.ToString();");
        sb.AppendLine();

        // State property (if state type is specified)
        if (!string.IsNullOrEmpty(model.StateTypeName))
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the workflow state.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public {model.StateTypeName} State {{ get; set; }} = default!;");
            sb.AppendLine();
        }

        // Loop iteration count properties
        if (model.HasLoops)
        {
            foreach (var loop in model.Loops!)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Gets or sets the iteration count for the {loop.LoopName} loop.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine($"    public int {loop.IterationPropertyName} {{ get; set; }}");
                sb.AppendLine();
            }
        }

        // Approval state tracking properties (if approvals are defined)
        if (model.HasApprovalPoints)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the pending approval request ID for timeout race condition detection.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public string? PendingApprovalRequestId { get; set; }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the instructions provided by the approver.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public string? ApprovalInstructions { get; set; }");
            sb.AppendLine();
        }

        // Fork path status and state properties (if forks are defined)
        if (model.HasForks)
        {
            foreach (var fork in model.Forks!)
            {
                // Sanitize ForkId for valid C# identifier
                var sanitizedId = fork.ForkId.Replace("-", "_");

                foreach (var path in fork.Paths)
                {
                    // Path status property
                    sb.AppendLine("    /// <summary>");
                    sb.AppendLine($"    /// Gets or sets the status for path {path.PathIndex} of fork {fork.ForkId}.");
                    sb.AppendLine("    /// </summary>");
                    sb.AppendLine($"    public Strategos.Definitions.ForkPathStatus Fork_{sanitizedId}_Path{path.PathIndex}Status {{ get; set; }} = Strategos.Definitions.ForkPathStatus.Pending;");
                    sb.AppendLine();

                    // Path state property (nullable to store path result)
                    if (!string.IsNullOrEmpty(model.StateTypeName))
                    {
                        sb.AppendLine("    /// <summary>");
                        sb.AppendLine($"    /// Gets or sets the state for path {path.PathIndex} of fork {fork.ForkId}.");
                        sb.AppendLine("    /// </summary>");
                        sb.AppendLine($"    public {model.StateTypeName}? Fork_{sanitizedId}_Path{path.PathIndex}State {{ get; set; }}");
                        sb.AppendLine();
                    }
                }
            }
        }

        // Failure tracking properties (if failure handlers are defined)
        if (model.HasFailureHandlers)
        {
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the name of the step that failed, triggering the failure handler.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public string? FailedStepName { get; set; }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the exception message from the failed step.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public string? FailureExceptionMessage { get; set; }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the exception type name from the failed step.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public string? FailureExceptionType { get; set; }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the stack trace from the failed step.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public string? FailureStackTrace { get; set; }");
            sb.AppendLine();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets or sets the timestamp when the failure occurred.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public DateTimeOffset? FailureTimestamp { get; set; }");
            sb.AppendLine();
        }

        // StartedAt timestamp
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets or sets the timestamp when the workflow started.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public DateTimeOffset StartedAt { get; set; }");
    }
}
