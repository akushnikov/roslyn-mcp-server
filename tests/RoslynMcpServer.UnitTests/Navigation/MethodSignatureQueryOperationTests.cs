using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class MethodSignatureQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var signature = new MethodSignatureDescriptor(
            "Read", "Method", "Sample.Read(int)", "Public", false, false,
            "string", "Sample", Array.Empty<MethodParameterDescriptor>(), null);
        var expected = new GetMethodSignatureResult(signature, null, null);
        var provider = new TestMethodSignatureQueryDataProvider { NextResult = expected };
        var request = CreateValidRequest();

        var result = await CreateOperation(provider).ExecuteAsync(request);
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Same(signature, success.Value.Value.Signature);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
        Assert.Same(request, provider.LastRequest);
    }

    [Theory]
    [InlineData("", "", 0, 0, "A non-empty solutionPath is required.")]
    [InlineData(" ", "File.cs", 1, 1, "A non-empty solutionPath is required.")]
    [InlineData("Sample.sln", "", 0, 0, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", " ", 1, 1, "A non-empty filePath is required.")]
    [InlineData("Sample.sln", "File.cs", 0, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", -1, 1, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, 0, "Line and column must be positive 1-based values.")]
    [InlineData("Sample.sln", "File.cs", 1, -1, "Line and column must be positive 1-based values.")]
    public async Task ExecuteAsync_ReturnsFirstValidationFailure_AndSkipsProvider(
        string solutionPath, string filePath, int line, int column, string failureReason)
    {
        var provider = new TestMethodSignatureQueryDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(
            new GetMethodSignatureRequest(solutionPath, filePath, line, column));
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal(failureReason, error.Value.Value.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(error.Value.Value.Guidance));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PromotesProviderFailure_WithExistingGuidance()
    {
        var provider = new TestMethodSignatureQueryDataProvider
        {
            NextResult = new GetMethodSignatureResult(
                null,
                "The resolved symbol is not a callable member.",
                "Use a method, constructor, local function, accessor, property, or event declaration.")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal(provider.NextResult.FailureReason, error.Value.Value.FailureReason);
        Assert.Equal(provider.NextResult.Guidance, error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestMethodSignatureQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not retrieve the method signature.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request and ensure the solution is loaded.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_AndSkipsProvider_WhenTokenIsAlreadyCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestMethodSignatureQueryDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenProviderObservesRequestedCancellation()
    {
        using var cts = new CancellationTokenSource();
        var provider = new TestMethodSignatureQueryDataProvider { CancelDuringCall = cts };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(cts.Token, provider.LastToken);
        Assert.Equal(1, provider.Calls);
    }

    private static MethodSignatureQueryOperation CreateOperation(TestMethodSignatureQueryDataProvider provider) =>
        new(provider, NullLogger<MethodSignatureQueryOperation>.Instance);

    private static GetMethodSignatureRequest CreateValidRequest() =>
        new(@"C:\repo\Sample.sln", @"C:\repo\File.cs", 1, 1);

    private sealed class TestMethodSignatureQueryDataProvider : IMethodSignatureQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetMethodSignatureRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public GetMethodSignatureResult NextResult { get; init; } = new(null, null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelDuringCall { get; init; }

        public ValueTask<GetMethodSignatureResult> GetMethodSignatureAsync(
            GetMethodSignatureRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastToken = cancellationToken;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
