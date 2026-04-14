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

public sealed class ProjectStructureQueryOperation(
    IWorkspaceSnapshotProvider workspaceSnapshotProvider,
    ILogger<ProjectStructureQueryOperation> logger)
    : QueryOperationBase<GetProjectStructureRequest, GetProjectStructureResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetProjectStructureResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        GetProjectStructureRequest request,
        CancellationToken cancellationToken)
    {
        var snapshot = await workspaceSnapshotProvider.GetReadySnapshotAsync(request.SolutionPath, cancellationToken);
        if (snapshot is null)
        {
            return new Error<QueryError>(new QueryError(
                "The requested solution is not currently loaded.",
                "Call load_solution first for the requested solutionPath."));
        }

        var projects = request.IncludeDocuments
            ? snapshot.Projects
            : snapshot.Projects
                .Select(static project => project with { Documents = Array.Empty<DocumentDescriptor>() })
                .ToArray();

        return new Success<GetProjectStructureResult>(new GetProjectStructureResult(
            Workspace: snapshot.Workspace,
            Projects: projects,
            FailureReason: null,
            Guidance: null));
    }

    protected override Error<QueryError> MapUnhandledError(
        GetProjectStructureRequest request,
        Exception exception)
        => new(new QueryError(
            "The server could not read project structure.",
            "Retry the request and ensure the solution is loaded."));
}
