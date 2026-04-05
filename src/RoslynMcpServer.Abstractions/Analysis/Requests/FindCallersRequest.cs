namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests callers for the symbol resolved at a specific document position.
/// </summary>
public sealed record FindCallersRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    int MaxResults = 100);
