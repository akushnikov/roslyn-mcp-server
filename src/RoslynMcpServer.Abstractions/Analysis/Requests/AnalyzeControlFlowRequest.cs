namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests control flow analysis for a contiguous source region.
/// </summary>
public sealed record AnalyzeControlFlowRequest(
    string SolutionPath,
    string FilePath,
    int StartLine,
    int EndLine);
