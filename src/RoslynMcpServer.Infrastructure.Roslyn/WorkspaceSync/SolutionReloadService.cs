using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Services;
using Microsoft.Extensions.Logging;

namespace RoslynMcpServer.Infrastructure.Roslyn.WorkspaceSync;

/// <summary>
/// Performs full solution reloads when incremental synchronization is no longer safe.
/// </summary>
internal sealed class SolutionReloadService(
    IWorkspaceLoader workspaceLoader,
    ILogger<SolutionReloadService> logger)
{
    /// <summary>
    /// Handles a queued full-solution reload request for the specified workspace.
    /// </summary>
    public async ValueTask<WorkspaceReloadResult> HandleAsync(
        string workspacePath,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Applying queued solution reload for workspace {WorkspacePath}. Reason: {Reason}",
            workspacePath,
            reason ?? "unspecified");

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
