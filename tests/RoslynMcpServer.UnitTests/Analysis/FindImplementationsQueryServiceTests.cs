using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class FindImplementationsQueryServiceTests
{
    [Fact]
    public async Task FindImplementationsAsync_PassesSuccessfulPayloadThrough()
    {
        var symbol = CreateSymbol();
        var expected = new FindImplementationsResult(symbol, new[] { symbol }, null, null);
        var provider = new TestDataProvider { NextResult = expected };

        var result = await CreateService(provider).FindImplementationsAsync(CreateValidRequest());

        Assert.Same(expected, result);
        Assert.Same(symbol, result.Symbol);
        Assert.Same(expected.Implementations, result.Implementations);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FindImplementationsAsync_ProjectsProviderFailure_WithSymbolAndEmptyImplementations(bool hasSymbol)
    {
        var symbol = hasSymbol ? CreateSymbol() : null;
        var provider = new TestDataProvider
        {
            NextResult = new FindImplementationsResult(symbol, new[] { CreateSymbol() }, "Provider failed.", "Retry lookup.")
        };

        var result = await CreateService(provider).FindImplementationsAsync(CreateValidRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.IsAssignableFrom<IReadOnlyList<SymbolDescriptor>>(result.Implementations);
        Assert.Empty(result.Implementations);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry lookup.", result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task FindImplementationsAsync_ProjectsValidationFailure_WithoutCallingProvider()
    {
        var provider = new TestDataProvider();

        var result = await CreateService(provider).FindImplementationsAsync(CreateValidRequest() with { MaxResults = 0 });

        Assert.Null(result.Symbol);
        Assert.Empty(result.Implementations);
        Assert.Equal("MaxResults must be a positive value.", result.FailureReason);
        Assert.Equal("Pass a positive integer (e.g., the default of 100).", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task FindImplementationsAsync_ProjectsUnexpectedFault_WithoutBackendDetails()
    {
        var provider = new TestDataProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var result = await CreateService(provider).FindImplementationsAsync(CreateValidRequest());

        Assert.Null(result.Symbol);
        Assert.Empty(result.Implementations);
        Assert.Equal("The server could not compute implementations for the resolved symbol.", result.FailureReason);
        Assert.Equal("Retry the request on an interface, abstract member, or base type.", result.Guidance);
        Assert.DoesNotContain("sensitive", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task FindImplementationsAsync_ProjectsPrecancellation_ToStablePayload()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new TestDataProvider();

        var result = await CreateService(provider).FindImplementationsAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.Implementations);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    private static FindImplementationsQueryService CreateService(TestDataProvider provider) =>
        new(new FindImplementationsQueryOperation(provider, NullLogger<FindImplementationsQueryOperation>.Instance));

    private static FindImplementationsRequest CreateValidRequest() => new("Sample.sln", "File.cs", 1, 1);

    private static SymbolDescriptor CreateSymbol() =>
        new("IWorker", "Interface", "IWorker", null, new SymbolLocation("File.cs", 1, 18));

    private sealed class TestDataProvider : IFindImplementationsQueryDataProvider
    {
        public int Calls { get; private set; }
        public FindImplementationsResult NextResult { get; init; } = new(null, Array.Empty<SymbolDescriptor>(), null, null);
        public Exception? NextException { get; init; }

        public ValueTask<FindImplementationsResult> FindImplementationsAsync(
            FindImplementationsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
