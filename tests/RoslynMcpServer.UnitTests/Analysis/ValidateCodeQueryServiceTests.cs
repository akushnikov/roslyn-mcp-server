using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class ValidateCodeQueryServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateCodeAsync_PreservesSuccessfulIdentityAndDiagnosticCounts(bool isValid)
    {
        var expected = new ValidateCodeResult(isValid, isValid ? 0 : 2, 3,
            [new("CS1002", "Error", "Compiler diagnostic.", null)], null, isValid ? null : "Resolve compiler errors.");
        var provider = new TestProvider { NextResult = expected };

        var result = await CreateService(provider).ValidateCodeAsync(new("Sample.sln", null));

        Assert.Same(expected, result);
        Assert.Equal(expected.ErrorCount, result.ErrorCount);
        Assert.Equal(3, result.WarningCount);
        Assert.Null(result.FailureReason);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ValidateCodeAsync_MapsFailureAndClearsPartialCountsAndDiagnostics()
    {
        var provider = new TestProvider { NextResult = new(true, 2, 3,
            [new("Partial", "Error", "Partial data.", null)], "Lookup failed.", "Reload solution.") };

        var result = await CreateService(provider).ValidateCodeAsync(new("Sample.sln", null));

        Assert.False(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("Lookup failed.", result.FailureReason);
        Assert.Equal("Reload solution.", result.Guidance);
    }

    [Fact]
    public async Task ValidateCodeAsync_ProjectsValidationWithoutProviderCall()
    {
        var provider = new TestProvider();

        var result = await CreateService(provider).ValidateCodeAsync(new(" ", null));

        Assert.False(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("A non-empty solutionPath is required.", result.FailureReason);
        Assert.NotNull(result.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateCodeAsync_ProjectsStablePreAndInFlightCancellation(bool inFlight)
    {
        using var source = new CancellationTokenSource();
        if (!inFlight)
        {
            await source.CancelAsync();
        }
        var provider = new TestProvider { CancelOnCall = inFlight ? source : null };

        var result = await CreateService(provider).ValidateCodeAsync(new("Sample.sln", null), source.Token);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
        Assert.Equal(inFlight ? 1 : 0, provider.Calls);
    }

    private static ValidateCodeQueryService CreateService(TestProvider provider) =>
        new(new ValidateCodeQueryOperation(provider, NullLogger<ValidateCodeQueryOperation>.Instance));

    private sealed class TestProvider : IValidateCodeQueryDataProvider
    {
        public int Calls { get; private set; }
        public ValidateCodeResult NextResult { get; init; } = new(true, 0, 0, [], null, null);
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<ValidateCodeResult> ValidateCodeAsync(ValidateCodeRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (CancelOnCall is not null)
            {
                await CancelOnCall.CancelAsync();
            }
            cancellationToken.ThrowIfCancellationRequested();
            return NextResult;
        }
    }
}
