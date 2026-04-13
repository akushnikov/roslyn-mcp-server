using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class SymbolInfoQueryServiceTests
{
    [Fact]
    public async Task GetSymbolInfoAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = new SymbolInfoQueryService(
            new SymbolInfoQueryOperation(
                new TestSymbolInfoQueryDataProvider(),
                NullLogger<SymbolInfoQueryOperation>.Instance));

        var result = await service.GetSymbolInfoAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Symbol);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    [Fact]
    public async Task GetSymbolInfoAsync_MapsProviderFailure_ToFailurePayload()
    {
        var service = new SymbolInfoQueryService(
            new SymbolInfoQueryOperation(
                new TestSymbolInfoQueryDataProvider
                {
                    NextResult = new GetSymbolInfoResult(
                        Symbol: null,
                        FailureReason: "Provider failed.",
                        Guidance: "Retry symbol lookup.")
                },
                NullLogger<SymbolInfoQueryOperation>.Instance));

        var result = await service.GetSymbolInfoAsync(CreateValidRequest());

        Assert.Null(result.Symbol);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry symbol lookup.", result.Guidance);
    }

    private static GetSymbolInfoRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1);

    private sealed class TestSymbolInfoQueryDataProvider : ISymbolInfoQueryDataProvider
    {
        public GetSymbolInfoResult NextResult { get; init; } = new(Symbol: null, FailureReason: null, Guidance: null);

        public ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
            GetSymbolInfoRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
