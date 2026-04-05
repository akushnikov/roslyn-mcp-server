using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Retrieves workspace cache state without triggering solution loads.
/// </summary>
public interface IWorkspaceStateService
{
    /// <summary>
    /// Returns the current workspace cache state for an optional solution path.
    /// </summary>
    ValueTask<GetWorkspaceStateResult> GetStateAsync(
        GetWorkspaceStateRequest request,
        CancellationToken cancellationToken = default);
}
