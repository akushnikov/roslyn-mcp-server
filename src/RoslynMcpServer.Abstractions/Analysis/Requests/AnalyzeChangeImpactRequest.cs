namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests a semantic impact summary for the symbol resolved at a specific document position.
/// </summary>
public sealed record AnalyzeChangeImpactRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    int MaxResults = 100);
