using Microsoft.Extensions.Logging.Abstractions;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Workspace;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.UnitTests.Workspace;

public sealed class LoadSolutionCommandServiceTests
{
    [Fact]
    public async Task LoadSolutionAsync_MapsCanceledResult_ToWarningDiagnostic()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var service = CreateService(new TestWorkspaceLoader(), new TestWorkspaceCache());

        var result = await service.LoadSolutionAsync(
            new LoadSolutionRequest(@"C:\repo\Sample.sln", ForceReload: true),
            progress: null,
            cts.Token);

        Assert.Null(result.Workspace);
        Assert.False(result.WasAlreadyLoaded);
        Assert.True(result.WasReloaded);
        Assert.Equal("Workspace loading was canceled.", result.FailureReason);
        Assert.Equal("Retry the request when the operation can run to completion.", result.Guidance);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(WorkspaceDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("OperationCanceled", diagnostic.Code);
    }

    [Fact]
    public async Task LoadSolutionAsync_MapsFailureResult_ToErrorDiagnostic()
    {
        var service = CreateService(
            new TestWorkspaceLoader
            {
                NextResult = new LoadSolutionResult(
                    Workspace: null,
                    WasAlreadyLoaded: false,
                    WasReloaded: false,
                    Diagnostics: Array.Empty<WorkspaceOperationDiagnostic>(),
                    FailureReason: "Workspace failed.",
                    Guidance: "Retry workspace load.")
            },
            new TestWorkspaceCache());

        var result = await service.LoadSolutionAsync(new LoadSolutionRequest(@"C:\repo\Sample.sln", ForceReload: false));

        Assert.Null(result.Workspace);
        Assert.Equal("Workspace failed.", result.FailureReason);
        Assert.Equal("Retry workspace load.", result.Guidance);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(WorkspaceDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("WorkspaceLoadFailed", diagnostic.Code);
        Assert.Equal("Workspace failed.", diagnostic.Message);
    }

    private static LoadSolutionCommandService CreateService(
        IWorkspaceLoader loader,
        IWorkspaceCache cache) =>
        new(
            new LoadSolutionCommandOperation(
                loader,
                cache,
                NullLogger<LoadSolutionCommandOperation>.Instance));

    private sealed class TestWorkspaceLoader : IWorkspaceLoader
    {
        public LoadSolutionResult NextResult { get; set; } = new(
            Workspace: null,
            WasAlreadyLoaded: false,
            WasReloaded: false,
            Diagnostics: Array.Empty<WorkspaceOperationDiagnostic>(),
            FailureReason: null,
            Guidance: null);

        public ValueTask<LoadSolutionResult> LoadAsync(
            LoadSolutionRequest request,
            IProgress<WorkspaceLoadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(NextResult);
        }
    }

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

        public ValueTask<IReadOnlyList<LoadedWorkspaceSnapshot>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<LoadedWorkspaceSnapshot>>(
                Snapshot is null ? Array.Empty<LoadedWorkspaceSnapshot>() : [Snapshot]);
        }
    }
}
