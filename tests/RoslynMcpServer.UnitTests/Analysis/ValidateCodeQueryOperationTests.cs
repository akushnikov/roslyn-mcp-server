using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class ValidateCodeQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_PassesCompilerErrorsThroughAsSuccessfulData()
    {
        var expected = new ValidateCodeResult(false, 1, 2,
            [new("CS1002", "Error", "Expected semicolon.", null)], null, "Resolve compiler errors.");
        var provider = new TestProvider { NextResult = expected };
        var request = new ValidateCodeRequest("Sample.sln", null);

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(request);

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Same(request, provider.LastRequest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_RejectsEmptySolution_WithoutCallingProvider(string? solution)
    {
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new(solution!, null));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("A non-empty solutionPath is required.", error.Value.Value.FailureReason);
        Assert.Equal("Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file.", error.Value.Value.Guidance);
        Assert.Null(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task ExecuteAsync_AcceptsOptionalBlankFile(string? file)
    {
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new("Sample.sln", file));

        Assert.NotNull(success);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(file, provider.LastRequest!.FilePath);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsDependencyFailureAndGuidance()
    {
        var provider = new TestProvider { NextResult = new(false, 3, 4,
            [new("Partial", "Error", "Partial data.", null)], "Lookup failed.", "Reload solution.") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new("Sample.sln", null));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("Lookup failed.", error.Value.Value.FailureReason);
        Assert.Equal("Reload solution.", error.Value.Value.Guidance);
        Assert.Null(canceled);
    }

    [Fact]
    public async Task ExecuteAsync_SanitizesUnexpectedFault()
    {
        var provider = new TestProvider { NextException = new InvalidOperationException("sensitive dependency path") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new("Sample.sln", null));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("The server could not compute diagnostics for the requested solution.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request and confirm the solution is loaded.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason);
        Assert.Null(canceled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ClassifiesPreAndInFlightCancellation(bool inFlight)
    {
        using var source = new CancellationTokenSource();
        if (!inFlight)
        {
            await source.CancelAsync();
        }
        var provider = new TestProvider { CancelOnCall = inFlight ? source : null };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new("Sample.sln", null), source.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(inFlight ? 1 : 0, provider.Calls);
        if (inFlight)
        {
            Assert.Equal(source.Token, provider.LastToken);
        }
    }

    private static ValidateCodeQueryOperation CreateOperation(TestProvider provider) =>
        new(provider, NullLogger<ValidateCodeQueryOperation>.Instance);

    private sealed class TestProvider : IValidateCodeQueryDataProvider
    {
        public int Calls { get; private set; }
        public ValidateCodeRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public ValidateCodeResult NextResult { get; init; } = new(true, 0, 0, [], null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<ValidateCodeResult> ValidateCodeAsync(ValidateCodeRequest request, CancellationToken cancellationToken = default)
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
