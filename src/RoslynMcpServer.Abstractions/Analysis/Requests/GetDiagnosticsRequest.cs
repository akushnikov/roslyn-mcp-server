namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests compiler diagnostics for a loaded solution or a specific file.
/// </summary>
public sealed record GetDiagnosticsRequest(
    string SolutionPath,
    string? FilePath,
    string? SeverityFilter);
