using RoslynMcpServer.Abstractions.Workspace.Models;
using RoslynMcpServer.Abstractions.Workspace.Requests;
using RoslynMcpServer.Abstractions.Workspace.Results;

namespace RoslynMcpServer.Abstractions.Workspace.Services;

/// <summary>
/// Application boundary for the stateful load-solution command pipeline.
/// </summary>
public interface ILoadSolutionCommandService
{
    /// <summary>
    /// Executes load_solution through the command abstraction.
    /// </summary>
    ValueTask<LoadSolutionResult> LoadSolutionAsync(
        LoadSolutionRequest request,
        IProgress<WorkspaceLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
