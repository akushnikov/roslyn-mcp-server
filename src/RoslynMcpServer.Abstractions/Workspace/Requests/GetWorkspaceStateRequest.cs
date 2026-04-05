namespace RoslynMcpServer.Abstractions.Workspace.Requests;

/// <summary>
/// Requests the current state of the workspace cache.
/// </summary>
public sealed record GetWorkspaceStateRequest(
    string? SolutionPath);
