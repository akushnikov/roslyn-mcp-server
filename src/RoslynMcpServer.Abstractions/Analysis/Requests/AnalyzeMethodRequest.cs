namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests a compound semantic analysis for the callable member resolved at a specific document position.
/// </summary>
public sealed record AnalyzeMethodRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    int MaxOutgoingCalls = 100);
