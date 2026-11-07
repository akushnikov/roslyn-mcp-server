using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class GoToDefinitionQueryServiceTests
{
    [Fact]
    public async Task GoToDefinitionAsync_PreservesResolvedSymbol_WhenNoSourceDefinitionExists()
    {
        var symbol = new SymbolDescriptor("WriteLine", "Method", "System.Console.WriteLine()", "System.Console", null);
        var expected = new GoToDefinitionResult(
            symbol,
            Array.Empty<SymbolLocation>(),
            "No source definition was found for the resolved symbol.",
            "The symbol may come from metadata or generated code outside the loaded solution.");
        var service = new GoToDefinitionQueryService(
            new GoToDefinitionQueryOperation(
                new TestGoToDefinitionQueryDataProvider { NextResult = expected },
                NullLogger<GoToDefinitionQueryOperation>.Instance));

        var result = await service.GoToDefinitionAsync(CreateValidRequest());

        Assert.Same(symbol, result.Symbol);
        Assert.Empty(result.Definitions);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GoToDefinitionAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = new GoToDefinitionQueryService(
            new GoToDefinitionQueryOperation(
                new TestGoToDefinitionQueryDataProvider(),
                NullLogger<GoToDefinitionQueryOperation>.Instance));

        var result = await service.GoToDefinitionAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.Symbol);
        Assert.Empty(result.Definitions);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    [Fact]
    public async Task GoToDefinitionAsync_MapsProviderFailure_ToFailurePayload()
    {
        var service = new GoToDefinitionQueryService(
            new GoToDefinitionQueryOperation(
                new TestGoToDefinitionQueryDataProvider
                {
                    NextResult = new GoToDefinitionResult(
                        Symbol: null,
                        Definitions: Array.Empty<SymbolLocation>(),
                        FailureReason: "Provider failed.",
                        Guidance: "Retry definition lookup.")
                },
                NullLogger<GoToDefinitionQueryOperation>.Instance));

        var result = await service.GoToDefinitionAsync(CreateValidRequest());

        Assert.Null(result.Symbol);
        Assert.Empty(result.Definitions);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Retry definition lookup.", result.Guidance);
    }

    private static GoToDefinitionRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1);

    private sealed class TestGoToDefinitionQueryDataProvider : IGoToDefinitionQueryDataProvider
    {
        public GoToDefinitionResult NextResult { get; init; } = new(
            Symbol: null,
            Definitions: Array.Empty<SymbolLocation>(),
            FailureReason: null,
            Guidance: null);

        public ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
            GoToDefinitionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
