using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Loads solutions and projects into the server workspace cache.
/// </summary>
public interface IWorkspaceLoader
{
    /// <summary>
    /// Loads the requested solution or project into the cache or reuses an existing entry.
    /// </summary>
    ValueTask<LoadSolutionResult> LoadAsync(
        LoadSolutionRequest request,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
