using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Application.Workspace;

/// <summary>
/// Returns project structure information for already loaded workspaces.
/// </summary>
public sealed class ProjectStructureService(IWorkspaceSnapshotProvider workspaceSnapshotProvider) : IProjectStructureService
{
    /// <inheritdoc />
    public async ValueTask<GetProjectStructureResult> GetProjectStructureAsync(
        GetProjectStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await workspaceSnapshotProvider.GetReadySnapshotAsync(request.SolutionPath, cancellationToken);
        if (snapshot is null)
        {
            return new GetProjectStructureResult(
                Workspace: null,
                Projects: Array.Empty<ProjectStructureDescriptor>(),
                FailureReason: "The requested solution is not currently loaded.",
                Guidance: "Call load_solution first for the requested solutionPath.");
        }

        var projects = request.IncludeDocuments
            ? snapshot.Projects
            : snapshot.Projects
                .Select(static project => project with { Documents = Array.Empty<DocumentDescriptor>() })
                .ToArray();

        return new GetProjectStructureResult(
            Workspace: snapshot.Workspace,
            Projects: projects,
            FailureReason: null,
            Guidance: null);
    }
}
