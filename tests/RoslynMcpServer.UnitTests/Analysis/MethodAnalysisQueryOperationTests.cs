using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class MethodAnalysisQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_PassesSuccessfulPayloadAndRequestThrough()
    {
        var expected = Success();
        var provider = new TestProvider { Result = expected };
        var request = Request();
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(request);
        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Same(request, provider.LastRequest);
    }

    [Theory]
    [InlineData("", "", 0, 0, 0, "A non-empty solutionPath is required.")]
    [InlineData(" ", "File.cs", 1, 1, 1, "A non-empty solutionPath is required.")]
    [InlineData("Sample.sln", "", 0, 0, 0, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", " ", 1, 1, 1, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", "File.cs", 0, 1, 0, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", -1, 1, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, 0, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, -1, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, 1, 0, "MaxOutgoingCalls must be a positive value.")]
    [InlineData("Sample.sln", "File.cs", 1, 1, -1, "MaxOutgoingCalls must be a positive value.")]
    public async Task ExecuteAsync_RejectsFirstInvalidInputBeforeProvider(string solution, string file, int line, int column, int limit, string reason)
    {
        var provider = new TestProvider();
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(new(solution, file, line, column, limit));
        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal(reason, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.Signature);
        Assert.Null(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSignatureAndGuidanceOnProviderFailure()
    {
        var signature = Signature();
        var provider = new TestProvider { Result = new(signature, null, null, [], "No block.", "Use block.") };
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(Request());
        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(signature, error.Value.Value.Signature);
        Assert.Equal("No block.", error.Value.Value.FailureReason);
        Assert.Equal("Use block.", error.Value.Value.Guidance);
        Assert.Null(canceled);
    }

    [Fact]
    public async Task ExecuteAsync_SanitizesUnexpectedFault()
    {
        var provider = new TestProvider { Exception = new InvalidOperationException("sensitive backend detail") };
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(Request());
        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("The server could not analyze the resolved callable member.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason);
        Assert.Null(canceled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_ClassifiesCancellationAndForwardsToken(bool inFlight)
    {
        using var source = new CancellationTokenSource();
        if (!inFlight) await source.CancelAsync();
        var provider = new TestProvider { CancelOnCall = inFlight ? source : null };
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(Request(), source.Token);
        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(inFlight ? 1 : 0, provider.Calls);
        if (inFlight) Assert.Equal(source.Token, provider.LastToken);
    }

    private static MethodAnalysisQueryOperation Operation(TestProvider provider) => new(provider, NullLogger<MethodAnalysisQueryOperation>.Instance);
    private static AnalyzeMethodRequest Request() => new("Sample.sln", "File.cs", 1, 1);
    private static MethodSignatureDescriptor Signature() => new("Run", "Method", "Sample.Run()", "Public", false, false, "int", "Sample", [], new("File.cs", 1, 1));
    private static AnalyzeMethodResult Success() => new(Signature(), new([], [], [], [], [], [], [], [], [], true), new(true, false, [], [], [], true), [new(null, new("File.cs", 2, 1), "Next()")], null, null);

    private sealed class TestProvider : IMethodAnalysisQueryDataProvider
    {
        public int Calls { get; private set; }
        public AnalyzeMethodRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public AnalyzeMethodResult Result { get; init; } = Success();
        public Exception? Exception { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(AnalyzeMethodRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastToken = cancellationToken;
            if (CancelOnCall is not null) await CancelOnCall.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return Exception is null ? Result : throw Exception;
        }
    }
}
