using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class SearchSymbolsQueryServiceTests
{
    [Fact]
    public async Task SearchSymbolsAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = new SearchSymbolsQueryService(
            new SearchSymbolsQueryOperation(
                new TestSearchSymbolsQueryDataProvider(),
                NullLogger<SearchSymbolsQueryOperation>.Instance));

        var result = await service.SearchSymbolsAsync(CreateValidRequest(), cts.Token);

        Assert.Empty(result.Symbols);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    [Fact]
    public async Task SearchSymbolsAsync_MapsProviderFailure_ToFailurePayload()
    {
        var service = new SearchSymbolsQueryService(
            new SearchSymbolsQueryOperation(
                new TestSearchSymbolsQueryDataProvider
                {
                    NextResult = new SearchSymbolsResult(
                        Symbols: Array.Empty<SymbolDescriptor>(),
                        FailureReason: "Provider failed.",
                        Guidance: "Retry symbol search.")
                },
                NullLogger<SearchSymbolsQueryOperation>.Instance));

        var result = await service.SearchSymbolsAsync(CreateValidRequest());

        Assert.Empty(result.Symbols);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry symbol search.", result.Guidance);
    }

    private static SearchSymbolsRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            Query: "My",
            KindFilter: null,
            MaxResults: 50);

    private sealed class TestSearchSymbolsQueryDataProvider : ISearchSymbolsQueryDataProvider
    {
        public SearchSymbolsResult NextResult { get; init; } = new(
            Symbols: Array.Empty<SymbolDescriptor>(),
            FailureReason: null,
            Guidance: null);

        public ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
            SearchSymbolsRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
