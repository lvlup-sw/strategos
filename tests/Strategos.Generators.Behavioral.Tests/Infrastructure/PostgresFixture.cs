// -----------------------------------------------------------------------
// <copyright file="PostgresFixture.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Testcontainers.PostgreSql;

namespace Strategos.Generators.Behavioral.Tests.Infrastructure;

/// <summary>
/// Owns the single PostgreSQL container shared across the behavioral-test
/// suite. Later DR-9 tasks compile the generated Wolverine+Marten saga and
/// run it against this database to assert runtime behavior; this fixture is
/// the harness backbone that proves a real database can be stood up.
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle is driven by TUnit: <see cref="InitializeAsync"/> (from
/// <see cref="IAsyncInitializer"/>) starts exactly one container and
/// <see cref="DisposeAsync"/> tears it down. Share a single instance across
/// the whole session by injecting it with
/// <c>[ClassDataSource&lt;PostgresFixture&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </para>
/// <para>
/// The Docker provider on the target environment is <b>podman</b> (rootless).
/// Testcontainers .NET talks the Docker API, so the fixture points it at the
/// active rootless podman socket and disables Ryuk (the resource-reaper
/// sidecar Ryuk is unreliable under rootless podman). These are set as
/// process environment variables before the container is built so the harness
/// is self-configuring regardless of how the test host is launched.
/// </para>
/// <para>
/// The podman redirect is applied only when ALL of the following hold, so a
/// Docker-only host using Testcontainers' default discovery is never broken:
/// <list type="number">
///   <item><description>
///     <c>DOCKER_HOST</c> is not already set — an explicit override or a CI
///     runner's Docker config always wins.
///   </description></item>
///   <item><description>
///     The rootless podman socket actually exists on disk — otherwise the
///     redirect would point Testcontainers at a non-existent socket and break
///     a Docker-only runner.
///   </description></item>
///   <item><description>
///     The host is Linux — the rootless <c>/run/user/&lt;uid&gt;/podman</c>
///     socket layout is Linux-specific.
///   </description></item>
/// </list>
/// When the redirect is skipped, Testcontainers' own provider discovery
/// (default Docker socket / <c>DOCKER_HOST</c>) is left untouched.
/// </para>
/// </remarks>
public sealed class PostgresFixture : IAsyncInitializer, IAsyncDisposable
{
    /// <summary>
    /// Default rootless podman API socket path on disk. Derived from the current
    /// user id so it resolves to <c>/run/user/&lt;uid&gt;/podman/podman.sock</c>,
    /// matching <c>podman info --format '{{.Host.RemoteSocket.Path}}'</c>. Used
    /// both to probe for the socket's existence and to build the
    /// <c>DOCKER_HOST</c> URI.
    /// </summary>
    private static readonly string DefaultPodmanSocketPath =
        $"/run/user/{GetCurrentUserId()}/podman/podman.sock";

    /// <summary>
    /// The <c>unix://</c> <c>DOCKER_HOST</c> URI for the rootless podman socket
    /// at <see cref="DefaultPodmanSocketPath"/>.
    /// </summary>
    private static readonly string DefaultPodmanSocketUri =
        $"unix://{DefaultPodmanSocketPath}";

    private readonly PostgreSqlContainer container;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresFixture"/> class,
    /// configuring the Docker provider for rootless podman and building (but
    /// not yet starting) the PostgreSQL container.
    /// </summary>
    /// <remarks>
    /// The builder validates the container-runtime endpoint at <c>Build()</c>, so an
    /// unreachable daemon fails HERE, in the constructor, before anything is started. That is
    /// why the diagnostic wraps the build as well as the start: fixture construction is the
    /// site the failure actually surfaces from, and TUnit reports it as "failed to expand data
    /// source" for every test in the suite at once — the shape most easily mistaken for a
    /// regression in the code under test.
    /// </remarks>
    public PostgresFixture()
    {
        ConfigurePodmanProvider();

        // Ryuk (the reaper sidecar) is disabled under rootless podman (see
        // ConfigurePodmanProvider), so cleanup is driven by WithCleanUp(true)
        // + the explicit DisposeAsync below. WithAutoRemove(true) is NOT used:
        // under podman the daemon removes the container on stop and then
        // Testcontainers' own remove-on-dispose races it ("no such container",
        // HTTP 500) — a noisy teardown error. A cleanly stopped (Exited)
        // container is the harmless, standard Ryuk-disabled tradeoff.
        this.container = BuildWithRuntimeDiagnostics(() =>
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithCleanUp(true)
                .Build());
    }

    /// <summary>
    /// Gets the connection string for the running container's database. Only
    /// valid after <see cref="InitializeAsync"/> has completed.
    /// </summary>
    public string ConnectionString => this.container.GetConnectionString();

    /// <summary>
    /// Starts the single shared PostgreSQL container.
    /// </summary>
    /// <remarks>
    /// A container-start failure is rethrown as a
    /// <see cref="ContainerRuntimeUnavailableException"/> carrying a diagnostic that names
    /// the container runtime. Without that, a developer with no reachable daemon sees the
    /// whole behavioral suite red at fixture initialization, which reads exactly like a real
    /// regression in the code under test.
    /// </remarks>
    /// <returns>A task that completes when the container is ready.</returns>
    public Task InitializeAsync() =>
        StartWithRuntimeDiagnosticsAsync(() => this.container.StartAsync());

    /// <summary>
    /// Runs a container start and converts any failure into a
    /// <see cref="ContainerRuntimeUnavailableException"/> whose message describes the
    /// container runtime rather than the workflow under test.
    /// </summary>
    /// <param name="startContainer">The container-start operation.</param>
    /// <returns>A task that completes when the container is ready.</returns>
    internal static async Task StartWithRuntimeDiagnosticsAsync(Func<Task> startContainer)
    {
        ArgumentNullException.ThrowIfNull(startContainer);

        try
        {
            await startContainer().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ContainerRuntimeUnavailableException)
        {
            throw new ContainerRuntimeUnavailableException(DescribeContainerRuntimeFailure(ex), ex);
        }
    }

    /// <summary>
    /// Runs a container build and converts any failure into a
    /// <see cref="ContainerRuntimeUnavailableException"/>. The builder validates the runtime
    /// endpoint, so this — not the start — is where an unreachable daemon is first seen.
    /// </summary>
    /// <typeparam name="TContainer">The container type being built.</typeparam>
    /// <param name="buildContainer">The container-build operation.</param>
    /// <returns>The built container.</returns>
    internal static TContainer BuildWithRuntimeDiagnostics<TContainer>(Func<TContainer> buildContainer)
    {
        ArgumentNullException.ThrowIfNull(buildContainer);

        try
        {
            return buildContainer();
        }
        catch (Exception ex) when (ex is not ContainerRuntimeUnavailableException)
        {
            throw new ContainerRuntimeUnavailableException(DescribeContainerRuntimeFailure(ex), ex);
        }
    }

    /// <summary>
    /// Builds the container-runtime diagnostic for the CURRENT environment.
    /// </summary>
    /// <param name="failure">The underlying container build or start failure.</param>
    /// <returns>The diagnostic message.</returns>
    internal static string DescribeContainerRuntimeFailure(Exception failure) =>
        DescribeContainerRuntimeFailure(
            failure,
            Environment.GetEnvironmentVariable("DOCKER_HOST"),
            DefaultPodmanSocketPath,
            File.Exists);

    /// <summary>
    /// Builds the container-runtime diagnostic from an explicitly supplied environment, so
    /// the message can be asserted without a container runtime being present either way.
    /// </summary>
    /// <param name="failure">The underlying container build or start failure.</param>
    /// <param name="dockerHost">The resolved <c>DOCKER_HOST</c>, or <see langword="null"/> when unset.</param>
    /// <param name="podmanSocketPath">The rootless podman socket path the fixture probes.</param>
    /// <param name="socketExists">Probe for whether a socket path exists on disk.</param>
    /// <returns>The diagnostic message.</returns>
    internal static string DescribeContainerRuntimeFailure(
        Exception failure,
        string? dockerHost,
        string podmanSocketPath,
        Func<string, bool> socketExists)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(socketExists);

        var podmanSocketPresent = socketExists(podmanSocketPath);
        var dockerHostSocketPath = TryGetUnixSocketPath(dockerHost);
        var dockerHostSocketPresent = dockerHostSocketPath is null
            ? (bool?)null
            : socketExists(dockerHostSocketPath);

        // A configured DOCKER_HOST is not the same as a reachable one — an explicit override
        // pointing at a socket that is not there is the most confusing case of all, because
        // the variable being set makes the environment LOOK configured. Probing the disk is
        // not a handshake: an endpoint that is not on disk certainly cannot serve, while one
        // that is may still fail for its own reasons, and a non-unix endpoint cannot be probed
        // at all. The message says which of the three it is rather than guessing.
        var endpointReachable = dockerHostSocketPresent
            ?? (!string.IsNullOrEmpty(dockerHost) || podmanSocketPresent);

        var lines = new List<string>
        {
            "The behavioral-test container runtime could not provide the PostgreSQL container.",
            string.Empty,
            "This is an ENVIRONMENT fault, not a regression in the code under test. Every test in",
            "this suite depends on the same container, so all of them fail together for this one",
            "reason; do not read a whole-suite red here as a break in the generator or the saga.",
            string.Empty,
            "  Container runtime endpoint",
            $"    DOCKER_HOST            : {(string.IsNullOrEmpty(dockerHost) ? "(not set)" : dockerHost)}",
        };

        if (dockerHostSocketPath is not null)
        {
            lines.Add(
                $"    DOCKER_HOST socket     : {(dockerHostSocketPresent!.Value ? "present on disk" : "MISSING on disk")}");
        }

        lines.Add($"    Probed podman socket   : {podmanSocketPath}");
        lines.Add($"    Socket present on disk : {(podmanSocketPresent ? "yes" : "no")}");
        lines.Add(string.Empty);

        if (dockerHostSocketPresent == false)
        {
            lines.Add("NO container-runtime endpoint was resolved: DOCKER_HOST is set but names a socket that");
            lines.Add("is not on disk, so nothing was listening. An explicit DOCKER_HOST always wins over the");
            lines.Add("fixture's own podman discovery, so this override is what was used — correct or unset it.");
        }
        else if (endpointReachable)
        {
            lines.Add("A container-runtime endpoint was resolved, so the failure below is the runtime's own");
            lines.Add("(the daemon may be stopped, out of resources, or unable to pull postgres:16-alpine).");
        }
        else
        {
            lines.Add("NO container-runtime endpoint was resolved: DOCKER_HOST is unset and no rootless podman");
            lines.Add("socket is present at the probed path, so nothing was listening to start a container.");
        }

        lines.Add(string.Empty);
        lines.Add("On a rootless podman host the socket must be named explicitly — the default");
        lines.Add("/var/run/docker.sock points at the ROOTFUL socket, which is typically not running:");
        lines.Add(string.Empty);
        lines.Add("    export DOCKER_HOST=unix:///run/user/$(id -u)/podman/podman.sock");
        lines.Add("    export TESTCONTAINERS_RYUK_DISABLED=true");
        lines.Add(string.Empty);
        lines.Add($"Underlying failure: {failure.GetType().FullName}: {failure.Message}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Extracts the on-disk socket path from a <c>unix://</c> <c>DOCKER_HOST</c> URI.
    /// </summary>
    /// <param name="dockerHost">The <c>DOCKER_HOST</c> value, or <see langword="null"/> when unset.</param>
    /// <returns>
    /// The socket path, or <see langword="null"/> when <c>DOCKER_HOST</c> is unset or names a
    /// transport that cannot be probed on disk (for example <c>tcp://</c> or <c>npipe://</c>).
    /// A <c>unix://</c> URI with an EMPTY path returns that empty string rather than
    /// <see langword="null"/>: it is a probeable unix endpoint that names no socket, so it must
    /// reach the probe and fail it. Collapsing it to <see langword="null"/> would route it down
    /// the unprobeable-transport arm, where a non-empty <c>DOCKER_HOST</c> alone is taken as a
    /// resolved endpoint — and the diagnostic would then blame a healthy daemon.
    /// </returns>
    private static string? TryGetUnixSocketPath(string? dockerHost)
    {
        const string UnixScheme = "unix://";

        if (string.IsNullOrEmpty(dockerHost)
            || !dockerHost.StartsWith(UnixScheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Deliberately NOT collapsed to null when empty: File.Exists("") is false (documented
        // for zero-length paths), so `unix://` correctly probes as a missing socket.
        return dockerHost[UnixScheme.Length..];
    }

    /// <summary>
    /// Stops and disposes the shared container.
    /// </summary>
    /// <returns>A value task that completes when the container is disposed.</returns>
    public ValueTask DisposeAsync() => this.container.DisposeAsync();

    /// <summary>
    /// Points Testcontainers at the rootless podman socket and disables Ryuk —
    /// but only on a host that actually has a rootless podman socket and no
    /// pre-existing <c>DOCKER_HOST</c>. A Docker-only host (or an explicit
    /// override) is left entirely to Testcontainers' default discovery so the
    /// redirect cannot point it at a non-existent socket.
    /// </summary>
    private static void ConfigurePodmanProvider()
    {
        // Guard (a): an explicit override or a CI runner's Docker config wins.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return;
        }

        // Guard (c): the rootless /run/user/<uid>/podman socket layout is
        // Linux-specific. (b): only redirect if that socket actually exists, so
        // a Docker-only Linux runner using default discovery is untouched.
        if (!OperatingSystem.IsLinux() || !File.Exists(DefaultPodmanSocketPath))
        {
            return;
        }

        Environment.SetEnvironmentVariable("DOCKER_HOST", DefaultPodmanSocketUri);

        // Ryuk (the Testcontainers resource reaper) commonly fails to start
        // under rootless podman; disable it so the run is not blocked. Only
        // applied alongside the podman redirect (and only when not already set),
        // so a Docker host keeps its default Ryuk behavior. The container's own
        // WithCleanUp(true) still removes the container on dispose.
        SetIfAbsent("TESTCONTAINERS_RYUK_DISABLED", "true");
    }

    /// <summary>
    /// Sets an environment variable for the current process only if it is not
    /// already set (so an explicit override or CI Docker config wins).
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The value to apply when absent.</param>
    private static void SetIfAbsent(string name, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    /// <summary>
    /// Resolves the current effective user id for the rootless podman socket
    /// path. Falls back to <c>1000</c> when the platform does not expose it.
    /// </summary>
    /// <returns>The numeric user id as a string.</returns>
    private static string GetCurrentUserId()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var uid = Libc.GetEffectiveUserId();
            if (uid >= 0)
            {
                return uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return "1000";
    }

    /// <summary>
    /// Minimal interop for the POSIX effective user id, used to build the
    /// rootless podman socket path.
    /// </summary>
    private static class Libc
    {
        /// <summary>
        /// Returns the effective user id, or <c>-1</c> if interop fails.
        /// </summary>
        /// <returns>The effective user id, or <c>-1</c> on failure.</returns>
        public static int GetEffectiveUserId()
        {
            try
            {
                return Geteuid();
            }
            catch (DllNotFoundException)
            {
                return -1;
            }
            catch (EntryPointNotFoundException)
            {
                return -1;
            }
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "geteuid")]
        private static extern int Geteuid();
    }
}
