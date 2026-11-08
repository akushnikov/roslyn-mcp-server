using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class DiagnosticsQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderPayload_OnSuccess()
    {
        var expected = new GetDiagnosticsResult(
            [new AnalysisDiagnosticDescriptor("CS1002", "Error", "; expected", null)], null, null);
        var provider = new TestDiagnosticsQueryDataProvider { NextResult = expected };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_ReturnsValidationFailure_WithoutCallingProvider(string solutionPath)
    {
        var provider = new TestDiagnosticsQueryDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest() with { SolutionPath = solutionPath });
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("A non-empty solutionPath is required.", error.Value.Value.FailureReason);
        Assert.Equal("Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file.", error.Value.Value.Guidance);
        Assert.Null(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesProviderFailureAndGuidance()
    {
        var provider = new TestDiagnosticsQueryDataProvider
        {
            NextResult = new GetDiagnosticsResult([], "Solution unavailable.", "Load the solution.")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("Solution unavailable.", error.Value.Value.FailureReason);
        Assert.Equal("Load the solution.", error.Value.Value.Guidance);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledFault_ToSafeFailure()
    {
        var provider = new TestDiagnosticsQueryDataProvider
        {
            NextException = new InvalidOperationException("secret backend connection details")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("The server could not compute diagnostics for the requested solution.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request and confirm the solution is loaded.", error.Value.Value.Guidance);
        Assert.DoesNotContain("secret", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", error.Value.Value.Guidance!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreCanceledToken_SkipsProvider()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestDiagnosticsQueryDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesToken_AndClassifiesInFlightCancellation()
    {
        using var cts = new CancellationTokenSource();
        var provider = new TestDiagnosticsQueryDataProvider { CancelDuringCall = cts };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(cts.Token, provider.ReceivedToken);
    }

    private static GetDiagnosticsRequest CreateValidRequest() => new(@"C:\repo\Sample.sln", null, null);

    private static DiagnosticsQueryOperation CreateOperation(TestDiagnosticsQueryDataProvider provider) =>
        new(provider, NullLogger<DiagnosticsQueryOperation>.Instance);

    private sealed class TestDiagnosticsQueryDataProvider : IDiagnosticsQueryDataProvider
    {
        public int Calls { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public GetDiagnosticsResult NextResult { get; init; } = new([], null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelDuringCall { get; init; }

        public ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(
            GetDiagnosticsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            ReceivedToken = cancellationToken;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
