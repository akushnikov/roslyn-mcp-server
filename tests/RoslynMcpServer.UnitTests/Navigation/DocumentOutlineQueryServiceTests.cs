using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class DocumentOutlineQueryServiceTests
{
    [Fact]
    public async Task GetDocumentOutlineAsync_PassesThroughSuccess_WithOrderedNestedLocations()
    {
        var start = new SymbolLocation(@"C:\repo\File.cs", 3, 1);
        var end = new SymbolLocation(start.FilePath, 10, 2);
        var firstChild = new DocumentOutlineNode("Z", "Method", start, end, Array.Empty<DocumentOutlineNode>());
        var secondChild = new DocumentOutlineNode("A", "Property", start, end, Array.Empty<DocumentOutlineNode>());
        var expected = new GetDocumentOutlineResult(start.FilePath,
            [new DocumentOutlineNode("Worker", "Class", start, end, [firstChild, secondChild])], null, null);

        var result = await CreateService(expected).GetDocumentOutlineAsync(CreateValidRequest());

        Assert.Same(expected, result);
        var root = Assert.Single(result.Nodes);
        Assert.Equal(new[] { "Z", "A" }, root.Children.Select(child => child.Name));
        Assert.Same(firstChild, root.Children[0]);
        Assert.Same(start, root.Start);
        Assert.Same(end, root.End);
        Assert.Null(result.FailureReason);
    }

    [Theory]
    [InlineData(null, "The requested solution is not currently loaded.")]
    [InlineData(@"C:\repo\Missing.cs", "The requested file was not found in the loaded solution.")]
    [InlineData(@"C:\repo\Other.vb", "The requested document does not have a C# compilation unit syntax tree.")]
    public async Task GetDocumentOutlineAsync_PreservesProviderFailureContext(string? filePath, string failureReason)
    {
        var expected = new GetDocumentOutlineResult(filePath, Array.Empty<DocumentOutlineNode>(), failureReason, "Existing guidance.");

        var result = await CreateService(expected).GetDocumentOutlineAsync(CreateValidRequest());

        Assert.Equal(filePath, result.FilePath);
        Assert.Empty(result.Nodes);
        Assert.Equal(expected.FailureReason, result.FailureReason);
        Assert.Equal(expected.Guidance, result.Guidance);
    }

    [Fact]
    public async Task GetDocumentOutlineAsync_ProjectsValidationFailure_WithoutInventingDocumentContext()
    {
        var result = await CreateService().GetDocumentOutlineAsync(new("", ""));

        Assert.Null(result.FilePath);
        Assert.Empty(result.Nodes);
        Assert.Equal("A non-empty solutionPath is required.", result.FailureReason);
        Assert.NotNull(result.Guidance);
    }

    [Fact]
    public async Task GetDocumentOutlineAsync_MapsCancellation_ToStableNonErrorPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CreateService().GetDocumentOutlineAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.FilePath);
        Assert.Empty(result.Nodes);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static DocumentOutlineQueryService CreateService(GetDocumentOutlineResult? result = null) =>
        new(new DocumentOutlineQueryOperation(
            new TestDocumentOutlineQueryDataProvider { NextResult = result ?? new(null, Array.Empty<DocumentOutlineNode>(), null, null) },
            NullLogger<DocumentOutlineQueryOperation>.Instance));

    private static GetDocumentOutlineRequest CreateValidRequest() =>
        new(@"C:\repo\Sample.sln", @"C:\repo\File.cs");

    private sealed class TestDocumentOutlineQueryDataProvider : IDocumentOutlineQueryDataProvider
    {
        public required GetDocumentOutlineResult NextResult { get; init; }

        public ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
            GetDocumentOutlineRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }
}
