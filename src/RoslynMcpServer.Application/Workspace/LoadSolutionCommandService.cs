using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.Application.Workspace;

/// <summary>
/// Application boundary service for load-solution command requests.
/// </summary>
internal sealed class LoadSolutionCommandService(
    LoadSolutionCommandOperation operation) : ILoadSolutionCommandService
{
    /// <inheritdoc />
    public async ValueTask<LoadSolutionResult> LoadSolutionAsync(
        LoadSolutionRequest request,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(
            new LoadSolutionCommandRequest(request, progress),
            cancellationToken);

        return result.Match(
            success => success.Value.Result,
            error => new LoadSolutionResult(
                Workspace: null,
                WasAlreadyLoaded: false,
                WasReloaded: request.ForceReload,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Error,
                        "WorkspaceLoadFailed",
                        error.Value.FailureReason,
                        request.SolutionPath)
                ],
                FailureReason: error.Value.FailureReason,
                Guidance: error.Value.Guidance),
            _ => new LoadSolutionResult(
                Workspace: null,
                WasAlreadyLoaded: false,
                WasReloaded: request.ForceReload,
                Diagnostics:
                [
                    new WorkspaceOperationDiagnostic(
                        WorkspaceDiagnosticSeverity.Warning,
                        "OperationCanceled",
                        "Workspace loading was canceled.",
                        request.SolutionPath)
                ],
                FailureReason: "Workspace loading was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
