using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Workspace;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.UnitTests.Workspace;

public sealed class ProjectStructureServiceTests
{
    [Fact]
    public async Task GetProjectStructureAsync_StripsDocuments_WhenIncludeDocumentsIsFalse()
    {
        var workspace = CreateWorkspace();
        var project = new ProjectStructureDescriptor(
            Name: "Sample",
            FilePath: @"C:\repo\Sample.csproj",
            TargetFrameworks: ["net9.0"],
            DocumentCount: 1,
            ProjectReferences: Array.Empty<string>(),
            Documents:
            [
                new DocumentDescriptor("Program.cs", @"C:\repo\Program.cs")
            ]);

        var service = CreateService(new TestWorkspaceSnapshotProvider
        {
            Snapshot = new LoadedWorkspaceSnapshot(workspace, [project])
        });

        var result = await service.GetProjectStructureAsync(
            new GetProjectStructureRequest(@"C:\repo\Sample.sln", IncludeDocuments: false));

        Assert.Equal(workspace, result.Workspace);
        var returnedProject = Assert.Single(result.Projects);
        Assert.Empty(returnedProject.Documents);
        Assert.Equal(project.DocumentCount, returnedProject.DocumentCount);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task GetProjectStructureAsync_ReturnsFailurePayload_WhenSnapshotIsMissing()
    {
        var service = CreateService(new TestWorkspaceSnapshotProvider());

        var result = await service.GetProjectStructureAsync(
            new GetProjectStructureRequest(@"C:\repo\Missing.sln", IncludeDocuments: true));

        Assert.Null(result.Workspace);
        Assert.Empty(result.Projects);
        Assert.Equal("The requested solution is not currently loaded.", result.FailureReason);
        Assert.Equal("Call load_solution first for the requested solutionPath.", result.Guidance);
    }

    [Fact]
    public async Task GetProjectStructureAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = CreateService(new TestWorkspaceSnapshotProvider());

        var result = await service.GetProjectStructureAsync(
            new GetProjectStructureRequest(@"C:\repo\Sample.sln", IncludeDocuments: true),
            cts.Token);

        Assert.Null(result.Workspace);
        Assert.Empty(result.Projects);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static ProjectStructureService CreateService(IWorkspaceSnapshotProvider snapshotProvider)
        => new(new ProjectStructureQueryOperation(
            snapshotProvider,
            NullLogger<ProjectStructureQueryOperation>.Instance));

    private static WorkspaceDescriptor CreateWorkspace()
        => new(
            SolutionPath: @"C:\repo\Sample.sln",
            SolutionName: "Sample",
            LoadedAtUtc: DateTimeOffset.UtcNow,
            ProjectCount: 1,
            DocumentCount: 1);

    private sealed class TestWorkspaceSnapshotProvider : IWorkspaceSnapshotProvider
    {
        public LoadedWorkspaceSnapshot? Snapshot { get; init; }

        public ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(
            string solutionPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Snapshot);
        }
    }
}
