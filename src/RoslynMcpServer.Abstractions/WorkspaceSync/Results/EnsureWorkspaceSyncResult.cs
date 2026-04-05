using RoslynMcpServer.Abstractions.WorkspaceSync.Models;

namespace RoslynMcpServer.Abstractions.WorkspaceSync.Results;

/// <summary>
/// Represents the outcome of starting or refreshing live synchronization for a workspace.
/// </summary>
public sealed record EnsureWorkspaceSyncResult(
    WorkspaceSyncDescriptor? Workspace,
    bool WasStarted,
    bool WasAlreadyRunning,
    string? FailureReason,
    string? Guidance);
