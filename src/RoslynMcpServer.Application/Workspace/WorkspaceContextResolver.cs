using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;


namespace RoslynMcpServer.Application.Workspace;

/// <summary>
/// Resolves the workspace context required by Roslyn use cases.
/// </summary>
/// <remarks>
/// Resolution currently prefers an explicit solution path, then client-provided roots,
/// and finally a server-configured fallback. This keeps transport-specific concerns
/// outside the application layer while still allowing MCP adapters to pass client context in.
/// </remarks>
public sealed class WorkspaceContextResolver : IWorkspaceContextResolver
{
    /// <summary>
    /// Resolves a workspace context from the available explicit, client, and server inputs.
    /// </summary>
    public ValueTask<ResolveWorkspaceContextResult> ResolveAsync(
        ResolveWorkspaceContextRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(request.ExplicitSolutionPath))
        {
            return ValueTask.FromResult(new ResolveWorkspaceContextResult(
                IsResolved: true,
                Context: new WorkspaceContext(
                    NormalizePath(request.ExplicitSolutionPath),
                    Array.Empty<WorkspaceRoot>(),
                    WorkspaceContextSource.ExplicitSolutionPath),
                FailureReason: null,
                Guidance: "Using the explicit solutionPath provided by the caller."));
        }

        if (request.ClientRoots.Count > 0)
        {
            return ValueTask.FromResult(new ResolveWorkspaceContextResult(
                IsResolved: true,
                Context: new WorkspaceContext(
                    null,
                    request.ClientRoots,
                    WorkspaceContextSource.ClientRoots),
                FailureReason: null,
                Guidance: "Client workspace roots are available, but no solution has been selected yet. Pass solutionPath explicitly for workspace-dependent tools."));
        }

        if (!string.IsNullOrWhiteSpace(request.ConfiguredSolutionPath))
        {
            return ValueTask.FromResult(new ResolveWorkspaceContextResult(
                IsResolved: true,
                Context: new WorkspaceContext(
                    NormalizePath(request.ConfiguredSolutionPath),
                    Array.Empty<WorkspaceRoot>(),
                    WorkspaceContextSource.ServerConfiguration),
                FailureReason: null,
                Guidance: "Using the server-configured default solutionPath."));
        }

        var guidance = request.ClientSupportsRoots
            ? "Client roots are supported, but no workspace roots were provided. Provide the current workspace root from the client or pass solutionPath explicitly."
            : "This client does not advertise roots support. Pass solutionPath explicitly or configure a default solution path on the server.";

        return ValueTask.FromResult(new ResolveWorkspaceContextResult(
            IsResolved: false,
            Context: null,
            FailureReason: "Workspace context is required before Roslyn operations can run.",
            Guidance: guidance));
    }
    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
