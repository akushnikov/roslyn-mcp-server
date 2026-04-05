using RoslynMcpServer.Abstractions.Workspace.Models;

namespace RoslynMcpServer.Abstractions.Workspace.Results;

/// <summary>
/// Represents the client-facing result of the workspace context tool.
/// </summary>
public sealed record GetWorkspaceContextResult(
    bool IsResolved,
    bool ClientSupportsRoots,
    WorkspaceContext? Context,
    string? FailureReason,
    string? Guidance);
