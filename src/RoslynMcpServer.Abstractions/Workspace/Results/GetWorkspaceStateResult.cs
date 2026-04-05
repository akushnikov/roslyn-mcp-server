using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Represents the current state of the workspace cache and an optional selected solution.
/// </summary>
public sealed record GetWorkspaceStateResult(
    bool HasLoadedSolution,
    WorkspaceDescriptor? Workspace,
    IReadOnlyList<WorkspaceDescriptor> CachedSolutions,
    string? FailureReason,
    string? Guidance);
