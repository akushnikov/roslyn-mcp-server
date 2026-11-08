using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class DiagnosticsQueryServiceTests
{
    [Fact]
    public async Task GetDiagnosticsAsync_ReturnsSuccessfulProviderPayloadUnchanged()
    {
        var expected = new GetDiagnosticsResult(
            [new AnalysisDiagnosticDescriptor("CS1002", "Error", "; expected", null)], null, null);
        var provider = new TestDiagnosticsQueryDataProvider { NextResult = expected };

        var result = await CreateService(provider).GetDiagnosticsAsync(CreateValidRequest());

        Assert.Same(expected, result);
        Assert.Single(result.Diagnostics);
        Assert.Null(result.FailureReason);
        Assert.Null(result.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_PreservesProviderFailureAndGuidance_WithEmptyDiagnostics()
    {
        var provider = new TestDiagnosticsQueryDataProvider
        {
            NextResult = new GetDiagnosticsResult(
                [new AnalysisDiagnosticDescriptor("CS1002", "Error", "; expected", null)],
                "Provider failed.", "Load the requested solution.")
        };

        var result = await CreateService(provider).GetDiagnosticsAsync(CreateValidRequest());

        Assert.Empty(result.Diagnostics);
        Assert.Equal("Provider failed.", result.FailureReason);
        Assert.Equal("Load the requested solution.", result.Guidance);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_MapsCanceledOutcome_ToStablePublicPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestDiagnosticsQueryDataProvider();

        var result = await CreateService(provider).GetDiagnosticsAsync(CreateValidRequest(), cts.Token);

        Assert.Empty(result.Diagnostics);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    private static GetDiagnosticsRequest CreateValidRequest() => new(@"C:\repo\Sample.sln", null, null);

    private static DiagnosticsQueryService CreateService(TestDiagnosticsQueryDataProvider provider) =>
        new(new DiagnosticsQueryOperation(provider, NullLogger<DiagnosticsQueryOperation>.Instance));

    private sealed class TestDiagnosticsQueryDataProvider : IDiagnosticsQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetDiagnosticsResult NextResult { get; init; } = new([], null, null);

        public ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(
            GetDiagnosticsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
