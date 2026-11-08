using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class ChangeImpactQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsCompleteProviderPayload_OnSuccess()
    {
        var expected = CreateSuccess();
        var provider = new TestProvider { NextResult = expected };
        var request = CreateRequest();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(request);

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Same(request, provider.LastRequest);
    }

    [Theory]
    [InlineData("", "File.cs", 1, 1, 100, "A non-empty solutionPath is required.")]
    [InlineData(" ", "", 0, 0, 0, "A non-empty solutionPath is required.")]
    [InlineData("Sample.sln", "", 0, 0, 0, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", " ", 1, 1, 100, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", "File.cs", 0, 1, 0, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", -1, 1, 100, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, 0, 100, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, -1, 100, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, 1, 0, "MaxResults must be a positive value.")]
    [InlineData("Sample.sln", "File.cs", 1, 1, -1, "MaxResults must be a positive value.")]
    public async Task ExecuteAsync_RejectsInvalidInputsInOrder_WithoutCallingProvider(
        string solution, string file, int line, int column, int maxResults, string failure)
    {
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(
            new AnalyzeChangeImpactRequest(solution, file, line, column, maxResults));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal(failure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.Symbol);
        Assert.Null(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSymbolAndGuidance_OnProviderFailure()
    {
        var symbol = CreateSymbol();
        var provider = new TestProvider { NextResult = new(symbol, null, "Lookup failed.", "Retry impact.") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal("Lookup failed.", error.Value.Value.FailureReason);
        Assert.Equal("Retry impact.", error.Value.Value.Guidance);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnexpectedFault_WithoutDisclosingDetails()
    {
        var provider = new TestProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("The server could not compute a semantic impact summary for the resolved symbol.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request on a source symbol inside the loaded solution.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsProvider_WhenTokenIsAlreadyCanceled()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateRequest(), source.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ClassifiesInFlightCancellation_AsCanceled()
    {
        using var source = new CancellationTokenSource();
        var provider = new TestProvider { CancelOnCall = source };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateRequest(), source.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(source.Token, provider.LastToken);
    }

    private static ChangeImpactQueryOperation CreateOperation(TestProvider provider) =>
        new(provider, NullLogger<ChangeImpactQueryOperation>.Instance);

    private static AnalyzeChangeImpactRequest CreateRequest() => new("Sample.sln", "File.cs", 1, 1);

    private static SymbolDescriptor CreateSymbol() => new("Run", "Method", "Sample.Run()", "Sample", new("File.cs", 1, 1));

    private static AnalyzeChangeImpactResult CreateSuccess() => new(CreateSymbol(),
        CreateImpact(), null, null);

    private static ChangeImpactDescriptor CreateImpact() => new(1, 1, 0,
        ["File.cs"],
        [new(new("File.cs", 2, 5), null, "Run();")],
        [new("Sample.Caller()", new("File.cs", 2, 5), "Run();")], []);

    private sealed class TestProvider : IChangeImpactQueryDataProvider
    {
        public int Calls { get; private set; }
        public AnalyzeChangeImpactRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public AnalyzeChangeImpactResult NextResult { get; init; } = CreateSuccess();
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpactAsync(AnalyzeChangeImpactRequest request, CancellationToken cancellationToken = default)
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
