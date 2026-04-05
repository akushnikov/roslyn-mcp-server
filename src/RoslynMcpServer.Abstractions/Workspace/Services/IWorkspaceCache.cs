using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Provides read-only access to loaded workspace snapshots.
/// </summary>
public interface IWorkspaceCache
{
    /// <summary>
    /// Returns a loaded workspace snapshot for the specified solution path when available.
    /// </summary>
    ValueTask<LoadedWorkspaceSnapshot?> GetAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all loaded workspace snapshots currently stored in cache.
    /// </summary>
    ValueTask<IReadOnlyList<LoadedWorkspaceSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);
}
