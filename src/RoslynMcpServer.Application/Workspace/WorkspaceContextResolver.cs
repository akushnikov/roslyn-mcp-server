using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.QueryPipeline;
using RoslynMcpServer.Application.Workspace.Operations;

namespace RoslynMcpServer.Application.Workspace;

public sealed class WorkspaceContextResolver(
    ResolveWorkspaceContextQueryOperation operation) : IWorkspaceContextResolver
{
    /// <summary>
    /// Resolves a workspace context from the available explicit, client, and server inputs.
    /// </summary>
    public async ValueTask<ResolveWorkspaceContextResult> ResolveAsync(
        ResolveWorkspaceContextRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new ResolveWorkspaceContextResult(
                IsResolved: false,
                Context: null,
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new ResolveWorkspaceContextResult(
                IsResolved: false,
                Context: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
