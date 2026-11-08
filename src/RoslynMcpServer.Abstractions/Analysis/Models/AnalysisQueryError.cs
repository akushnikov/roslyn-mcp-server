using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Preserves resolved symbol context when an analysis query cannot complete.
/// </summary>
/// <param name="FailureReason">Transport-safe failure description.</param>
/// <param name="Guidance">Optional recovery guidance for the caller.</param>
/// <param name="Symbol">Symbol resolved before the failure, if available.</param>
public sealed record AnalysisQueryError(
    string FailureReason,
    string? Guidance,
    SymbolDescriptor? Symbol = null);
