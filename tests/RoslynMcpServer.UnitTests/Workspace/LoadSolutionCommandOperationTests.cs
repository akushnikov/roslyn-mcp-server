using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.UnitTests.Workspace;

public sealed class LoadSolutionCommandOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWithChangedEffects_WhenWorkspaceLoaded()
    {
        var workspace = new WorkspaceDescriptor(
            SolutionPath: @"C:\repo\Sample.sln",
            SolutionName: "Sample",
            LoadedAtUtc: DateTimeOffset.UtcNow,
            ProjectCount: 2,
            DocumentCount: 5);

        var loader = new TestWorkspaceLoader
        {
            NextResult = new LoadSolutionResult(
                Workspace: workspace,
                WasAlreadyLoaded: false,
                WasReloaded: false,
                Diagnostics: Array.Empty<WorkspaceOperationDiagnostic>(),
                FailureReason: null,
                Guidance: null)
        };

        var cache = new TestWorkspaceCache
        {
            Snapshot = null
        };

        var operation = new LoadSolutionCommandOperation(loader, cache);
        var result = await operation.ExecuteAsync(
            new LoadSolutionCommandRequest(
                new LoadSolutionRequest(@"C:\repo\Sample.sln", ForceReload: false),
                Progress: null));

        var (success, error, canceled) = result;
        Assert.NotNull(success);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.True(success.Value.Value.Effects.Changed);
        Assert.NotNull(success.Value.Value.Effects.WorkspaceVersionAfter);
        Assert.Equal(Path.GetFullPath(@"C:\repo\Sample.sln"), success.Value.Value.Effects.NormalizedSolutionPath);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWithNoOpEffects_WhenWorkspaceAlreadyLoaded()
    {
        var beforeLoadedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var beforeWorkspace = new WorkspaceDescriptor(
            SolutionPath: @"C:\repo\Sample.sln",
            SolutionName: "Sample",
            LoadedAtUtc: beforeLoadedAt,
            ProjectCount: 2,
            DocumentCount: 5);

        var afterWorkspace = beforeWorkspace with { LoadedAtUtc = beforeLoadedAt };

        var loader = new TestWorkspaceLoader
        {
            NextResult = new LoadSolutionResult(
                Workspace: afterWorkspace,
                WasAlreadyLoaded: true,
                WasReloaded: false,
                Diagnostics: Array.Empty<WorkspaceOperationDiagnostic>(),
                FailureReason: null,
                Guidance: null)
        };

        var cache = new TestWorkspaceCache
        {
            Snapshot = new LoadedWorkspaceSnapshot(beforeWorkspace, Array.Empty<ProjectStructureDescriptor>())
        };

        var operation = new LoadSolutionCommandOperation(loader, cache);
        var result = await operation.ExecuteAsync(
            new LoadSolutionCommandRequest(
                new LoadSolutionRequest(@"C:\repo\Sample.sln", ForceReload: false),
                Progress: null));

        var (success, error, canceled) = result;
        Assert.NotNull(success);
        Assert.Null(error);
        Assert.Null(canceled);
        Assert.False(success.Value.Value.Effects.Changed);
        Assert.Equal(
            beforeLoadedAt.ToUnixTimeMilliseconds(),
            success.Value.Value.Effects.WorkspaceVersionBefore);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationError_WhenSolutionPathMissing()
    {
        var operation = new LoadSolutionCommandOperation(new TestWorkspaceLoader(), new TestWorkspaceCache());

        var result = await operation.ExecuteAsync(
            new LoadSolutionCommandRequest(
                new LoadSolutionRequest(string.Empty, ForceReload: false),
                Progress: null));

        var (success, error, canceled) = result;
        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);
        Assert.Equal("A non-empty solutionPath is required.", error.Value.Value.FailureReason);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCanceled_WhenCancellationRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var operation = new LoadSolutionCommandOperation(new TestWorkspaceLoader(), new TestWorkspaceCache());

        var result = await operation.ExecuteAsync(
            new LoadSolutionCommandRequest(
                new LoadSolutionRequest(@"C:\repo\Sample.sln", ForceReload: false),
                Progress: null),
            cts.Token);

        var (success, error, canceled) = result;
        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);
    }

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

        public ValueTask<LoadedWorkspaceSnapshot?> GetAsync(string solutionPath, CancellationToken cancellationToken = default)
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
