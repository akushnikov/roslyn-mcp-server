namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests a compact overview for a source file in the loaded solution.
/// </summary>
public sealed record GetFileOverviewRequest(
    string SolutionPath,
    string FilePath);
