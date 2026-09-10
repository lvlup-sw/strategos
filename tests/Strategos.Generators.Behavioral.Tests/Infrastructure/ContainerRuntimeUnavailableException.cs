// -----------------------------------------------------------------------
// <copyright file="ContainerRuntimeUnavailableException.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Thrown when the behavioral suite's PostgreSQL container cannot be started.
/// </summary>
/// <remarks>
/// <para>
/// The suite needs a reachable Docker-API daemon. Without one, EVERY test in it fails at
/// fixture initialization — which is indistinguishable, from the outside, from a real
/// regression in the code under test. That ambiguity is at its worst precisely when the
/// generator is being changed, because a whole-suite red is exactly what a genuine break
/// would look like.
/// </para>
/// <para>
/// This exception type exists so the environment fault is machine-distinguishable rather
/// than a wall of ordinary failures, and its message states the resolved daemon endpoint,
/// whether it exists on disk, and how to point the harness at a rootless socket.
/// </para>
/// </remarks>
public sealed class ContainerRuntimeUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerRuntimeUnavailableException"/> class.
    /// </summary>
    public ContainerRuntimeUnavailableException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerRuntimeUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic describing the container-runtime fault.</param>
    public ContainerRuntimeUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerRuntimeUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The diagnostic describing the container-runtime fault.</param>
    /// <param name="innerException">The underlying container-start failure.</param>
    public ContainerRuntimeUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
