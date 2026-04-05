namespace RoslynMcpServer.Abstractions.Workspace.Requests;

/// <summary>
/// Requests project structure information for a previously loaded solution.
/// </summary>
public sealed record GetProjectStructureRequest(
    string SolutionPath,
    bool IncludeDocuments);
