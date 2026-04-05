using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Workspace;

/// <summary>
/// Coordinates loaded workspaces with the sync subsystem before exposing semantic sessions or synchronized snapshots.
/// </summary>
internal interface IWorkspaceSessionProvider : IWorkspaceSnapshotProvider
{
    /// <summary>
    /// Returns a synchronized live Roslyn workspace session for the requested solution path.
    /// </summary>
    ValueTask<RoslynWorkspaceSession?> GetReadySessionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
