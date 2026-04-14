using OneOf;
using OneOf.Types;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Workspace.Operations;

public sealed class ResolveWorkspaceContextQueryOperation(
    ILogger<ResolveWorkspaceContextQueryOperation> logger)
    : QueryOperationBase<ResolveWorkspaceContextRequest, ResolveWorkspaceContextResult, QueryError>(logger)
{
    protected override ValueTask<OneOf<Success<ResolveWorkspaceContextResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        ResolveWorkspaceContextRequest request,
        CancellationToken cancellationToken)
    {
        var result = Resolve(request);
        return ValueTask.FromResult<OneOf<Success<ResolveWorkspaceContextResult>, Error<QueryError>, Canceled>>(
            new Success<ResolveWorkspaceContextResult>(result));
    }

    protected override Error<QueryError> MapUnhandledError(
        ResolveWorkspaceContextRequest request,
        Exception exception)
        => new(new QueryError(
            "The server could not resolve workspace context.",
            "Retry the request and provide solutionPath explicitly if the problem persists."));

    private static ResolveWorkspaceContextResult Resolve(ResolveWorkspaceContextRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ExplicitSolutionPath))
        {
            return new ResolveWorkspaceContextResult(
                IsResolved: true,
                Context: new WorkspaceContext(
                    NormalizePath(request.ExplicitSolutionPath),
                    Array.Empty<WorkspaceRoot>(),
                    WorkspaceContextSource.ExplicitSolutionPath),
                FailureReason: null,
                Guidance: "Using the explicit solutionPath provided by the caller.");
        }

        if (request.ClientRoots.Count > 0)
        {
            return new ResolveWorkspaceContextResult(
                IsResolved: true,
                Context: new WorkspaceContext(
                    null,
                    request.ClientRoots,
                    WorkspaceContextSource.ClientRoots),
                FailureReason: null,
                Guidance: "Client workspace roots are available, but no solution has been selected yet. Pass solutionPath explicitly for workspace-dependent tools.");
        }

        if (!string.IsNullOrWhiteSpace(request.ConfiguredSolutionPath))
        {
            return new ResolveWorkspaceContextResult(
                IsResolved: true,
                Context: new WorkspaceContext(
                    NormalizePath(request.ConfiguredSolutionPath),
                    Array.Empty<WorkspaceRoot>(),
                    WorkspaceContextSource.ServerConfiguration),
                FailureReason: null,
                Guidance: "Using the server-configured default solutionPath.");
        }

        var guidance = request.ClientSupportsRoots
            ? "Client roots are supported, but no workspace roots were provided. Provide the current workspace root from the client or pass solutionPath explicitly."
            : "This client does not advertise roots support. Pass solutionPath explicitly or configure a default solution path on the server.";

        return new ResolveWorkspaceContextResult(
            IsResolved: false,
            Context: null,
            FailureReason: "Workspace context is required before Roslyn operations can run.",
            Guidance: guidance);
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
