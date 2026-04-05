using RoslynMcpServer.Abstractions.Analysis.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents compiler diagnostics for a loaded solution or file.
/// </summary>
public sealed record GetDiagnosticsResult(
    IReadOnlyList<AnalysisDiagnosticDescriptor> Diagnostics,
    string? FailureReason,
    string? Guidance);
