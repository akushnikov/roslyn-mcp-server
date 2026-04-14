using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.QueryPipeline;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.Application.Workspace;

public sealed class WorkspaceStateService(
    WorkspaceStateQueryOperation operation) : IWorkspaceStateService
{
    /// <inheritdoc />
    public async ValueTask<GetWorkspaceStateResult> GetStateAsync(
        GetWorkspaceStateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetWorkspaceStateResult(
                HasLoadedSolution: false,
                Workspace: null,
                CachedSolutions: Array.Empty<WorkspaceDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetWorkspaceStateResult(
                HasLoadedSolution: false,
                Workspace: null,
                CachedSolutions: Array.Empty<WorkspaceDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
