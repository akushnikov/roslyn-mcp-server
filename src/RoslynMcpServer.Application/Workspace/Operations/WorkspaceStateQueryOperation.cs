using OneOf;
using OneOf.Types;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Workspace.Operations;

public sealed class WorkspaceStateQueryOperation(
    IWorkspaceCache workspaceCache,
    ILogger<WorkspaceStateQueryOperation> logger)
    : QueryOperationBase<GetWorkspaceStateRequest, GetWorkspaceStateResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetWorkspaceStateResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        GetWorkspaceStateRequest request,
        CancellationToken cancellationToken)
    {
        var cachedSnapshots = await workspaceCache.ListAsync(cancellationToken);
        var cachedSolutions = cachedSnapshots
            .Select(static snapshot => snapshot.Workspace)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            var snapshot = await workspaceCache.GetAsync(request.SolutionPath, cancellationToken);
            if (snapshot is null)
            {
                return new Error<QueryError>(new QueryError(
                    "The requested solution is not currently loaded.",
                    "Call load_solution first for the requested solutionPath."));
            }

            return new Success<GetWorkspaceStateResult>(new GetWorkspaceStateResult(
                HasLoadedSolution: true,
                Workspace: snapshot.Workspace,
                CachedSolutions: cachedSolutions,
                FailureReason: null,
                Guidance: null));
        }

        WorkspaceDescriptor? selectedWorkspace = cachedSnapshots.Count switch
        {
            1 => cachedSnapshots[0].Workspace,
            _ => null
        };

        return new Success<GetWorkspaceStateResult>(new GetWorkspaceStateResult(
            HasLoadedSolution: selectedWorkspace is not null,
            Workspace: selectedWorkspace,
            CachedSolutions: cachedSolutions,
            FailureReason: null,
            Guidance: selectedWorkspace is null && cachedSnapshots.Count > 1
                ? "Multiple solutions are loaded. Pass solutionPath to select a specific workspace."
                : null));
    }

    protected override Error<QueryError> MapUnhandledError(
        GetWorkspaceStateRequest request,
        Exception exception)
        => new(new QueryError(
            "The server could not read workspace state.",
            "Retry the request after ensuring the workspace cache is available."));
}
