using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class TypeCompatibilityQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderPayloadAndForwardsRequest()
    {
        var provider = new TestProvider();
        var request = Request();
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(request);

        Assert.NotNull(success);
        Assert.Same(provider.Result, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Same(request, provider.LastRequest);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("", "", 0, 0, "", 0, 0, "A non-empty solutionPath is required.")]
    [InlineData(" ", "Source.cs", 1, 1, "Target.cs", 1, 1, "A non-empty solutionPath is required.")]
    [InlineData("Sample.sln", "", 0, 0, "", 0, 0, "A non-empty sourceFilePath is required.")]
    [InlineData("Sample.sln", " ", 1, 1, "Target.cs", 1, 1, "A non-empty sourceFilePath is required.")]
    [InlineData("Sample.sln", "Source.cs", 0, 1, "", 0, 0, "Source line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", 1, -1, "Target.cs", 1, 1, "Source line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", -1, 1, "Target.cs", 1, 1, "Source line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", 1, 0, "Target.cs", 1, 1, "Source line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", 1, 1, "", 0, 0, "A non-empty targetFilePath is required.")]
    [InlineData("Sample.sln", "Source.cs", 1, 1, " ", 1, 1, "A non-empty targetFilePath is required.")]
    [InlineData("Sample.sln", "Source.cs", 1, 1, "Target.cs", 0, 1, "Target line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", 1, 1, "Target.cs", 1, 0, "Target line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", 1, 1, "Target.cs", -1, 1, "Target line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "Source.cs", 1, 1, "Target.cs", 1, -1, "Target line and column must be positive 1-based values.")]
    public async Task ExecuteAsync_RejectsFirstInvalidInputBeforeProvider(string solution, string sourceFile,
        int sourceLine, int sourceColumn, string targetFile, int targetLine, int targetColumn, string failure)
    {
        var provider = new TestProvider();
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(
            new(solution, sourceFile, sourceLine, sourceColumn, targetFile, targetLine, targetColumn));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal(failure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.Source);
        Assert.Null(error.Value.Value.Target);
        Assert.Null(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesBothDescriptorsOnProviderFailure()
    {
        var source = Symbol("Source");
        var target = Symbol("Target");
        var provider = new TestProvider { Result = new(source, target, null, "Lookup failed.", "Retry lookup.") };
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(Request());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(source, error.Value.Value.Source);
        Assert.Same(target, error.Value.Value.Target);
        Assert.Equal("Lookup failed.", error.Value.Value.FailureReason);
        Assert.Equal("Retry lookup.", error.Value.Value.Guidance);
        Assert.Null(canceled);
    }

    [Fact]
    public async Task ExecuteAsync_SanitizesUnhandledFault()
    {
        var provider = new TestProvider { Exception = new InvalidOperationException("private backend details") };
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(Request());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("The server could not check type compatibility for the requested symbols.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("private", error.Value.Value.FailureReason);
        Assert.Null(canceled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_CancellationIsDistinctAndForwardsToken(bool inFlight)
    {
        using var cancellation = new CancellationTokenSource();
        if (!inFlight) await cancellation.CancelAsync();
        var provider = new TestProvider { CancelOnCall = inFlight ? cancellation : null };
        var (success, error, canceled) = await Operation(provider).ExecuteAsync(Request(), cancellation.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(inFlight ? 1 : 0, provider.Calls);
        if (inFlight) Assert.Equal(cancellation.Token, provider.LastToken);
    }

    private static TypeCompatibilityQueryOperation Operation(TestProvider provider) =>
        new(provider, NullLogger<TypeCompatibilityQueryOperation>.Instance);
    private static CheckTypeCompatibilityRequest Request() => new("Sample.sln", "Source.cs", 1, 1, "Target.cs", 1, 1);
    private static SymbolDescriptor Symbol(string name) => new(name, "Class", name, "Sample", new("File.cs", 1, 1));

    private sealed class TestProvider : ITypeCompatibilityQueryDataProvider
    {
        public int Calls { get; private set; }
        public CheckTypeCompatibilityRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public CheckTypeCompatibilityResult Result { get; init; } = new(Symbol("Source"), Symbol("Target"),
            new("int", "long", false, true, true, false, false, false, true, false), null, null);
        public Exception? Exception { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibilityAsync(CheckTypeCompatibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastToken = cancellationToken;
            if (CancelOnCall is not null) await CancelOnCall.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return Exception is not null ? throw Exception : Result;
        }
    }
}
