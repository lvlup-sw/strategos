// -----------------------------------------------------------------------
// <copyright file="SagaConcurrencyPolicyEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits the saga-level <c>Configure(HandlerChain)</c> error policy that retries a
/// lost optimistic-concurrency race instead of dead-lettering it on the first loss.
/// </summary>
/// <remarks>
/// <para>
/// The saga class implements <c>JasperFx.IRevisioned</c>, so Wolverine's
/// <c>MartenPersistenceFrameProvider.DetermineUpdateFrame</c> emits
/// <c>documentSession.UpdateRevision(saga, expectedSagaRevision)</c> and Marten's
/// numeric revision guard is enforced. A concurrent delivery that loses the race
/// now raises <c>JasperFx.ConcurrencyException</c> rather than silently clobbering
/// the winner's transition.
/// </para>
/// <para>
/// Wolverine has no default failure rule for <c>ConcurrencyException</c>, so an
/// unmatched exception falls through to <c>MoveToErrorQueue</c> — an immediate dead
/// letter. This policy restores liveness: the loser is re-delivered, re-loads the
/// saga at the winner's revision, and re-applies its transition. After three
/// retries the message dead-letters, which is the correct terminal outcome for a
/// transition that genuinely cannot be applied.
/// </para>
/// <para>
/// Wolverine invokes the handler type's static <c>Configure</c> once per message
/// chain, so one unconditional method covers every message the saga handles; no
/// per-command dispatch is needed (unlike
/// <c>WorkerHandlerEmitter.EmitConfigureMethod</c>, whose per-command
/// <c>CompensatingAction&lt;T&gt;</c> cast must match the chain's message type).
/// </para>
/// </remarks>
internal sealed class SagaConcurrencyPolicyEmitter : ISagaComponentEmitter
{
    /// <summary>
    /// The number of re-delivery attempts granted to a saga transition that loses
    /// the optimistic-concurrency race before the message is dead-lettered.
    /// </summary>
    private const int ConcurrencyRetryTimes = 3;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        // The properties emitter closes on its last member with no trailing blank line.
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Configures the Wolverine error policy for every message chain handled by");
        sb.AppendLine("    /// this saga. Wolverine discovers this static method by convention and calls");
        sb.AppendLine("    /// it once per chain.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// The saga implements <c>JasperFx.IRevisioned</c>, so its persistence call is");
        sb.AppendLine("    /// <c>UpdateRevision</c> under Marten's numeric revision guard. A delivery that");
        sb.AppendLine("    /// loses a concurrent transition race throws <c>JasperFx.ConcurrencyException</c>;");
        sb.AppendLine("    /// Wolverine has no default rule for it and would dead-letter on the first loss.");
        sb.AppendLine("    /// Retrying re-loads the saga at the winner's revision and re-applies the");
        sb.AppendLine("    /// transition.");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <param name=\"chain\">The handler chain to apply the error policy to.</param>");
        sb.AppendLine("    public static void Configure(HandlerChain chain)");
        sb.AppendLine("    {");
        sb.AppendLine($"        chain.OnException<JasperFx.ConcurrencyException>().RetryTimes({ConcurrencyRetryTimes});");
        sb.AppendLine("    }");
        sb.AppendLine();
    }
}
