namespace RoslynMcpServer.Abstractions.WorkspaceSync.Requests;

/// <summary>
/// Requests that live synchronization be started or refreshed for a loaded workspace.
/// </summary>
public sealed record EnsureWorkspaceSyncRequest(
    string WorkspacePath);
