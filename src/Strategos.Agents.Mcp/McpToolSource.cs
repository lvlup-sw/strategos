// =============================================================================
// <copyright file="McpToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Diagnostics;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Strategos.Agents.Abstractions;
using Strategos.Agents.Exceptions;

namespace Strategos.Agents.Mcp;

/// <summary>
/// Default <see cref="IMcpToolSource"/> implementation wrapping the official C# MCP SDK
/// (<c>ModelContextProtocol.Client</c>). Resolves <see cref="AIFunction"/>s from a remote
/// MCP server so Strategos agents can consume external skill providers.
/// </summary>
/// <remarks>
/// <para>
/// The adapter opens the underlying <see cref="McpClient"/> lazily on the first call to
/// <see cref="GetToolsAsync"/> and reuses it for subsequent calls until the adapter is
/// disposed. Any handshake or transport failure surfaces as an
/// <see cref="AgentMcpException"/> (diagnostic AGAG004) with the endpoint redacted of
/// embedded user-info credentials (DR-10).
/// </para>
/// <para>
/// External cancellation (the token supplied by the caller) propagates as
/// <see cref="OperationCanceledException"/>; only domain-level MCP failures are wrapped.
/// </para>
/// </remarks>
public sealed class McpToolSource : IMcpToolSource, IAsyncDisposable
{
    private readonly Uri _endpoint;
    private readonly TimeSpan _timeout;
    private readonly string _redactedEndpoint;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private McpClient? _client;
    private bool _disposed;

    private McpToolSource(Uri endpoint, TimeSpan timeout)
    {
        _endpoint = endpoint;
        _timeout = timeout;
        _redactedEndpoint = RedactEndpoint(endpoint);
    }

    /// <summary>
    /// Creates an adapter that opens an HTTP/streamable connection to the given MCP
    /// endpoint on the first call to <see cref="GetToolsAsync"/>.
    /// </summary>
    /// <param name="endpoint">The absolute HTTP/HTTPS URI of the MCP server. May contain
    /// user-info credentials, which are stripped before being surfaced on
    /// <see cref="AgentMcpException.RedactedEndpoint"/>.</param>
    /// <param name="timeout">Upper bound on the combined handshake + tool-discovery
    /// duration on each <see cref="GetToolsAsync"/> call.</param>
    public static McpToolSource ForHttpEndpoint(Uri endpoint, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("Endpoint must be an absolute URI.", nameof(endpoint));
        }

        // Uri.Scheme is normalized to lower-case per RFC 3986, so an ordinal compare
        // against the canonical http/https constants is sufficient. The constants are
        // static readonly (not compile-time const), so they cannot appear in a pattern.
        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Endpoint scheme must be http or https, but was '{endpoint.Scheme}'.",
                nameof(endpoint));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
        }

        return new McpToolSource(endpoint, timeout);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            var client = await GetOrCreateClientAsync(cts.Token).ConfigureAwait(false);

            var tools = await client.ListToolsAsync(options: null, cancellationToken: cts.Token).ConfigureAwait(false);

            var result = new List<AIFunction>(tools.Count);
            foreach (var tool in tools)
            {
                result.Add(tool);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-driven cancellation propagates unwrapped — only domain failures wrap.
            throw;
        }
        catch (ObjectDisposedException)
        {
            // Disposal raced with this call; surface the lifecycle violation unwrapped
            // rather than masking it as a domain MCP failure.
            throw;
        }
        catch (Exception ex)
        {
            throw new AgentMcpException(
                "MCP tool discovery failed.",
                _redactedEndpoint,
                ex);
        }
    }

    // Serializes lazy client creation against concurrent GetToolsAsync calls and against
    // DisposeAsync. Without this, two concurrent first-callers could each construct an
    // McpClient (leaking one), or a caller could observe _client mid-disposal. The network
    // round-trip (ListToolsAsync) deliberately runs OUTSIDE the gate so tool discovery is
    // not serialized once the client exists.
    private async Task<McpClient> GetOrCreateClientAsync(CancellationToken cancellationToken)
    {
        var existing = _client;
        if (existing is not null)
        {
            return existing;
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_client is null)
            {
                var transportOptions = new HttpClientTransportOptions
                {
                    Endpoint = StripUserInfo(_endpoint),
                };
                var transport = new HttpClientTransport(transportOptions);
                _client = await McpClient.CreateAsync(
                    transport,
                    clientOptions: null,
                    loggerFactory: null,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return _client;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Swap out the client under the lifecycle gate so a concurrent GetToolsAsync
        // initializer cannot resurrect _client after we decide to dispose it.
        McpClient? client;
        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            client = _client;
            _client = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (client is not null)
        {
            try
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Disposal must never throw to callers; the underlying client may already
                // be in a faulted state from a failed handshake. Surface via Trace so the
                // failure is observable without forcing logger wiring on callers.
                Trace.WriteLine(
                    $"AGAG004 swallowed dispose failure on McpToolSource: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _lifecycleGate.Dispose();
    }

    /// <summary>
    /// Returns a copy of the URI with any user-info segment (user[:password]) removed,
    /// preserving scheme, host, port, path, query, and fragment. Used both to surface a
    /// safe endpoint on <see cref="AgentMcpException.RedactedEndpoint"/> and to hand a
    /// credentials-free URI to the underlying MCP transport (transport-level auth is
    /// configured via <c>HttpClientTransportOptions.OAuth</c> / <c>AdditionalHeaders</c>
    /// rather than URI user-info).
    /// </summary>
    private static Uri StripUserInfo(Uri endpoint)
    {
        if (string.IsNullOrEmpty(endpoint.UserInfo))
        {
            return endpoint;
        }

        var builder = new UriBuilder(endpoint)
        {
            UserName = string.Empty,
            Password = string.Empty,
        };
        return builder.Uri;
    }

    private static string RedactEndpoint(Uri endpoint) => StripUserInfo(endpoint).ToString();
}
