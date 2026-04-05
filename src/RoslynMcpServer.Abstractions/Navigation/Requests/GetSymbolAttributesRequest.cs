namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests attributes for the symbol resolved at a specific document position.
/// </summary>
public sealed record GetSymbolAttributesRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
