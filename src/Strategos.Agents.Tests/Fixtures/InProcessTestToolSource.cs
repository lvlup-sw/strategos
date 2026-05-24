// =============================================================================
// <copyright file="InProcessTestToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.AI;
using Strategos.Agents.Abstractions;

namespace Strategos.Agents.Tests.Fixtures;

/// <summary>
/// In-process hand-rolled fake implementing <see cref="IToolSource"/> for tests.
/// Replaces NSubstitute mocks of the port — the project rule is that mocks live
/// ONLY at the <see cref="IChatClient"/> boundary; every other collaborator
/// (including Strategos-owned ports) is constructed real or via a hand-rolled
/// fake like this one.
/// </summary>
/// <remarks>
/// <para>
/// Counts invocations of <see cref="GetToolsAsync(CancellationToken)"/> so the
/// resolve-at-most-once cache contract on <c>StrategosFunctionsChatClient</c>
/// can be asserted against the fake directly.
/// </para>
/// <para>
/// Optionally throws a caller-supplied exception (constructor overload) so the
/// source-resolution-failure path can be exercised without a mocking library.
/// </para>
/// </remarks>
internal sealed class InProcessTestToolSource : IToolSource
{
    private readonly IReadOnlyList<AIFunction> _tools;
    private readonly Exception? _throwOnResolve;
    private int _calls;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessTestToolSource"/> class
    /// that returns <paramref name="tools"/> from <see cref="GetToolsAsync(CancellationToken)"/>.
    /// </summary>
    /// <param name="tools">Tools to surface on each invocation.</param>
    public InProcessTestToolSource(IReadOnlyList<AIFunction> tools)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _throwOnResolve = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessTestToolSource"/> class
    /// whose <see cref="GetToolsAsync(CancellationToken)"/> throws
    /// <paramref name="throwOnResolve"/>. Used to exercise the resolution-failure path.
    /// </summary>
    /// <param name="throwOnResolve">Exception to throw on resolve.</param>
    public InProcessTestToolSource(Exception throwOnResolve)
    {
        _tools = Array.Empty<AIFunction>();
        _throwOnResolve = throwOnResolve ?? throw new ArgumentNullException(nameof(throwOnResolve));
    }

    /// <summary>Number of times <see cref="GetToolsAsync(CancellationToken)"/> has been invoked.</summary>
    public int GetToolsAsyncCount => Volatile.Read(ref _calls);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (_throwOnResolve is not null)
        {
            throw _throwOnResolve;
        }

        return Task.FromResult(_tools);
    }
}
