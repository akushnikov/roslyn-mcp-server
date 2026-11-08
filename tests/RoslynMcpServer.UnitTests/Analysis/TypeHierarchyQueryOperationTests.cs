using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class TypeHierarchyQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsCompleteProviderPayload_AndForwardsRequestAndToken()
    {
        using var source = new CancellationTokenSource();
        var expected = CreateSuccess();
        var provider = new TestProvider { NextResult = expected };
        var request = CreateRequest();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(request, source.Token);

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Same(request, provider.LastRequest);
        Assert.Equal(source.Token, provider.LastToken);
    }

    [Theory]
    [InlineData("", "File.cs", 1, 1, "A non-empty solutionPath is required.")]
    [InlineData(" ", "", 0, 0, "A non-empty solutionPath is required.")]
    [InlineData("Sample.sln", "", 0, 0, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", " ", 1, 1, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", "File.cs", 0, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", -1, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, 0, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, -1, "Line and column must be positive 1-based values.")]
    public async Task ExecuteAsync_RejectsInvalidInputsInOrder_WithoutCallingProvider(
        string solution, string file, int line, int column, string failure)
    {
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(
            new GetTypeHierarchyRequest(solution, file, line, column, null));

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
        var symbol = CreateSymbol("Target");
        var provider = new TestProvider { NextResult = new(symbol, [], [], [], "Lookup failed.", "Retry hierarchy.") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal("Lookup failed.", error.Value.Value.FailureReason);
        Assert.Equal("Retry hierarchy.", error.Value.Value.Guidance);
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
        Assert.Equal("The server could not compute the type hierarchy for the resolved symbol.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request on a type declaration or a member that belongs to a named type.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sensitive", error.Value.Value.Guidance!, StringComparison.OrdinalIgnoreCase);
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

    private static TypeHierarchyQueryOperation CreateOperation(TestProvider provider) =>
        new(provider, NullLogger<TypeHierarchyQueryOperation>.Instance);

    private static GetTypeHierarchyRequest CreateRequest() => new("Sample.sln", "File.cs", 1, 1, null);

    private static SymbolDescriptor CreateSymbol(string name) => new(name, "Class", $"Sample.{name}", "Sample", new("File.cs", 1, 1));

    private static GetTypeHierarchyResult CreateSuccess() => new(CreateSymbol("Target"),
        [CreateSymbol("Base")], [CreateSymbol("Derived")], [CreateSymbol("IContract")], null, null);

    private sealed class TestProvider : ITypeHierarchyQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetTypeHierarchyRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public GetTypeHierarchyResult NextResult { get; init; } = CreateSuccess();
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public async ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(GetTypeHierarchyRequest request, CancellationToken cancellationToken = default)
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
