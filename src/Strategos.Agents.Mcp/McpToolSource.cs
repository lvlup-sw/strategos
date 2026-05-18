// =============================================================================
// <copyright file="McpToolSource.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

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
                    cancellationToken: cts.Token).ConfigureAwait(false);
            }

            var tools = await _client.ListToolsAsync(options: null, cancellationToken: cts.Token).ConfigureAwait(false);

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
        catch (Exception ex)
        {
            throw new AgentMcpException(
                "MCP tool discovery failed.",
                _redactedEndpoint,
                ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_client is not null)
        {
            try
            {
                await _client.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Disposal must never throw to callers; the underlying client may already
                // be in a faulted state from a failed handshake.
            }

            _client = null;
        }
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
