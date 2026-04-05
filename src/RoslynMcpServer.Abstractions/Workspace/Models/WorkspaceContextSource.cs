namespace RoslynMcpServer.Abstractions.Workspace.Models;

/// <summary>
/// Identifies how a workspace context was resolved.
/// </summary>
public enum WorkspaceContextSource
{
    ExplicitSolutionPath = 0,
    ClientRoots = 1,
    ServerConfiguration = 2
}
