namespace RoslynMcpServer.Abstractions.WorkspaceSync.Requests;

/// <summary>
/// Requests that live synchronization be stopped for a workspace.
/// </summary>
public sealed record StopWorkspaceSyncRequest(
    string WorkspacePath);
