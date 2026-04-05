using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Requests;

/// <summary>
/// Describes the available inputs that may be used to resolve a workspace context.
/// </summary>
public sealed record ResolveWorkspaceContextRequest(
    string? ExplicitSolutionPath,
    string? ConfiguredSolutionPath,
    IReadOnlyList<WorkspaceRoot> ClientRoots,
    bool ClientSupportsRoots);
