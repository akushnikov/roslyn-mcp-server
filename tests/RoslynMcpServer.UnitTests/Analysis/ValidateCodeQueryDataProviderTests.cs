using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Infrastructure.Roslyn.Analysis;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class ValidateCodeQueryDataProviderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("File.cs")]
    public async Task ValidateCodeAsync_ForwardsOptionalFileAndTokenExactlyOnce_PreservingDiagnosticOrder(string? file)
    {
        AnalysisDiagnosticDescriptor[] diagnostics =
        [
            new("W1", "warning", "First warning.", null),
            new("E1", "eRrOr", "Compiler error.", null),
            new("I1", "Info", "Information.", null),
            new("W2", "WARNING", "Second warning.", null)
        ];
        var dependency = new DiagnosticsProvider { NextResult = new(diagnostics, null, null) };
        using var source = new CancellationTokenSource();

        var result = await new RoslynValidateCodeQueryDataProvider(dependency)
            .ValidateCodeAsync(new("Sample.sln", file), source.Token);

        Assert.Equal(new GetDiagnosticsRequest("Sample.sln", file, "All"), dependency.LastRequest);
        Assert.Equal(source.Token, dependency.LastToken);
        Assert.Equal(1, dependency.Calls);
        Assert.Same(diagnostics, result.Diagnostics);
        Assert.Equal(new[] { "W1", "E1", "I1", "W2" }, result.Diagnostics.Select(static diagnostic => diagnostic.Id));
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(2, result.WarningCount);
        Assert.False(result.IsValid);
        Assert.Null(result.FailureReason);
        Assert.Equal("Resolve compiler errors before applying additional semantic edits.", result.Guidance);
    }

    [Fact]
    public async Task ValidateCodeAsync_WithoutCompilerErrors_IsValidWithoutGuidance()
    {
        var dependency = new DiagnosticsProvider { NextResult = new([new("W1", "Warning", "Warning.", null)], null, null) };

        var result = await new RoslynValidateCodeQueryDataProvider(dependency).ValidateCodeAsync(new("Sample.sln", null));

        Assert.True(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
        Assert.Null(result.FailureReason);
        Assert.Null(result.Guidance);
    }

    [Fact]
    public async Task ValidateCodeAsync_ProjectsDependencyFailureAndDiscardsPartialDiagnostics()
    {
        var dependency = new DiagnosticsProvider { NextResult = new(
            [new("Partial", "Error", "Partial data.", null)], "Diagnostics unavailable.", "Reload solution.") };

        var result = await new RoslynValidateCodeQueryDataProvider(dependency).ValidateCodeAsync(new("Sample.sln", null));

        Assert.False(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("Diagnostics unavailable.", result.FailureReason);
        Assert.Equal("Reload solution.", result.Guidance);
        Assert.Equal(1, dependency.Calls);
    }

    [Fact]
    public async Task ValidateCodeAsync_DependencyFaultIsSanitizedByPipeline()
    {
        var dependency = new DiagnosticsProvider { NextException = new InvalidOperationException("sensitive backend detail") };
        var service = new ValidateCodeQueryService(new(new RoslynValidateCodeQueryDataProvider(dependency),
            NullLogger<ValidateCodeQueryOperation>.Instance));

        var result = await service.ValidateCodeAsync(new("Sample.sln", null));

        Assert.False(result.IsValid);
        Assert.Equal("The server could not compute diagnostics for the requested solution.", result.FailureReason);
        Assert.Equal("Retry the request and confirm the solution is loaded.", result.Guidance);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Equal(1, dependency.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateCodeAsync_PropagatesRequestedCancellation(bool inFlight)
    {
        using var source = new CancellationTokenSource();
        if (!inFlight)
        {
            await source.CancelAsync();
        }
        var dependency = new DiagnosticsProvider { CancelOnCall = inFlight ? source : null };
        var provider = new RoslynValidateCodeQueryDataProvider(dependency);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ValidateCodeAsync(new("Sample.sln", null), source.Token).AsTask());

        Assert.Equal(inFlight ? 1 : 0, dependency.Calls);
        if (inFlight)
        {
            Assert.Equal(source.Token, dependency.LastToken);
        }
    }

    private sealed class DiagnosticsProvider : IDiagnosticsQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetDiagnosticsRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public GetDiagnosticsResult NextResult { get; init; } = new([], null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(GetDiagnosticsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastToken = cancellationToken;
            if (CancelOnCall is not null)
            {
                await CancelOnCall.CancelAsync();
            }
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : NextResult;
        }
    }
}
