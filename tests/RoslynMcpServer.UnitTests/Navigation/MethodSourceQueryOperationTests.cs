using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class MethodSourceQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new GetMethodSourceResult(null, null, null, null);
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
    [InlineData("sample.sln", "source.cs", 1, -1, "Line and column must be positive 1-based values.")]
    public async Task ExecuteAsync_ValidatesInGuardOrder_BeforeProvider(
        string solutionPath, string filePath, int line, int column, string expectedFailure)
    {
        var provider = new TestProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(
            new GetMethodSourceRequest(solutionPath, filePath, line, column));

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Equal(expectedFailure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.Signature);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSignature_InStructuredProviderFailure()
    {
        var signature = Signature();
        var provider = new TestProvider
        {
            NextResult = new GetMethodSourceResult(signature, null, "Source unavailable.", "Inspect metadata.")
        };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest());

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Same(signature, error.Value.Value.Signature);
        Assert.Equal("Source unavailable.", error.Value.Value.FailureReason);
        Assert.Equal("Inspect metadata.", error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledFault_ToSafeFailure()
    {
        var provider = new TestProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest());

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Equal("The server could not retrieve method source.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Value.Value.Guidance);
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
        var provider = new TestProvider { CancelDuringCall = cts };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(ValidRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
    }

    private static MethodSourceQueryOperation CreateOperation(TestProvider provider) =>
        new(provider, NullLogger<MethodSourceQueryOperation>.Instance);

    private static GetMethodSourceRequest ValidRequest() => new("sample.sln", "source.cs", 1, 1);

    private static MethodSignatureDescriptor Signature() =>
        new("Run", "Method", "Example.Run()", "Public", false, false, "void", "Example",
            Array.Empty<MethodParameterDescriptor>(), null);

    private sealed class TestProvider : IMethodSourceQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetMethodSourceResult NextResult { get; init; } = new(null, null, null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelDuringCall { get; init; }

        public ValueTask<GetMethodSourceResult> GetMethodSourceAsync(
            GetMethodSourceRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
