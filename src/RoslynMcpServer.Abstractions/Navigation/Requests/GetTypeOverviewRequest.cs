namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests a compact overview for the type resolved at a specific document position.
/// </summary>
public sealed record GetTypeOverviewRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
