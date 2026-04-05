namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests a structural outline for a source document in a loaded solution.
/// </summary>
public sealed record GetDocumentOutlineRequest(
    string SolutionPath,
    string FilePath);
