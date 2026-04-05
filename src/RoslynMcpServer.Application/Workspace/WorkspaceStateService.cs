using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Application.Workspace;

/// <summary>
/// Reads the current state of the workspace cache without loading solutions.
/// </summary>
public sealed class WorkspaceStateService(IWorkspaceCache workspaceCache) : IWorkspaceStateService
{
    /// <inheritdoc />
    public async ValueTask<GetWorkspaceStateResult> GetStateAsync(
        GetWorkspaceStateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var cachedSnapshots = await workspaceCache.ListAsync(cancellationToken);
        var cachedSolutions = cachedSnapshots
            .Select(static snapshot => snapshot.Workspace)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            var snapshot = await workspaceCache.GetAsync(request.SolutionPath, cancellationToken);
            if (snapshot is null)
            {
                return new GetWorkspaceStateResult(
                    HasLoadedSolution: false,
                    Workspace: null,
                    CachedSolutions: cachedSolutions,
                    FailureReason: "The requested solution is not currently loaded.",
                    Guidance: "Call load_solution first for the requested solutionPath.");
            }

            return new GetWorkspaceStateResult(
                HasLoadedSolution: true,
                Workspace: snapshot.Workspace,
                CachedSolutions: cachedSolutions,
                FailureReason: null,
                Guidance: null);
        }

        WorkspaceDescriptor? selectedWorkspace = cachedSnapshots.Count switch
        {
            1 => cachedSnapshots[0].Workspace,
            _ => null
        };

        return new GetWorkspaceStateResult(
            HasLoadedSolution: selectedWorkspace is not null,
            Workspace: selectedWorkspace,
            CachedSolutions: cachedSolutions,
            FailureReason: null,
            Guidance: selectedWorkspace is null && cachedSnapshots.Count > 1
                ? "Multiple solutions are loaded. Pass solutionPath to select a specific workspace."
                : null);
    }
}
