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
        this.container = new PostgreSqlBuilder("postgres:16-alpine")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the connection string for the running container's database. Only
    /// valid after <see cref="InitializeAsync"/> has completed.
    /// </summary>
    public string ConnectionString => this.container.GetConnectionString();

    /// <summary>
    /// Starts the single shared PostgreSQL container.
    /// </summary>
    /// <returns>A task that completes when the container is ready.</returns>
    public Task InitializeAsync() => this.container.StartAsync();

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
