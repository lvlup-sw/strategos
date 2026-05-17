// -----------------------------------------------------------------------
// <copyright file="FakeAgentIdentityAccessor.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Identity.Abstractions.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAgentIdentityAccessor"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// Constructed with an optional <c>IDictionary&lt;string,string&gt;</c> standing in
/// for the envelope-header bag. Passing <c>null</c> models the "no Wolverine
/// message context active" case (DR-8 row 1).
/// </para>
/// <para>
/// Per DR-5 contract, invalid header values produce <c>null</c> rather than
/// throwing — the accessor is read-only and must not be allowed to crash
/// projections, debuggers, or hosted-service inspection paths.
/// </para>
/// </remarks>
public sealed class FakeAgentIdentityAccessor : IAgentIdentityAccessor
{
    private readonly IDictionary<string, string>? envelopeHeaders;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeAgentIdentityAccessor"/> class.
    /// </summary>
    /// <param name="envelopeHeaders">
    /// The header bag standing in for the Wolverine envelope, or <c>null</c> to model
    /// "no message context active".
    /// </param>
    public FakeAgentIdentityAccessor(IDictionary<string, string>? envelopeHeaders)
    {
        this.envelopeHeaders = envelopeHeaders;
    }

    /// <inheritdoc/>
    public WorkflowIdentity? CurrentWorkflow => this.TryGet(StrategosHeaders.WorkflowIdentity, v => new WorkflowIdentity(v));

    /// <inheritdoc/>
    public AgentIdentity? CurrentAgent => this.TryGet(StrategosHeaders.AgentIdentity, v => new AgentIdentity(v));

    private T? TryGet<T>(string headerKey, Func<string, T> factory)
        where T : class
    {
        if (this.envelopeHeaders is null)
        {
            return null;
        }

        if (!this.envelopeHeaders.TryGetValue(headerKey, out var raw) || raw is null)
        {
            return null;
        }

        try
        {
            return factory(raw);
        }
        catch (ArgumentException)
        {
            // DR-5: invalid header value must not crash the accessor — silently
            // degrade so projections, debuggers, and hosted-service inspection
            // paths can continue.
            return null;
        }
    }
}
