using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class SearchSymbolsQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new SearchSymbolsResult(
            Symbols: Array.Empty<SymbolDescriptor>(),
            FailureReason: null,
            Guidance: null);
        var provider = new TestSearchSymbolsQueryDataProvider
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
        var provider = new TestSearchSymbolsQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(
            CreateValidRequest() with { Query = "   " });
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("A non-empty query is required.", error.Value.Value.FailureReason);
        Assert.Equal("Pass part of a symbol name and optionally a kindFilter.", error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenMaxResultsIsZero()
    {
        var provider = new TestSearchSymbolsQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(
            CreateValidRequest() with { MaxResults = 0 });
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("MaxResults must be a positive value.", error.Value.Value.FailureReason);
        Assert.Equal("Pass a positive integer (e.g., the default of 50).", error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestSearchSymbolsQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not complete the symbol search.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenCancellationRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = new TestSearchSymbolsQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    private static SearchSymbolsQueryOperation CreateOperation(TestSearchSymbolsQueryDataProvider provider) =>
        new(provider, NullLogger<SearchSymbolsQueryOperation>.Instance);

    private static SearchSymbolsRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            Query: "My",
            KindFilter: null,
            MaxResults: 50);

    private sealed class TestSearchSymbolsQueryDataProvider : ISearchSymbolsQueryDataProvider
    {
        public int Calls { get; private set; }

        public SearchSymbolsResult NextResult { get; init; } = new(
            Symbols: Array.Empty<SymbolDescriptor>(),
            FailureReason: null,
            Guidance: null);

        public Exception? NextException { get; init; }

        public ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
            SearchSymbolsRequest request,
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
