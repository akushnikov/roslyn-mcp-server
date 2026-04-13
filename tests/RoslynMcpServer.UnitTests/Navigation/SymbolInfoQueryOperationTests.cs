using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class SymbolInfoQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new GetSymbolInfoResult(Symbol: null, FailureReason: null, Guidance: null);
        var provider = new TestSymbolInfoQueryDataProvider
        {
            NextResult = expected
        };
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_AndSkipsProvider()
    {
        var provider = new TestSymbolInfoQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(
            CreateValidRequest() with { Line = 0 });
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("Line and column must be positive 1-based values.", error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestSymbolInfoQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not resolve symbol information.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenCancellationRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = new TestSymbolInfoQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    private static SymbolInfoQueryOperation CreateOperation(TestSymbolInfoQueryDataProvider provider) =>
        new(provider, NullLogger<SymbolInfoQueryOperation>.Instance);

    private static GetSymbolInfoRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1);

    private sealed class TestSymbolInfoQueryDataProvider : ISymbolInfoQueryDataProvider
    {
        public int Calls { get; private set; }

        public GetSymbolInfoResult NextResult { get; init; } = new(Symbol: null, FailureReason: null, Guidance: null);

        public Exception? NextException { get; init; }

        public ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
            GetSymbolInfoRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();

            return NextException is not null 
                ? throw NextException 
                : ValueTask.FromResult(NextResult);
        }
    }
}
