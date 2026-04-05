namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests a validation summary for a loaded solution or a specific source file.
/// </summary>
public sealed record ValidateCodeRequest(
    string SolutionPath,
    string? FilePath);
