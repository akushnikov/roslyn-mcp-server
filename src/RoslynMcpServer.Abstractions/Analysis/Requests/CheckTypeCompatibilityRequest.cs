namespace RoslynMcpServer.Abstractions.Analysis.Requests;

/// <summary>
/// Requests a compatibility check between two resolved types.
/// </summary>
public sealed record CheckTypeCompatibilityRequest(
    string SolutionPath,
    string SourceFilePath,
    int SourceLine,
    int SourceColumn,
    string TargetFilePath,
    int TargetLine,
    int TargetColumn);
