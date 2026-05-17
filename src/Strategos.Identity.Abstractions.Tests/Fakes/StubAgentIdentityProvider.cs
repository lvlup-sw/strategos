// -----------------------------------------------------------------------
// <copyright file="StubAgentIdentityProvider.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions.Tests.Fakes;

/// <summary>
/// Minimal in-test implementation of <see cref="IAgentIdentityProvider"/>.
/// </summary>
/// <remarks>
/// Concatenates <c>{workflow.Value}#{phaseName}</c> for derivation; round-trips
/// the workflow header value verbatim. The production basileus adapter shapes
/// the value as <c>spiffe://td/workflow/&lt;id&gt;/step/&lt;phase&gt;</c> — the
/// stub is intentionally simpler so tests can assert the contract without
/// pulling in SPIFFE machinery.
/// </remarks>
internal sealed class StubAgentIdentityProvider : IAgentIdentityProvider
{
    /// <inheritdoc/>
    public AgentIdentity DeriveStepIdentity(WorkflowIdentity workflow, string phaseName)
    {
        if (workflow is null)
        {
            throw new ArgumentNullException(nameof(workflow));
        }

        if (phaseName is null)
        {
            throw new ArgumentNullException(nameof(phaseName));
        }

        if (string.IsNullOrWhiteSpace(phaseName))
        {
            throw new ArgumentException("Phase name must be non-empty.", nameof(phaseName));
        }

        return new AgentIdentity($"{workflow.Value}#{phaseName}");
    }

    /// <inheritdoc/>
    public WorkflowIdentity ParseWorkflowHeader(string headerValue)
    {
        if (headerValue is null)
        {
            throw new ArgumentNullException(nameof(headerValue));
        }

        return new WorkflowIdentity(headerValue);
    }
}
