using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Represents the outcome of resolving a workspace context for Roslyn use cases.
/// </summary>
public sealed record ResolveWorkspaceContextResult(
    bool IsResolved,
    WorkspaceContext? Context,
    string? FailureReason,
    string? Guidance);
