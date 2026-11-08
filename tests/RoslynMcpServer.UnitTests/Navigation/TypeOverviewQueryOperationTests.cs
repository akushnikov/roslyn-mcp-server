using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class TypeOverviewQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var type = new SymbolDescriptor("Example", "Class", "Sample.Example", "Sample", null);
        var baseType = new SymbolDescriptor("Base", "Class", "Sample.Base", "Sample", null);
        var directInterface = new SymbolDescriptor("IExample", "Interface", "Sample.IExample", "Sample", null);
        var member = new TypeMemberDescriptor("Run", "Method", "Sample.Example.Run()", "Public", false, null);
        var expected = new GetTypeOverviewResult(type, baseType, new[] { directInterface }, new[] { member }, null, null);
        var provider = new TestTypeOverviewQueryDataProvider { NextResult = expected };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());

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
        var provider = new TestTypeOverviewQueryDataProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(
            new GetTypeOverviewRequest(solutionPath, filePath, line, column));

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Equal(expectedFailure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesResolvedType_InProviderError()
    {
        var symbol = new SymbolDescriptor("Sample", "Namespace", "Sample", null, null);
        var provider = new TestTypeOverviewQueryDataProvider
        {
            NextResult = new GetTypeOverviewResult(symbol, null, Array.Empty<SymbolDescriptor>(),
                Array.Empty<TypeMemberDescriptor>(), "The resolved symbol does not belong to a named type.",
                "Use a type declaration or a member inside a named type.")
        };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal(provider.NextResult.FailureReason, error.Value.Value.FailureReason);
        Assert.Equal(provider.NextResult.Guidance, error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestTypeOverviewQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Equal("The server could not retrieve the type overview.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WithoutCallingProvider_WhenTokenIsPreCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestTypeOverviewQueryDataProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenProviderObservesRequestedCancellation()
    {
        using var cts = new CancellationTokenSource();
        var provider = new TestTypeOverviewQueryDataProvider { BeforeResult = cts.Cancel };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
    }

    private static TypeOverviewQueryOperation CreateOperation(TestTypeOverviewQueryDataProvider provider) =>
        new(provider, NullLogger<TypeOverviewQueryOperation>.Instance);

    private static GetTypeOverviewRequest CreateValidRequest() =>
        new(@"C:\repo\Sample.sln", @"C:\repo\src\File.cs", 1, 1);

    private sealed class TestTypeOverviewQueryDataProvider : ITypeOverviewQueryDataProvider
    {
        public int Calls { get; private set; }
        public Action? BeforeResult { get; init; }
        public Exception? NextException { get; init; }
        public GetTypeOverviewResult NextResult { get; init; } = new(null, null,
            Array.Empty<SymbolDescriptor>(), Array.Empty<TypeMemberDescriptor>(), null, null);

        public ValueTask<GetTypeOverviewResult> GetTypeOverviewAsync(
            GetTypeOverviewRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            BeforeResult?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
