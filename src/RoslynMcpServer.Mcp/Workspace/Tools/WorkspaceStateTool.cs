using System.ComponentModel;
using ModelContextProtocol.Server;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;
using RoslynMcpServer.Abstractions.Workspace.Services;

namespace RoslynMcpServer.Mcp.Workspace.Tools;

/// <summary>
/// Returns the current state of the server-side workspace cache.
/// </summary>
[McpServerToolType]
internal sealed class WorkspaceStateTool(IWorkspaceStateService workspaceStateService)
{
    /// <summary>
    /// Returns cache state for an optional specific solution path.
    /// </summary>
    [McpServerTool(Name = "get_workspace_state")]
    [Description("Returns the current workspace cache state and optional selected solution.")]
    public ValueTask<GetWorkspaceStateResult> GetWorkspaceState(
        [Description("Optional absolute path to a previously loaded .sln, .slnx, or .csproj file.")] string? solutionPath,
        CancellationToken cancellationToken = default)
        => workspaceStateService.GetStateAsync(new GetWorkspaceStateRequest(solutionPath), cancellationToken);
}
