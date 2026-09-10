// =============================================================================
// <copyright file="ForeignThrowingToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// Bare <see cref="IToolSource"/> fixture that does NOT throw an <c>AgentException</c>
/// subtype. Its <see cref="GetToolsAsync"/> throws a raw <see cref="InvalidOperationException"/>
/// whose message embeds a URI with credentials, simulating a third-party adapter that
/// has not applied any redaction. Used to drive the credential-leak test (DR-10 / #85).
/// </summary>
internal sealed class ForeignThrowingToolSource : IToolSource
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("connect failed to https://alice:s3cr3t@mcp.example/tools");
    }
}
