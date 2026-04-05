namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests a symbol search over a loaded solution.
/// </summary>
public sealed record SearchSymbolsRequest(
    string SolutionPath,
    string Query,
    string? KindFilter,
    int MaxResults = 50);
