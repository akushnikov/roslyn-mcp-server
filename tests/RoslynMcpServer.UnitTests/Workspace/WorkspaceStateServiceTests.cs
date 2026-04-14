using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Workspace;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.UnitTests.Workspace;

public sealed class WorkspaceStateServiceTests
{
    [Fact]
    public async Task GetStateAsync_ReturnsSelectedWorkspace_WhenRequestedSolutionIsLoaded()
    {
        var workspace = CreateWorkspace(@"C:\repo\Sample.sln", "Sample");
        var service = CreateService(new TestWorkspaceCache
        {
            Snapshot = new LoadedWorkspaceSnapshot(workspace, Array.Empty<ProjectStructureDescriptor>())
        });

        var result = await service.GetStateAsync(new GetWorkspaceStateRequest(@"C:\repo\Sample.sln"));

        Assert.True(result.HasLoadedSolution);
        Assert.Equal(workspace, result.Workspace);
        Assert.Single(result.CachedSolutions);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsFailurePayload_WhenRequestedSolutionIsMissing()
    {
        var service = CreateService(new TestWorkspaceCache());

        var result = await service.GetStateAsync(new GetWorkspaceStateRequest(@"C:\repo\Missing.sln"));

        Assert.False(result.HasLoadedSolution);
        Assert.Null(result.Workspace);
        Assert.Empty(result.CachedSolutions);
        Assert.Equal("The requested solution is not currently loaded.", result.FailureReason);
        Assert.Equal("Call load_solution first for the requested solutionPath.", result.Guidance);
    }

    [Fact]
    public async Task GetStateAsync_MapsCanceledResult_ToStableCancellationPayload()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = CreateService(new TestWorkspaceCache());

        var result = await service.GetStateAsync(new GetWorkspaceStateRequest(null), cts.Token);

        Assert.False(result.HasLoadedSolution);
        Assert.Null(result.Workspace);
        Assert.Empty(result.CachedSolutions);
        Assert.Equal("The operation was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);
    }

    private static WorkspaceStateService CreateService(IWorkspaceCache workspaceCache)
        => new(new WorkspaceStateQueryOperation(
            workspaceCache,
            NullLogger<WorkspaceStateQueryOperation>.Instance));

    private static WorkspaceDescriptor CreateWorkspace(string solutionPath, string solutionName)
        => new(
            SolutionPath: solutionPath,
            SolutionName: solutionName,
            LoadedAtUtc: DateTimeOffset.UtcNow,
            ProjectCount: 1,
            DocumentCount: 2);

    private sealed class TestWorkspaceCache : IWorkspaceCache
    {
        public LoadedWorkspaceSnapshot? Snapshot { get; init; }

        public ValueTask<LoadedWorkspaceSnapshot?> GetAsync(
            string solutionPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Snapshot);
        }

        public ValueTask<IReadOnlyList<LoadedWorkspaceSnapshot>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<LoadedWorkspaceSnapshot>>(
                Snapshot is null ? Array.Empty<LoadedWorkspaceSnapshot>() : [Snapshot]);
        }
    }
}
