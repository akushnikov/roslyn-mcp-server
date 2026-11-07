using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class FindReferencesQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_PreservesResolvedSymbol_InErrorBranch()
    {
        var symbol = new SymbolDescriptor("Sample", "Class", "Sample", null, new SymbolLocation("File.cs", 1, 14));
        var provider = new TestFindReferencesQueryDataProvider
        {
            NextResult = new FindReferencesResult(symbol, Array.Empty<SymbolReferenceDescriptor>(), "Reference lookup failed.", "Retry.")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal("Reference lookup failed.", error.Value.Value.FailureReason);
        Assert.Equal("Retry.", error.Value.Value.Guidance);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new FindReferencesResult(
            Symbol: null,
            References: Array.Empty<SymbolReferenceDescriptor>(),
            FailureReason: null,
            Guidance: null);
        var provider = new TestFindReferencesQueryDataProvider
        {
            NextResult = expected
        };
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_AndSkipsProvider()
    {
        var provider = new TestFindReferencesQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(
            CreateValidRequest() with { MaxResults = 0 });
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("MaxResults must be a positive value.", error.Value.Value.FailureReason);
        Assert.Equal("Pass a positive integer (e.g., the default of 100).", error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestFindReferencesQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not compute references for the resolved symbol.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenCancellationRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = new TestFindReferencesQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    private static FindReferencesQueryOperation CreateOperation(TestFindReferencesQueryDataProvider provider) =>
        new(provider, NullLogger<FindReferencesQueryOperation>.Instance);

    private static FindReferencesRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1,
            MaxResults: 100);

    private sealed class TestFindReferencesQueryDataProvider : IFindReferencesQueryDataProvider
    {
        public int Calls { get; private set; }

        public FindReferencesResult NextResult { get; init; } = new(
            Symbol: null,
            References: Array.Empty<SymbolReferenceDescriptor>(),
            FailureReason: null,
            Guidance: null);

        public Exception? NextException { get; init; }

        public ValueTask<FindReferencesResult> FindReferencesAsync(
            FindReferencesRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();

            return NextException is not null
                ? throw NextException
                : ValueTask.FromResult(NextResult);
        }
    }
}
