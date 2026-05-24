// =============================================================================
// <copyright file="AgentExceptionThrowingToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// <see cref="IToolSource"/> fixture that throws a conforming <see cref="AgentToolSourceException"/>
/// (AGAG007) from <see cref="GetToolsAsync"/>. Used to assert the propagation-unchanged
/// arm of the boundary catch: a source that already wraps its own failure must not be
/// re-wrapped in a second <see cref="AgentToolSourceException"/> (#85 / T2).
/// </summary>
internal sealed class AgentExceptionThrowingToolSource : IToolSource
{
    private readonly AgentToolSourceException _exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentExceptionThrowingToolSource"/> class.
    /// </summary>
    /// <param name="exception">The <see cref="AgentToolSourceException"/> to throw on resolve.</param>
    public AgentExceptionThrowingToolSource(AgentToolSourceException exception)
    {
        _exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
