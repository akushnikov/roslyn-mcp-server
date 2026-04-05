namespace RoslynMcpServer.Abstractions.Navigation.Requests;

/// <summary>
/// Requests members for the type resolved at a specific document position.
/// </summary>
public sealed record GetTypeMembersRequest(
    string SolutionPath,
    string FilePath,
    int Line,
    int Column);
