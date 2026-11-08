using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class FileOverviewQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderResult_OnSuccess()
    {
        var expected = new GetFileOverviewResult("source.cs", new[] { "Sample" }, Array.Empty<FileTypeDescriptor>(), null, null);
        var provider = new TestFileOverviewQueryDataProvider { NextResult = expected };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("", "", "A non-empty solutionPath is required.")]
    [InlineData(" ", "source.cs", "A non-empty solutionPath is required.")]
    [InlineData("sample.sln", "", "A non-empty filePath is required.")]
    [InlineData("sample.sln", " ", "A non-empty filePath is required.")]
    public async Task ExecuteAsync_ValidatesInGuardOrder_BeforeProvider(
        string solutionPath, string filePath, string expectedFailure)
    {
        var provider = new TestFileOverviewQueryDataProvider();

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(new(solutionPath, filePath));

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal(expectedFailure, error.Value.Value.FailureReason);
        Assert.NotNull(error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.FilePath);
        Assert.Equal(0, provider.Calls);
    }

    [Theory]
    [InlineData(null, "The requested solution is not currently loaded.", "Call load_solution first for the requested solutionPath.")]
    [InlineData("C:\\repo\\missing.cs", "The requested file was not found in the loaded solution.", "Call get_project_structure with includeDocuments=true to inspect valid file paths.")]
    [InlineData("C:\\repo\\source.cs", "The requested document does not have a C# compilation unit syntax tree.", "Use a C# source file that belongs to the loaded solution.")]
    public async Task ExecuteAsync_PreservesFailureStageFilePath(
        string? filePath, string failureReason, string guidance)
    {
        var provider = new TestFileOverviewQueryDataProvider
        {
            NextResult = new(filePath, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(), failureReason, guidance)
        };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal(filePath, error.Value.Value.FilePath);
        Assert.Equal(failureReason, error.Value.Value.FailureReason);
        Assert.Equal(guidance, error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledProviderError_ToSafeFailure()
    {
        var provider = new TestFileOverviewQueryDataProvider { NextException = new InvalidOperationException("sensitive backend details") };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not retrieve the file overview.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request and ensure the solution is loaded.", error.Value.Value.Guidance);
        Assert.Null(error.Value.Value.FilePath);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_BeforeProviderAccess()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestFileOverviewQueryDataProvider();

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
        var provider = new TestFileOverviewQueryDataProvider { CancelDuringCall = cts };

        var (success, error, canceled) = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(1, provider.Calls);
    }

    private static FileOverviewQueryOperation CreateOperation(TestFileOverviewQueryDataProvider provider) =>
        new(provider, NullLogger<FileOverviewQueryOperation>.Instance);

    private static GetFileOverviewRequest CreateValidRequest() => new("C:\\repo\\Sample.sln", "C:\\repo\\source.cs");

    private sealed class TestFileOverviewQueryDataProvider : IFileOverviewQueryDataProvider
    {
        public int Calls { get; private set; }
        public GetFileOverviewResult NextResult { get; init; } = new(null, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(), null, null);
        public Exception? NextException { get; init; }
        public CancellationTokenSource? CancelDuringCall { get; init; }

        public ValueTask<GetFileOverviewResult> GetFileOverviewAsync(GetFileOverviewRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
