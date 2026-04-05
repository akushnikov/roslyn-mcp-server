using RoslynMcpServer.Abstractions.Analysis.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents a validation summary for a loaded solution or source file.
/// </summary>
public sealed record ValidateCodeResult(
    bool IsValid,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<AnalysisDiagnosticDescriptor> Diagnostics,
    string? FailureReason,
    string? Guidance);
