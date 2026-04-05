namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests data flow analysis for a contiguous source region.
/// </summary>
public sealed record AnalyzeDataFlowRequest(
    string SolutionPath,
    string FilePath,
    int StartLine,
    int EndLine);
