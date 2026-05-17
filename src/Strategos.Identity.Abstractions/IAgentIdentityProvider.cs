// -----------------------------------------------------------------------
// <copyright file="IAgentIdentityProvider.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions;

/// <summary>
/// Port that derives a per-step <see cref="AgentIdentity"/> from a
/// <see cref="WorkflowIdentity"/> and the saga's current phase name.
/// </summary>
/// <remarks>
/// <para>
/// Strategos owns this port; basileus is the SPIFFE-shaped adapter. The
/// derivation is keyed on <c>phaseName</c> (not numeric step ordinal) because
/// the Strategos saga's authoritative step identifier is <c>Phase.ToString()</c>
/// — workflows with forks, branches, and failure handlers do not have stable
/// numeric ordinals.
/// </para>
/// <para>
/// Implementations must be pure and deterministic given equal inputs. All
/// methods reject null or empty input per DR-3 / DR-8 row 4.
/// </para>
/// </remarks>
public interface IAgentIdentityProvider
{
    /// <summary>
    /// Derives the agent identity for the supplied workflow and phase.
    /// </summary>
    /// <param name="workflow">The workflow identity. Must not be null.</param>
    /// <param name="phaseName">The saga's current phase name. Must be non-null, non-empty.</param>
    /// <returns>The derived <see cref="AgentIdentity"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="workflow"/> or <paramref name="phaseName"/> is null.</exception>
    /// <exception cref="ArgumentException">When <paramref name="phaseName"/> is empty or whitespace.</exception>
    AgentIdentity DeriveStepIdentity(WorkflowIdentity workflow, string phaseName);

    /// <summary>
    /// Parses the inbound workflow-identity header value into a <see cref="WorkflowIdentity"/>.
    /// </summary>
    /// <param name="headerValue">The header value as it appears on the envelope. Must not be null.</param>
    /// <returns>The parsed <see cref="WorkflowIdentity"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="headerValue"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// When <paramref name="headerValue"/> violates the validation contract of <see cref="WorkflowIdentity"/>.
    /// </exception>
    WorkflowIdentity ParseWorkflowHeader(string headerValue);
}
