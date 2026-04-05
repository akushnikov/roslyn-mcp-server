namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests signature details for the callable member resolved at a specific document position.
/// </summary>
public sealed record GetMethodSignatureRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
