using RoslynMcpServer.Abstractions.WorkspaceSync.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using Microsoft.Extensions.Logging;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Performs project-scoped reloads when the workspace root supports them.
/// </summary>
internal sealed class ProjectReloadService(
    IWorkspaceLoader workspaceLoader,
    ILogger<ProjectReloadService> logger)
{
    /// <summary>
    /// Handles the queued project reload requests for the specified workspace.
    /// </summary>
    public async ValueTask<WorkspaceReloadResult> HandleAsync(
        string workspacePath,
        IReadOnlyList<ProjectReloadRequest> reloadRequests,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(reloadRequests);
        cancellationToken.ThrowIfCancellationRequested();

        if (reloadRequests.Count == 0)
        {
            return new WorkspaceReloadResult(
                Succeeded: true,
                RequiresCoordinatorRefresh: false,
                FailureReason: null,
                Diagnostics: Array.Empty<WorkspaceOperationDiagnostic>());
        }

        logger.LogInformation(
            "Applying {ProjectReloadCount} queued project reload request(s) for workspace {WorkspacePath}",
            reloadRequests.Count,
            workspacePath);

        var result = await workspaceLoader.LoadAsync(
            new LoadSolutionRequest(workspacePath, ForceReload: true),
            progress: null,
            cancellationToken);

        return new WorkspaceReloadResult(
            Succeeded: result.Workspace is not null && string.IsNullOrWhiteSpace(result.FailureReason),
            RequiresCoordinatorRefresh: result.Workspace is not null && string.IsNullOrWhiteSpace(result.FailureReason),
            FailureReason: result.FailureReason,
            Diagnostics: result.Diagnostics);
    }
}
