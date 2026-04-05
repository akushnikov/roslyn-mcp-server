using RoslynMcpServer.Abstractions.WorkspaceSync.Requests;
using RoslynMcpServer.Abstractions.WorkspaceSync.Results;

namespace RoslynMcpServer.Abstractions.WorkspaceSync.Services;

/// <summary>
/// Manages live synchronization between the file system and loaded Roslyn workspaces.
/// </summary>
public interface IWorkspaceSyncService
{
    /// <summary>
    /// Ensures that synchronization is running for the requested workspace.
    /// </summary>
    ValueTask<EnsureWorkspaceSyncResult> EnsureStartedAsync(
        EnsureWorkspaceSyncRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops synchronization for the requested workspace when it is running.
    /// </summary>
    ValueTask StopAsync(
        StopWorkspaceSyncRequest request,
        CancellationToken cancellationToken = default);
}
