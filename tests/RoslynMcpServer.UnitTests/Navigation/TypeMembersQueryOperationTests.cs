using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class TypeMembersQueryOperationTests
{
    [Theory]
    [InlineData("", "", 0, 0, "A non-empty solutionPath is required.")]
    [InlineData(" ", "source.cs", 1, 1, "A non-empty solutionPath is required.")]
    [InlineData("sample.sln", "", 0, 0, "A non-empty filePath is required.")]
    [InlineData("sample.sln", " ", 1, 1, "A non-empty filePath is required.")]
    [InlineData("sample.sln", "source.cs", -1, 1, "Line and column must be positive 1-based values.")]
    [InlineData("sample.sln", "source.cs", 1, 0, "Line and column must be positive 1-based values.")]
    public async Task ExecuteAsync_ValidatesInGuardOrder_BeforeProvider(
        string solutionPath, string filePath, int line, int column, string expectedFailure)
    {
        var provider = new TestTypeMembersQueryDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(
            new GetTypeMembersRequest(solutionPath, filePath, line, column));
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Equal(expectedFailure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesResolvedSymbol_InErrorBranch()
    {
        var symbol = new SymbolDescriptor("WriteLine", "Method", "System.Console.WriteLine()", "System.Console", null);
        var provider = new TestTypeMembersQueryDataProvider
        {
            NextResult = new GetTypeMembersResult(symbol, Array.Empty<TypeMemberDescriptor>(), "No source definition.", "Inspect metadata.")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(canceled);
        Assert.NotNull(error);
        Assert.Same(symbol, error.Value.Value.Symbol);
        Assert.Equal("No source definition.", error.Value.Value.FailureReason);
        Assert.Equal("Inspect metadata.", error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new GetTypeMembersResult(
            Type: null,
            Members: Array.Empty<TypeMemberDescriptor>(),
            FailureReason: null,
            Guidance: null);
        var provider = new TestTypeMembersQueryDataProvider
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
        var provider = new TestTypeMembersQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(
            CreateValidRequest() with { Line = 0 });
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("Line and column must be positive 1-based values.", error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestTypeMembersQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not retrieve type members.", error.Value.Value.FailureReason);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenCancellationRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var provider = new TestTypeMembersQueryDataProvider();
        var operation = CreateOperation(provider);

        var result = await operation.ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(0, provider.Calls);
    }

    private static TypeMembersQueryOperation CreateOperation(TestTypeMembersQueryDataProvider provider) =>
        new(provider, NullLogger<TypeMembersQueryOperation>.Instance);

    private static GetTypeMembersRequest CreateValidRequest() =>
        new(
            SolutionPath: @"C:\repo\Sample.sln",
            FilePath: @"C:\repo\src\File.cs",
            Line: 1,
            Column: 1);

    private sealed class TestTypeMembersQueryDataProvider : ITypeMembersQueryDataProvider
    {
        public int Calls { get; private set; }

        public GetTypeMembersResult NextResult { get; init; } = new(
            Type: null,
            Members: Array.Empty<TypeMemberDescriptor>(),
            FailureReason: null,
            Guidance: null);

        public Exception? NextException { get; init; }

        public ValueTask<GetTypeMembersResult> GetTypeMembersAsync(
            GetTypeMembersRequest request,
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
