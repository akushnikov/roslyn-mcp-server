using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Provides transport-safe workspace snapshots that are synchronized with the live Roslyn workspace session.
/// </summary>
public interface IWorkspaceSnapshotProvider
{
    /// <summary>
    /// Returns an up-to-date snapshot for a loaded workspace after pending synchronization work has completed.
    /// </summary>
    ValueTask<LoadedWorkspaceSnapshot?> GetReadySnapshotAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
