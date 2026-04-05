namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests type hierarchy information for the symbol resolved at a specific document position.
/// </summary>
public sealed record GetTypeHierarchyRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column,
    string? Direction);
