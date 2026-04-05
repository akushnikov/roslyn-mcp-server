namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests source definition locations for the symbol resolved at a specific document position.
/// </summary>
public sealed record GoToDefinitionRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
