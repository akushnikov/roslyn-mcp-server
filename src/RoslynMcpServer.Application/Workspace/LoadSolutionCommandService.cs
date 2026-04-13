using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.Workspace.Operations;

using RoslynMcpServer.Abstractions.CommandPipeline.Models;

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
            static success => success.Value.Result,
            error => FromFailure(request, error.Value),
            _ => FromCanceled(request));
    }

    private static LoadSolutionResult FromFailure(LoadSolutionRequest request, CommandError error)
    {
        return new LoadSolutionResult(
            Workspace: null,
            WasAlreadyLoaded: false,
            WasReloaded: request.ForceReload,
            Diagnostics:
            [
                new WorkspaceOperationDiagnostic(
                    WorkspaceDiagnosticSeverity.Error,
                    "WorkspaceLoadFailed",
                    error.FailureReason,
                    request.SolutionPath)
            ],
            FailureReason: error.FailureReason,
            Guidance: error.Guidance);
    }

    private static LoadSolutionResult FromCanceled(LoadSolutionRequest request)
    {
        return new LoadSolutionResult(
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
            Guidance: "Retry the request when the operation can run to completion.");
    }
}
