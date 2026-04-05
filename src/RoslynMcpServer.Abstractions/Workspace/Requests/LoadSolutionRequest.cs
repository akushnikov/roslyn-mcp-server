namespace RoslynMcpServer.Abstractions.Workspace.Requests;

/// <summary>
/// Requests that a solution or project be loaded into the server workspace cache.
/// </summary>
public sealed record LoadSolutionRequest(
    string SolutionPath,
    bool ForceReload);
