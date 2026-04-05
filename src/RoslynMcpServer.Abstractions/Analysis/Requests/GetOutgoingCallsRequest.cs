namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests outgoing calls from the callable symbol resolved at a specific document position.
/// </summary>
public sealed record GetOutgoingCallsRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    int MaxResults = 100);
