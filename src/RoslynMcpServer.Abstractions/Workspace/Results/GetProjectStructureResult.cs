using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Represents the project structure for a previously loaded solution.
/// </summary>
public sealed record GetProjectStructureResult(
    WorkspaceDescriptor? Workspace,
    IReadOnlyList<ProjectStructureDescriptor> Projects,
    string? FailureReason,
    string? Guidance);
