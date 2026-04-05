namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests source references for the symbol resolved at a specific document position.
/// </summary>
public sealed record FindReferencesRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    int MaxResults = 100);
