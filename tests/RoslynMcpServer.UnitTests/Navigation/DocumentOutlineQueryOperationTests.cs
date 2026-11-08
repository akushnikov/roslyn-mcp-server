using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class DocumentOutlineQueryOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsProviderHierarchyUnchanged_OnSuccess()
    {
        var location = new SymbolLocation(@"C:\repo\File.cs", 2, 1);
        var child = new DocumentOutlineNode("Run", "Method", location, location, Array.Empty<DocumentOutlineNode>());
        var node = new DocumentOutlineNode("Worker", "Class", location, location, [child]);
        var expected = new GetDocumentOutlineResult(location.FilePath, [node], null, null);
        var provider = new TestDocumentOutlineQueryDataProvider { NextResult = expected };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Same(expected, success.Value.Value);
        Assert.Same(node, Assert.Single(success.Value.Value.Nodes));
        Assert.Same(child, Assert.Single(node.Children));
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData("", "", "A non-empty solutionPath is required.")]
    [InlineData(" ", " ", "A non-empty solutionPath is required.")]
    [InlineData(@"C:\repo\Sample.sln", "", "A non-empty filePath is required.")]
    [InlineData(@"C:\repo\Sample.sln", "\t", "A non-empty filePath is required.")]
    public async Task ExecuteAsync_ValidatesPathsInOrder_AndSkipsProvider(
        string solutionPath, string filePath, string expectedFailure)
    {
        var provider = new TestDocumentOutlineQueryDataProvider();

        var result = await CreateOperation(provider).ExecuteAsync(new(solutionPath, filePath));
        var (success, error, canceled) = result;

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
    [InlineData(@"C:\repo\Missing.cs", "The requested file was not found in the loaded solution.", "Call get_project_structure with includeDocuments=true to inspect valid file paths.")]
    [InlineData(@"C:\repo\Other.vb", "The requested document does not have a C# compilation unit syntax tree.", "Use a C# source file that belongs to the loaded solution.")]
    public async Task ExecuteAsync_PreservesStageSpecificContext_InProviderFailure(
        string? filePath, string failureReason, string guidance)
    {
        var provider = new TestDocumentOutlineQueryDataProvider
        {
            NextResult = new(filePath, Array.Empty<DocumentOutlineNode>(), failureReason, guidance)
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal(filePath, error.Value.Value.FilePath);
        Assert.Equal(failureReason, error.Value.Value.FailureReason);
        Assert.Equal(guidance, error.Value.Value.Guidance);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_MapsUnhandledFault_ToSafeFailure()
    {
        var provider = new TestDocumentOutlineQueryDataProvider
        {
            NextException = new InvalidOperationException("sensitive backend details")
        };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest());
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("The server could not retrieve the document outline.", error.Value.Value.FailureReason);
        Assert.Equal("Retry the request and ensure the solution is loaded.", error.Value.Value.Guidance);
        Assert.DoesNotContain("sensitive", error.Value.Value.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(error.Value.Value.FilePath);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WithoutCallingProvider_WhenAlreadyCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new TestDocumentOutlineQueryDataProvider();

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
        var provider = new TestDocumentOutlineQueryDataProvider { OnCall = cts.Cancel };

        var result = await CreateOperation(provider).ExecuteAsync(CreateValidRequest(), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
        Assert.Equal(cts.Token, provider.LastToken);
        Assert.Equal(1, provider.Calls);
    }

    private static DocumentOutlineQueryOperation CreateOperation(TestDocumentOutlineQueryDataProvider provider) =>
        new(provider, NullLogger<DocumentOutlineQueryOperation>.Instance);

    private static GetDocumentOutlineRequest CreateValidRequest() =>
        new(@"C:\repo\Sample.sln", @"C:\repo\File.cs");

    private sealed class TestDocumentOutlineQueryDataProvider : IDocumentOutlineQueryDataProvider
    {
        public int Calls { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Action? OnCall { get; init; }
        public Exception? NextException { get; init; }
        public GetDocumentOutlineResult NextResult { get; init; } = new(null, Array.Empty<DocumentOutlineNode>(), null, null);

        public ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
            GetDocumentOutlineRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = cancellationToken;
            OnCall?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return NextException is not null ? throw NextException : ValueTask.FromResult(NextResult);
        }
    }
}
