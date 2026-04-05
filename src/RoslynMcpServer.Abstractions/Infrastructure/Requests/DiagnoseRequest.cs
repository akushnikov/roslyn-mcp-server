namespace RoslynMcpServer.Abstractions.Infrastructure.Requests;

/// <summary>
/// Requests an environment and optional workspace diagnostic report.
/// </summary>
public sealed record DiagnoseRequest(
    string? SolutionPath,
    bool Verbose);
