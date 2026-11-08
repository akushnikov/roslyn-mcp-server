using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class SymbolAttributesQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new GetSymbolAttributesResult(
            new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null),
            new[] { new SymbolAttributeDescriptor("Marker", "Sample.Marker", new[] { "1" }, new[] { "Name = demo" }) },
            null, null);
        var provider = new TestProvider { NextResult = expected };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest());

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("", "", 0, 0, "A non-empty solutionPath is required.")]
    [InlineData(" ", "source.cs", 1, 1, "A non-empty solutionPath is required.")]
    [InlineData("sample.sln", "", 0, 0, "A non-empty filePath is required.")]
    [InlineData("sample.sln", " ", 1, 1, "A non-empty filePath is required.")]
    [InlineData("sample.sln", "source.cs", 0, 1, "Line and column must be positive 1-based values.")]
    [InlineData("sample.sln", "source.cs", -1, 1, "Line and column must be positive 1-based values.")]
    [InlineData("sample.sln", "source.cs", 1, 0, "Line and column must be positive 1-based values.")]
    [InlineData("sample.sln", "source.cs", 1, -1, "Line and column must be positive 1-based values.")]
    public async Task ExecuteAsync_ValidatesInGuardOrder_BeforeProvider(
        string solutionPath, string filePath, int line, int column, string expectedFailure)
    {
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(
            new GetSymbolAttributesRequest(solutionPath, filePath, line, column));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal(expectedFailure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PromotesProviderFailure_PreservingResolvedSymbol()
    {
        var symbol = new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null);
        var provider = new TestProvider
        {
            NextResult = new GetSymbolAttributesResult(symbol, Array.Empty<SymbolAttributeDescriptor>(), "Provider failed.", "Retry lookup.")
        };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal("Provider failed.", error.Value.Value.FailureReason);
        Assert.Equal("Retry lookup.", error.Value.Value.Guidance);
        Assert.Null(canceled);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledFault_ToSafeFailure()
    {
        var provider = new TestProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Equal("The server could not retrieve symbol attributes.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request and ensure the solution is loaded.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WithoutCallingProvider()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenProviderPropagatesRequestedCancellation()
    {
        using var cts = new CancellationTokenSource();
        var provider = new TestProvider { CancellationSource = cts };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
    }

    private static SymbolAttributesQueryOperation CreateOperation(TestProvider provider) =>
        new(provider, NullLogger<SymbolAttributesQueryOperation>.Instance);

    private static GetSymbolAttributesRequest ValidRequest() => new("sample.sln", "source.cs", 1, 1);

    private sealed class TestProvider : ISymbolAttributesQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetSymbolAttributesResult NextResult { get; init; } = new(null, Array.Empty<SymbolAttributeDescriptor>(), null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancellationSource { get; init; }

        public ValueTask<GetSymbolAttributesResult> GetSymbolAttributesAsync(
            GetSymbolAttributesRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CancellationSource?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
