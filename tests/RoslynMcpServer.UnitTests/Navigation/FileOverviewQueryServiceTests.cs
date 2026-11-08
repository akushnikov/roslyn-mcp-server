using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation;
using RoslynMcpServer.Application.Navigation.Operations;

namespace RoslynMcpServer.UnitTests.Navigation;

public sealed class FileOverviewQueryServiceTests
{
    [Fact]
    public async Task GetFileOverviewAsync_PassesThroughSuccessPayload()
    {
        var type = new FileTypeDescriptor("Example", "Class", new("source.cs", 2, 1), 3);
        var expected = new GetFileOverviewResult("source.cs", new[] { "Sample" }, new[] { type }, null, null);

        var result = await CreateService(expected).GetFileOverviewAsync(CreateValidRequest());

        Assert.Same(expected, result);
        Assert.Same(expected.Namespaces, result.Namespaces);
        Assert.Same(type, Assert.Single(result.Types));
    }

    [Theory]
    [InlineData(null, "The requested solution is not currently loaded.", "Call load_solution first for the requested solutionPath.")]
    [InlineData("C:\\repo\\missing.cs", "The requested file was not found in the loaded solution.", "Call get_project_structure with includeDocuments=true to inspect valid file paths.")]
    [InlineData("C:\\repo\\source.cs", "The requested document does not have a C# compilation unit syntax tree.", "Use a C# source file that belongs to the loaded solution.")]
    public async Task GetFileOverviewAsync_PreservesFailureStageFilePath(
        string? filePath, string failureReason, string guidance)
    {
        var expected = new GetFileOverviewResult(filePath, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(), failureReason, guidance);

        var result = await CreateService(expected).GetFileOverviewAsync(CreateValidRequest());

        Assert.Equal(filePath, result.FilePath);
        Assert.Empty(result.Namespaces);
        Assert.Empty(result.Types);
        Assert.Equal(failureReason, result.FailureReason);
        Assert.Equal(guidance, result.Guidance);
    }

    [Fact]
    public async Task GetFileOverviewAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await CreateService(new(null, Array.Empty<string>(), Array.Empty<FileTypeDescriptor>(), null, null))
            .GetFileOverviewAsync(CreateValidRequest(), cts.Token);

        Assert.Null(result.FilePath);
        Assert.Empty(result.Namespaces);
        Assert.Empty(result.Types);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static GetFileOverviewRequest CreateValidRequest() => new("C:\\repo\\Sample.sln", "C:\\repo\\source.cs");

    private static FileOverviewQueryService CreateService(GetFileOverviewResult result) =>
        new(new FileOverviewQueryOperation(new TestFileOverviewQueryDataProvider(result), NullLogger<FileOverviewQueryOperation>.Instance));

    private sealed class TestFileOverviewQueryDataProvider(GetFileOverviewResult result) : IFileOverviewQueryDataProvider
    {
        public ValueTask<GetFileOverviewResult> GetFileOverviewAsync(GetFileOverviewRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
