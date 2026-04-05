namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Represents the resolved workspace context that Roslyn-based use cases can operate on.
/// </summary>
public sealed record WorkspaceContext(
    string? SolutionPath,
    IReadOnlyList<WorkspaceRoot> Roots,
    WorkspaceContextSource Source);
