using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.WorkspaceSync.Requests;
using RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

namespace RoslynMcpServer.Infrastructure.Roslyn.Workspace;

/// <summary>
/// Coordinates loaded workspaces with the sync subsystem before exposing semantic sessions.
/// </summary>
internal sealed class WorkspaceSessionProvider(
    IRoslynWorkspaceAccessor workspaceAccessor,
    WorkspaceCoordinatorRegistry workspaceCoordinatorRegistry) : IWorkspaceSessionProvider
{
    public async ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedPath = NormalizePath(solutionPath);
        var initialSession = await workspaceAccessor.GetSessionAsync(normalizedPath, cancellationToken);
        if (initialSession is null)
        {
            return null;
        }

        await workspaceCoordinatorRegistry.EnsureStartedAsync(
            new EnsureWorkspaceSyncRequest(normalizedPath),
            cancellationToken);

        await workspaceCoordinatorRegistry.WaitForQuiescenceAsync(normalizedPath, cancellationToken);

        return await workspaceAccessor.GetSessionAsync(normalizedPath, cancellationToken);
    }

    public async ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
        => (await GetReadySessionAsync(solutionPath, cancellationToken))?.Snapshot;

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
