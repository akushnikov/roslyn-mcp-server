namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Represents the cacheable, transport-safe snapshot of a loaded workspace.
/// </summary>
public sealed record LoadedWorkspaceSnapshot(
    WorkspaceDescriptor Workspace,
    IReadOnlyList<ProjectStructureDescriptor> Projects);
