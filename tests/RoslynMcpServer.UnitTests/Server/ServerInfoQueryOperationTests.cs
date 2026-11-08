using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Server.Requests;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.Server.Operations;

namespace RoslynMcpServer.UnitTests.Server;

public sealed class ServerInfoQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderSnapshotUnchanged()
    {
        var expected = CreateSnapshot();
        var provider = new TestProvider(_ => ValueTask.FromResult(expected));
        using var cts = new CancellationTokenSource();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new GetServerInfoRequest(), cts.Token);

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(cts.Token, provider.ReceivedToken);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNullInternalRequestBeforeProvider()
    {
        var provider = new TestProvider(_ => ValueTask.FromResult(CreateSnapshot()));

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateOperation(provider).ExecuteAsync(null!).AsTask());

        Assert.Equal("request", exception.ParamName);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledTokenSkipsProvider()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestProvider(_ => ValueTask.FromResult(CreateSnapshot()));

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new GetServerInfoRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_InFlightCancellationIsNonErrorEvenWhenProviderCompletes(bool providerThrows)
    {
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<GetServerInfoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new TestProvider(_ =>
        {
            entered.SetResult();
            return new ValueTask<GetServerInfoResult>(completion.Task);
        });
        var logger = new RecordingLogger();
        var pending = new ServerInfoQueryOperation(provider, logger).ExecuteAsync(new GetServerInfoRequest(), cts.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await cts.CancelAsync();
        if (providerThrows)
        {
            completion.SetException(new OperationCanceledException(cts.Token));
        }
        else
        {
            completion.SetResult(CreateSnapshot());
        }

        var (success, error, canceled) = await pending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(cts.Token, provider.ReceivedToken);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal("canceled", entry.State["Outcome"]);
    }

    [Fact]
    public async Task ExecuteAsync_MapsOrdinaryFaultToOriginalExceptionCarrier()
    {
        var expected = new InvalidOperationException("metadata unavailable");
        var provider = new TestProvider(_ => throw expected);

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new GetServerInfoRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(expected, error.Value.Value.SourceException);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesUnrelatedTokenCancellation()
    {
        using var unrelated = new CancellationTokenSource();
        await unrelated.CancelAsync();
        var expected = new OperationCanceledException("another operation canceled", unrelated.Token);
        var provider = new TestProvider(_ => throw expected);

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateOperation(provider).ExecuteAsync(new GetServerInfoRequest()).AsTask());

        Assert.Same(expected, actual);
        Assert.Equal(unrelated.Token, actual.CancellationToken);
        Assert.Equal(1, provider.Calls);
    }

    private static ServerInfoQueryOperation CreateOperation(TestProvider provider) =>
        new(provider, NullLogger<ServerInfoQueryOperation>.Instance);

    private static GetServerInfoResult CreateSnapshot() =>
        new("server", "version", "framework", "os", DateTimeOffset.UtcNow);

    private sealed class TestProvider(Func<CancellationToken, ValueTask<GetServerInfoResult>> handler) : IServerInfoQueryDataProvider
    {
        public int Calls { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }

        public ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            ReceivedToken = cancellationToken;
            return handler(cancellationToken);
        }
    }

    private sealed class RecordingLogger : ILogger<ServerInfoQueryOperation>
    {
        public List<(LogLevel Level, Exception? Exception, Dictionary<string, object?> State)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(state);
            Entries.Add((logLevel, exception, fields.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }
    }
}
