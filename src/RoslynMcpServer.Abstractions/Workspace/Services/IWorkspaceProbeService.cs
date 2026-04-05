using RoslynMcpServer.Abstractions.Infrastructure.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Probes a solution or project path without mutating the shared workspace cache.
/// </summary>
public interface IWorkspaceProbeService
{
    /// <summary>
    /// Probes the specified solution or project path and returns load diagnostics.
    /// </summary>
    ValueTask<WorkspaceHealthDescriptor> ProbeAsync(
        string solutionPath,
        CancellationToken cancellationToken = default);
}
