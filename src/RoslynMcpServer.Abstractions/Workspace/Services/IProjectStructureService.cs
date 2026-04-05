using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Retrieves project structure for already loaded workspaces.
/// </summary>
public interface IProjectStructureService
{
    /// <summary>
    /// Returns project and document structure for a loaded solution.
    /// </summary>
    ValueTask<GetProjectStructureResult> GetProjectStructureAsync(
        GetProjectStructureRequest request,
        CancellationToken cancellationToken = default);
}
