using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.Server;
using RoslynMcpServer.Application.Server.Operations;

namespace RoslynMcpServer.UnitTests.Server;

public sealed class ServerInfoQueryServiceTests
{
    [Fact]
    public async Task GetServerInfoAsync_ReturnsCompleteProviderSnapshotUnchanged()
    {
        var expected = new GetServerInfoResult("server", "version", "framework", "os", DateTimeOffset.UtcNow);
        var provider = new TestProvider(_ => ValueTask.FromResult(expected));
        using var cts = new CancellationTokenSource();

        var result = await CreateService(provider).GetServerInfoAsync(cts.Token);

        Assert.Same(expected, result);
        Assert.Equal("server", result.Name);
        Assert.Equal("version", result.Version);
        Assert.Equal("framework", result.Framework);
        Assert.Equal("os", result.Os);
        Assert.Equal(expected.UtcNow, result.UtcNow);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(cts.Token, provider.ReceivedToken);
    }

    [Fact]
    public async Task GetServerInfoAsync_RethrowsOriginalFaultWithOriginalStack()
    {
        var expected = new InvalidOperationException("original metadata failure");
        var provider = new TestProvider(_ => ThrowFromProvider(expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(provider).GetServerInfoAsync().AsTask());

        Assert.Same(expected, actual);
        Assert.Equal("original metadata failure", actual.Message);
        Assert.Contains(nameof(ThrowFromProvider), actual.StackTrace!);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetServerInfoAsync_PreCanceledTokenThrowsWithCallerTokenAndSkipsProvider()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestProvider(_ => throw new InvalidOperationException("provider must not execute"));

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateService(provider).GetServerInfoAsync(cts.Token).AsTask());

        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetServerInfoAsync_InFlightCancellationThrowsWithCallerToken(bool providerThrows)
    {
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<GetServerInfoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new TestProvider(_ =>
        {
            entered.SetResult();
            return new ValueTask<GetServerInfoResult>(completion.Task);
        });
        var pending = CreateService(provider).GetServerInfoAsync(cts.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await cts.CancelAsync();
        if (providerThrows)
        {
            completion.SetException(new OperationCanceledException(cts.Token));
        }
        else
        {
            completion.SetResult(new GetServerInfoResult("server", "version", "framework", "os", DateTimeOffset.UtcNow));
        }

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.Equal(cts.Token, provider.ReceivedToken);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetServerInfoAsync_PropagatesUnrelatedCancellationUnchanged()
    {
        using var unrelated = new CancellationTokenSource();
        await unrelated.CancelAsync();
        var expected = new OperationCanceledException("unrelated cancellation", unrelated.Token);
        var provider = new TestProvider(_ => throw expected);

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateService(provider).GetServerInfoAsync().AsTask());

        Assert.Same(expected, actual);
        Assert.Equal(unrelated.Token, actual.CancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ValueTask<GetServerInfoResult> ThrowFromProvider(Exception exception) => throw exception;

    private static ServerInfoQueryService CreateService(TestProvider provider) =>
        new(new ServerInfoQueryOperation(provider, NullLogger<ServerInfoQueryOperation>.Instance));

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
}
