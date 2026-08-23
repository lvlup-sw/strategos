// -----------------------------------------------------------------------
// <copyright file="ContainerRuntimeDiagnosticTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Tests that an unreachable container runtime is diagnosable as an environment fault
/// rather than surfacing as a suite-wide regression.
/// </summary>
/// <remarks>
/// These tests deliberately stand up no container and take no fixture: the whole point is
/// that they run and report usefully on a host where the behavioral suite CANNOT run.
/// </remarks>
[Property("Category", "Unit")]
public sealed class ContainerRuntimeDiagnosticTests
{
    /// <summary>
    /// With no endpoint resolvable, the diagnostic must name the container runtime, say
    /// plainly that this is an environment fault, and carry the remediation — never leave the
    /// reader to conclude the code under test broke.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContainerRuntimeDiagnostic_NoEndpointResolvable_NamesTheRuntimeAsAnEnvironmentFault()
    {
        var message = PostgresFixture.DescribeContainerRuntimeFailure(
            new InvalidOperationException("Docker is either not running or misconfigured"),
            dockerHost: null,
            podmanSocketPath: "/run/user/4242/podman/podman.sock",
            socketExists: _ => false);

        await Assert.That(message)
            .Contains("ENVIRONMENT fault")
            .Because("the reader must be told immediately that this is not a regression in the code under test");

        await Assert.That(message)
            .Contains("container runtime")
            .Because("the diagnostic must name what is missing");

        await Assert.That(message)
            .Contains("NO container-runtime endpoint was resolved")
            .Because("with DOCKER_HOST unset and no socket on disk, nothing was listening and the message must say so");

        await Assert.That(message)
            .Contains("/run/user/4242/podman/podman.sock")
            .Because("the probed endpoint must be quoted so the reader can check it");

        await Assert.That(message)
            .Contains("DOCKER_HOST            : (not set)")
            .Because("an unset DOCKER_HOST must be reported as unset, not omitted");

        await Assert.That(message)
            .Contains("TESTCONTAINERS_RYUK_DISABLED")
            .Because("the message must carry the remediation, not just the symptom");
    }

    /// <summary>
    /// With an endpoint resolvable, the diagnostic must NOT claim the runtime is missing —
    /// that would send the reader to fix an environment that is already correct.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContainerRuntimeDiagnostic_EndpointResolvable_DoesNotClaimTheRuntimeIsMissing()
    {
        var message = PostgresFixture.DescribeContainerRuntimeFailure(
            new InvalidOperationException("failed to pull image postgres:16-alpine"),
            dockerHost: "unix:///run/user/4242/podman/podman.sock",
            podmanSocketPath: "/run/user/4242/podman/podman.sock",
            socketExists: _ => true);

        await Assert.That(message)
            .DoesNotContain("NO container-runtime endpoint was resolved")
            .Because("an endpoint that resolved must not be reported as absent");

        await Assert.That(message)
            .Contains("A container-runtime endpoint was resolved")
            .Because("the reader must be pointed at the runtime's own failure instead");

        await Assert.That(message)
            .Contains("unix:///run/user/4242/podman/podman.sock")
            .Because("the resolved DOCKER_HOST must be quoted so the reader can check which endpoint was used");
    }

    /// <summary>
    /// A <c>DOCKER_HOST</c> that is SET but names a socket that is not on disk must be
    /// reported as unresolved, not as resolved. This is the most confusing case in practice —
    /// the variable being set makes the environment look configured — and an explicit override
    /// suppresses the fixture's own podman discovery, so the podman socket being present says
    /// nothing about whether anything was listening.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContainerRuntimeDiagnostic_DockerHostNamesAMissingSocket_ReportsItUnresolved()
    {
        const string MissingSocket = "/run/user/4242/nonexistent/does-not-exist.sock";
        const string PodmanSocket = "/run/user/4242/podman/podman.sock";

        // The podman socket IS present: the override is what broke the run, not its absence.
        var message = PostgresFixture.DescribeContainerRuntimeFailure(
            new InvalidOperationException("Docker is either not running or misconfigured"),
            dockerHost: "unix://" + MissingSocket,
            podmanSocketPath: PodmanSocket,
            socketExists: path => string.Equals(path, PodmanSocket, StringComparison.Ordinal));

        await Assert.That(message)
            .Contains("NO container-runtime endpoint was resolved")
            .Because("a DOCKER_HOST naming a socket that is not on disk means nothing was listening");

        await Assert.That(message)
            .Contains("DOCKER_HOST socket     : MISSING on disk")
            .Because("the reader must be told which of the two endpoints is the missing one");

        await Assert.That(message)
            .Contains("DOCKER_HOST always wins")
            .Because(
                "with a healthy podman socket also present, the reader needs to know the override "
                + "suppressed discovery — otherwise the environment looks correct");
    }

    /// <summary>
    /// A non-unix <c>DOCKER_HOST</c> cannot be probed on disk, so the diagnostic must not
    /// invent a verdict about it.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContainerRuntimeDiagnostic_NonUnixDockerHost_MakesNoOnDiskClaim()
    {
        var message = PostgresFixture.DescribeContainerRuntimeFailure(
            new InvalidOperationException("connection refused"),
            dockerHost: "tcp://127.0.0.1:2375",
            podmanSocketPath: "/run/user/4242/podman/podman.sock",
            socketExists: _ => false);

        await Assert.That(message)
            .DoesNotContain("DOCKER_HOST socket")
            .Because("a tcp endpoint has no on-disk socket, so claiming one is present or missing would be false");

        await Assert.That(message)
            .Contains("tcp://127.0.0.1:2375")
            .Because("the configured endpoint must still be quoted");
    }

    /// <summary>
    /// The underlying container-start failure is preserved verbatim, so the diagnostic adds
    /// context without hiding the cause.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ContainerRuntimeDiagnostic_PreservesTheUnderlyingFailure()
    {
        var failure = new TimeoutException("the operation has timed out");

        var message = PostgresFixture.DescribeContainerRuntimeFailure(
            failure,
            dockerHost: null,
            podmanSocketPath: "/run/user/4242/podman/podman.sock",
            socketExists: _ => false);

        await Assert.That(message)
            .Contains("the operation has timed out")
            .Because("wrapping must not swallow the cause");

        await Assert.That(message)
            .Contains(typeof(TimeoutException).FullName!)
            .Because("the underlying exception type is part of the cause");
    }

    /// <summary>
    /// The fixture's start path converts a container-start failure into the dedicated
    /// environment-fault exception, preserving the original as the inner exception. This runs
    /// the production wrapper, not a copy of it, and needs no container runtime to do so.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PostgresFixtureStart_ContainerStartFails_ThrowsContainerRuntimeUnavailable()
    {
        var underlying = new InvalidOperationException("Docker is either not running or misconfigured");

        var thrown = await Assert.ThrowsAsync<ContainerRuntimeUnavailableException>(
            async () => await PostgresFixture.StartWithRuntimeDiagnosticsAsync(() => Task.FromException(underlying)));

        await Assert.That(thrown!.InnerException)
            .IsSameReferenceAs(underlying)
            .Because("the original container-start failure must remain available for debugging");

        await Assert.That(thrown.Message)
            .Contains("ENVIRONMENT fault")
            .Because("the exception the suite surfaces must carry the environment diagnostic, not a bare message");
    }

    /// <summary>
    /// A successful start is passed straight through, so the diagnostic wrapper costs nothing
    /// on a healthy host.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PostgresFixtureStart_ContainerStartSucceeds_DoesNotThrow()
    {
        var started = false;

        await PostgresFixture.StartWithRuntimeDiagnosticsAsync(() =>
        {
            started = true;
            return Task.CompletedTask;
        });

        await Assert.That(started)
            .IsTrue()
            .Because("the wrapper must run the supplied start operation and not interfere with success");
    }

    /// <summary>
    /// The container BUILD is covered too, and this is the site that matters: the Testcontainers
    /// builder validates the runtime endpoint at build time, so an unreachable daemon throws
    /// during fixture CONSTRUCTION, before any start is attempted. Wrapping only the start would
    /// leave the diagnostic unreachable in the exact scenario it exists for.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PostgresFixtureBuild_ContainerBuildFails_ThrowsContainerRuntimeUnavailable()
    {
        var underlying = new ArgumentException(
            "Docker is either not running or misconfigured. Please ensure that Docker is running");

        var thrown = Assert.Throws<ContainerRuntimeUnavailableException>(
            () => PostgresFixture.BuildWithRuntimeDiagnostics<object>(() => throw underlying));

        await Assert.That(thrown!.InnerException)
            .IsSameReferenceAs(underlying)
            .Because("the original builder validation failure must remain available for debugging");

        await Assert.That(thrown.Message)
            .Contains("ENVIRONMENT fault")
            .Because(
                "the builder validates the runtime endpoint, so this is the throw a developer with "
                + "no reachable daemon actually sees, and it must carry the environment diagnostic");
    }

    /// <summary>
    /// A successful build is passed straight through and its result returned unchanged.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PostgresFixtureBuild_ContainerBuildSucceeds_ReturnsTheBuiltContainer()
    {
        var built = new object();

        var result = PostgresFixture.BuildWithRuntimeDiagnostics(() => built);

        await Assert.That(result)
            .IsSameReferenceAs(built)
            .Because("the wrapper must return the builder's result untouched on a healthy host");
    }
}
