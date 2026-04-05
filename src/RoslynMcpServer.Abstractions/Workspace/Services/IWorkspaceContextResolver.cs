using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Defines a boundary for resolving the workspace context needed by Roslyn use cases.
/// </summary>
public interface IWorkspaceContextResolver
{
    /// <summary>
    /// Resolves a workspace context from explicit input, client-provided roots, or server defaults.
    /// </summary>
    ValueTask<ResolveWorkspaceContextResult> ResolveAsync(
        ResolveWorkspaceContextRequest request,
        CancellationToken cancellationToken = default);
}
