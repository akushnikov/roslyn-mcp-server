using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;

namespace RoslynMcpServer.UnitTests.Analysis;

public sealed class FindImplementationsQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderPayloadAndForwardsRequestAndToken_OnSuccess()
    {
        using var cts = new CancellationTokenSource();
        var expected = new FindImplementationsResult(null, Array.Empty<SymbolDescriptor>(), null, null);
        var provider = new TestDataProvider { NextResult = expected };
        var request = CreateValidRequest();

        var result = await CreateOperation(provider).ExecuteAsync(request, cts.Token);
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Same(request, provider.LastRequest);
        Assert.Equal(cts.Token, provider.LastToken);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        var valid = CreateValidRequest();
        yield return [valid with { SolutionPath = "" }, "A non-empty solutionPath is required."];
        yield return [valid with { SolutionPath = " " }, "A non-empty solutionPath is required."];
        yield return [valid with { FilePath = "" }, "A non-empty filePath is required."];
        yield return [valid with { FilePath = " " }, "A non-empty filePath is required."];
        yield return [valid with { Line = 0 }, "Line and column must be positive 1-based values."];
        yield return [valid with { Line = -1 }, "Line and column must be positive 1-based values."];
        yield return [valid with { Column = 0 }, "Line and column must be positive 1-based values."];
        yield return [valid with { Column = -1 }, "Line and column must be positive 1-based values."];
        yield return [valid with { MaxResults = 0 }, "MaxResults must be a positive value."];
        yield return [valid with { MaxResults = -1 }, "MaxResults must be a positive value."];
        yield return [valid with { SolutionPath = "", FilePath = "", Line = 0, Column = 0, MaxResults = 0 }, "A non-empty solutionPath is required."];
        yield return [valid with { FilePath = "", Line = 0, Column = 0, MaxResults = 0 }, "A non-empty filePath is required."];
        yield return [valid with { Line = 0, MaxResults = 0 }, "Line and column must be positive 1-based values."];
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task ExecuteAsync_ReturnsFirstValidationFailure_WithoutCallingProvider(
        FindImplementationsRequest request, string expectedFailure)
    {
        var provider = new TestDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(request);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal(expectedFailure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.Symbol);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesResolvedSymbolAndGuidance_WhenProviderFails()
    {
        var symbol = new SymbolDescriptor("IWorker", "Interface", "IWorker", null, new SymbolLocation("File.cs", 1, 18));
        var provider = new TestDataProvider
        {
            NextResult = new FindImplementationsResult(symbol, Array.Empty<SymbolDescriptor>(), "Lookup failed.", "Retry.")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal("Lookup failed.", error.Value.Value.FailureReason);
        Assert.Equal("Retry.", error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnexpectedFault_ToSafeFailure()
    {
        var provider = new TestDataProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not compute implementations for the resolved symbol.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request on an interface, abstract member, or base type.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(error.Value.Value.Symbol);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WithoutCallingProvider_WhenPrecanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new TestDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenStartedProviderIsCanceled()
    {
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new TestDataProvider
        {
            Execute = async token =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("The canceled wait must not complete normally.");
            }
        };
        var execution = CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await cts.CancelAsync();
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(10));
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(cts.Token, provider.LastToken);
    }

    private static FindImplementationsQueryOperation CreateOperation(TestDataProvider provider) =>
        new(provider, NullLogger<FindImplementationsQueryOperation>.Instance);

    private static FindImplementationsRequest CreateValidRequest() => new("Sample.sln", "File.cs", 1, 1);

    private sealed class TestDataProvider : IFindImplementationsQueryDataProvider
    {
        public int Calls { get; private set; }
        public FindImplementationsRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public FindImplementationsResult NextResult { get; init; } = new(null, Array.Empty<SymbolDescriptor>(), null, null);
        public Exception? NextException { get; init; }
        public Func<CancellationToken, ValueTask<FindImplementationsResult>>? Execute { get; init; }

        public ValueTask<FindImplementationsResult> FindImplementationsAsync(
            FindImplementationsRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (Execute is not null)
            {
                return Execute(cancellationToken);
            }

            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
