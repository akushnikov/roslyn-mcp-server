using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Requests;

/// <summary>
/// Requests execution of the stateful load-solution command pipeline.
/// </summary>
public sealed record LoadSolutionCommandRequest(
    LoadSolutionRequest LoadRequest,
    IProgress<WorkspaceLoadProgress>? Progress);
