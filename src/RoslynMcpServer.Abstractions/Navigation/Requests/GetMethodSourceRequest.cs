namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests source text for the callable member resolved at a specific document position.
/// </summary>
public sealed record GetMethodSourceRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
