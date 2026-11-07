using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class FindReferencesQueryServiceTests
{
    [Fact]
    public async Task FindReferencesAsync_PreservesResolvedSymbol_WhenProviderReturnsFailure()
    {
        var symbol = new SymbolDescriptor("Sample", "Class", "Sample", null, new SymbolLocation("File.cs", 1, 14));
        var expected = new FindReferencesResult(
            symbol,
            Array.Empty<SymbolReferenceDescriptor>(),
            "The server could not compute references for the resolved symbol.",
            "Retry the request and confirm the file position resolves to a source symbol.");
        var service = new FindReferencesQueryService(
            new FindReferencesQueryOperation(
                new TestFindReferencesQueryDataProvider { NextResult = expected },
                NullLogger<FindReferencesQueryOperation>.Instance));

        var result = await service.FindReferencesAsync(CreateValidRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.Empty(result.References);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task FindReferencesAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = new FindReferencesQueryService(
            new FindReferencesQueryOperation(
                new TestFindReferencesQueryDataProvider(),
                NullLogger<FindReferencesQueryOperation>.Instance));

        var result = await service.FindReferencesAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.References);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    [Fact]
    public async Task FindReferencesAsync_MapsProviderFailure_ToFailurePayload()
    {
        var service = new FindReferencesQueryService(
            new FindReferencesQueryOperation(
                new TestFindReferencesQueryDataProvider
                {
                    NextResult = new FindReferencesResult(
                        Symbol: null,
                        References: Array.Empty<SymbolReferenceDescriptor>(),
                        FailureReason: "Provider failed.",
                        Guidance: "Retry reference lookup.")
                },
                NullLogger<FindReferencesQueryOperation>.Instance));

        var result = await service.FindReferencesAsync(CreateValidRequest());

        Assert.Null(result.Symbol);
        Assert.Empty(result.References);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry reference lookup.", result.Guidance);
    }

    private static FindReferencesRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1,
            MaxResults: 100);

    private sealed class TestFindReferencesQueryDataProvider : IFindReferencesQueryDataProvider
    {
        public FindReferencesResult NextResult { get; init; } = new(
            Symbol: null,
            References: Array.Empty<SymbolReferenceDescriptor>(),
            FailureReason: null,
            Guidance: null);

        public ValueTask<FindReferencesResult> FindReferencesAsync(
            FindReferencesRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
