namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests semantic symbol information for a specific document position.
/// </summary>
public sealed record GetSymbolInfoRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
