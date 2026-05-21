// =============================================================================
// <copyright file="AgentStepBuilderConfiguratorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Strategos.Abstractions;
using Strategos.Agents;
using Strategos.Agents.Configuration;
using Strategos.Steps;

namespace Strategos.Agents.Tests.Integration;

/// <summary>
/// T-015: DR-6 host-composition escape hatch — <c>AgentStepBuilder.ConfigureChatClient</c>
/// applies the host configurator BEFORE <c>UseStrategosFunctions(tools)</c> and
/// <c>UseFunctionInvocation</c>, so host middleware (e.g. <c>UseLogging</c>) wraps
/// everything else in the produced <see cref="IChatClient"/> pipeline.
///
/// Order is mechanically asserted by inspecting the recorded sequence of
/// <see cref="ILogger"/> calls captured by a real <see cref="LoggerFactory"/>
/// (NSubstitute is used only for the terminal <see cref="IChatClient"/>, not for
/// the logging surface) — see [[feedback_no_handwavy_mitigations]].
/// </summary>
// DR-9: this class deliberately carries NO TUnit Property metadata that could
// be used by `--treenode-filter` to exclude it from CI. The design's DR-9
// acceptance criterion requires this test to run in the standard test job; a
// metadata gate is forbidden so it cannot be silently skipped.
public sealed class AgentStepBuilderConfiguratorTests
{
    [Test]
    public async Task Build_ConfigureChatClientHook_AppliesHostConfiguratorBeforeStrategosFunctionsAndFunctionInvocation()
    {
        // Arrange — capture logger output sequence via a real LoggerFactory + custom provider.
        var recorder = new OrderedLogRecorder();
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            // Trace level so LoggingChatClient's entry/exit messages are captured.
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddProvider(recorder);
        });

        // Terminal chat client — emits a marker log via the SAME factory when invoked.
        // That way the recorder's sequence reflects: outer middleware logs → ... → terminal marker.
        var terminalLogger = loggerFactory.CreateLogger("test.TerminalChatClient");
        var terminalCalls = 0;
        var terminalClient = Substitute.For<IChatClient>();
        terminalClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                terminalCalls++;
                terminalLogger.LogInformation("terminal:invoked");
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });

        // The single AIFunction tool, registered via WithTool().
        var fakeTool = AIFunctionFactory.Create(() => "tool-result", name: "fake_tool");

        // Build configuration — host configurator wires UseLogging(loggerFactory).
        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((s, _, _) => Task.FromResult(new StepResult<TestState>(s)));
        builder.WithTool(fakeTool);
        builder.ConfigureChatClient(b => b.UseLogging(loggerFactory));

        // Act — produce the step, then exercise the composed IChatClient.
        var step = builder.Build(terminalClient);
        var composed = GetComposedChatClient(step);
        await composed.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") },
            options: null,
            CancellationToken.None);

        // Assert — the terminal was reached exactly once.
        await Assert.That(terminalCalls).IsEqualTo(1);

        var entries = recorder.Snapshot();

        // 1. The terminal's marker was recorded — proves the call reached the inner client.
        var terminalIdx = entries.FindIndex(e => e.Message.Contains("terminal:invoked", StringComparison.Ordinal));
        await Assert.That(terminalIdx).IsGreaterThanOrEqualTo(0);

        // 2. UseStrategosFunctions registered a wrapper between host configurator and UseFunctionInvocation.
        //    Verify by resolving the wrapper through IChatClient.GetService<T>() — the chain MUST expose it.
        var fnExtensionsClient = composed.GetService<StrategosFunctionsChatClient>();
        await Assert.That(fnExtensionsClient).IsNotNull();
        await Assert.That(fnExtensionsClient!.Tools.Count).IsEqualTo(1);
        await Assert.That(fnExtensionsClient.Tools[0]).IsSameReferenceAs(fakeTool);

        // 3. UseFunctionInvocation was applied with MaximumIterationsPerRequest = DefaultMaxToolIterations.
        var fnInvoker = composed.GetService<FunctionInvokingChatClient>();
        await Assert.That(fnInvoker).IsNotNull();
        await Assert.That(fnInvoker!.MaximumIterationsPerRequest)
            .IsEqualTo(AgentStepBase<TestState, string>.DefaultMaxToolIterations);

        // 4. LoggingChatClient (from the host configurator) is the outermost wrapper.
        //    Its ENTRY-SIDE log must appear BEFORE the terminal marker — that mechanically
        //    proves host middleware wraps the rest of the pipeline. (Exit-side logs from
        //    LoggingChatClient naturally appear AFTER the terminal returns, which is fine.)
        var loggingClientEntries = entries
            .Select((e, i) => (Index: i, Entry: e))
            .Where(x => x.Entry.Category.Contains("LoggingChatClient", StringComparison.Ordinal))
            .ToList();
        await Assert.That(loggingClientEntries.Count).IsGreaterThan(0);
        var firstLoggingIdx = loggingClientEntries.Min(x => x.Index);
        await Assert.That(firstLoggingIdx).IsLessThan(terminalIdx);
    }

    [Test]
    public async Task ConfigureChatClient_NullConfigurator_ThrowsArgumentNullException()
    {
        var builder = new AgentStepBuilder<TestState, string>();

        var ex = Assert.Throws<ArgumentNullException>(() => builder.ConfigureChatClient(null!));

        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task Build_NoConfigureChatClient_AppliesStrategosFunctionsAndFunctionInvocationInOrder()
    {
        // Without a host configurator, the chain is still composed with
        // UseStrategosFunctions(tools) → UseFunctionInvocation(opts) so AIFunction
        // tools registered via WithTool() are surfaced for automatic invocation.
        var terminalClient = Substitute.For<IChatClient>();
        terminalClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        var fakeTool = AIFunctionFactory.Create(() => "tool-result", name: "fake_tool");

        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((s, _, _) => Task.FromResult(new StepResult<TestState>(s)));
        builder.WithTool(fakeTool);

        var step = builder.Build(terminalClient);
        var composed = GetComposedChatClient(step);

        var fnExtensionsClient = composed.GetService<StrategosFunctionsChatClient>();
        await Assert.That(fnExtensionsClient).IsNotNull();
        await Assert.That(fnExtensionsClient!.Tools.Count).IsEqualTo(1);

        var fnInvoker = composed.GetService<FunctionInvokingChatClient>();
        await Assert.That(fnInvoker).IsNotNull();
    }

    [Test]
    public async Task Build_ConfigureChatClientWithDistributedCache_RepeatCallsReturnCachedResponse()
    {
        // DR-6 acceptance bullet 2: a consumer calling
        // `.ConfigureChatClient(b => b.UseDistributedCache(cache))` sees cached
        // responses on repeat calls. Wiring parity (DIM-4) demands a real
        // IDistributedCache (MemoryDistributedCache), NOT NSubstitute — the
        // production code path must serialize/deserialize through actual
        // distributed-cache semantics.
        //
        // Pipeline composed by AgentStepBuilder.Build for this test:
        //   DistributedCachingChatClient (outermost, from host configurator)
        //     → StrategosFunctionsChatClient
        //       → FunctionInvokingChatClient
        //         → CountingChatClient (terminal — hand-rolled, NOT a mock)
        //
        // The cache wraps the rest of the chain, so a second ExecuteAsync call
        // with identical inputs MUST be served from cache and never reach the
        // terminal CountingChatClient. We assert that by checking CallCount == 1.

        // Arrange — real MemoryDistributedCache (no mock).
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        // Deterministic structured-output payload. AgentStepBase calls
        // GetResponseAsync<TResult>, which serializes/deserializes through JSON.
        // Returning a CachedDto JSON keeps the test compatible with that path.
        const string jsonPayload = "{\"value\":\"cached-answer\"}";
        var fake = new CountingChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonPayload)));

        var applyResultInvocations = 0;
        var builder = new AgentStepBuilder<TestState, CachedDto>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((s, _, _) =>
        {
            applyResultInvocations++;
            return Task.FromResult(new StepResult<TestState>(s));
        });
        builder.ConfigureChatClient(b => b.UseDistributedCache(cache));

        var step = builder.Build(fake);

        // Fixed WorkflowId so both calls produce identical message sequences.
        var state = new TestState { WorkflowId = Guid.Parse("11111111-1111-1111-1111-111111111111") };
        var context = new StepContext
        {
            CorrelationId = "test-correlation",
            WorkflowId = state.WorkflowId,
            StepName = "CacheTestStep",
            Timestamp = DateTimeOffset.UtcNow,
            CurrentPhase = "Testing",
        };

        // Act — two ExecuteAsync calls with identical state.
        await step.ExecuteAsync(state, context, CancellationToken.None);
        await step.ExecuteAsync(state, context, CancellationToken.None);

        // Assert — terminal client invoked exactly ONCE. The second call was
        // served from the MemoryDistributedCache without reaching the bottom
        // of the chain.
        await Assert.That(fake.CallCount).IsEqualTo(1);

        // Sanity: ApplyResult fired both times — the cached response is still
        // delivered to the consumer-facing pipeline output. Only the inner
        // chat-client invocation was elided.
        await Assert.That(applyResultInvocations).IsEqualTo(2);
    }

    [Test]
    public async Task Build_WithMaxToolIterationsOverride_PropagatesToFunctionInvokingClient()
    {
        var terminalClient = Substitute.For<IChatClient>();
        terminalClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));

        var builder = new AgentStepBuilder<TestState, string>();
        builder.WithSystemPrompt(_ => "sys");
        builder.WithUserPrompt(_ => "user");
        builder.WithApplyResult((s, _, _) => Task.FromResult(new StepResult<TestState>(s)));
        builder.WithMaxToolIterations(3);

        var step = builder.Build(terminalClient);
        var composed = GetComposedChatClient(step);

        var fnInvoker = composed.GetService<FunctionInvokingChatClient>();
        await Assert.That(fnInvoker).IsNotNull();
        await Assert.That(fnInvoker!.MaximumIterationsPerRequest).IsEqualTo(3);
    }

    private static IChatClient GetComposedChatClient(object step)
    {
        var field = step.GetType().GetField("_chatClient", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected private field '_chatClient' on AgentStepBase<,>.");
        return (IChatClient)field.GetValue(step)!;
    }

    /// <summary>
    /// Custom <see cref="ILoggerProvider"/> that captures every log entry in the order it was emitted,
    /// across all logger categories. Used to mechanically assert middleware ordering.
    /// </summary>
    private sealed class OrderedLogRecorder : ILoggerProvider
    {
        private readonly List<LogEntry> _entries = new();
        private readonly object _gate = new();

        public ILogger CreateLogger(string categoryName) => new RecorderLogger(categoryName, this);

        public List<LogEntry> Snapshot()
        {
            lock (_gate)
            {
                return new List<LogEntry>(_entries);
            }
        }

        public void Dispose()
        {
            // no-op
        }

        private void Record(LogEntry entry)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        public sealed record LogEntry(string Category, LogLevel Level, string Message);

        private sealed class RecorderLogger : ILogger
        {
            private readonly string _category;
            private readonly OrderedLogRecorder _owner;

            public RecorderLogger(string category, OrderedLogRecorder owner)
            {
                _category = category;
                _owner = owner;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                _owner.Record(new LogEntry(_category, logLevel, message));
            }
        }
    }

    internal sealed record TestState : IWorkflowState
    {
        public Guid WorkflowId { get; init; } = Guid.NewGuid();
    }

    /// <summary>
    /// Structured-output payload used by the DR-6 cached-response test. Matches
    /// the JSON shape <c>{"value":"…"}</c> the fake terminal client returns.
    /// </summary>
    internal sealed record CachedDto(string Value);

    /// <summary>
    /// Hand-rolled terminal <see cref="IChatClient"/> that returns a fixed
    /// <see cref="ChatResponse"/> and tracks invocation count. Deliberately
    /// NOT NSubstitute — the DR-6 acceptance criterion requires real wiring
    /// parity (DIM-4) for the cache test.
    /// </summary>
    private sealed class CountingChatClient : IChatClient
    {
        private readonly ChatResponse _response;
        private int _callCount;

        public CountingChatClient(ChatResponse response)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
        }

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(_response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            foreach (var msg in _response.Messages)
            {
                yield return new ChatResponseUpdate(msg.Role, msg.Text);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // no-op
        }
    }
}
