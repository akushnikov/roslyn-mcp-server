namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests implementations for the symbol resolved at a specific document position.
/// </summary>
public sealed record FindImplementationsRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    int MaxResults = 100);
