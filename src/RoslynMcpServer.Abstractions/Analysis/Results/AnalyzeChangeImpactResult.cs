using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents a best-effort semantic impact summary for a resolved symbol.
/// </summary>
public sealed record AnalyzeChangeImpactResult(
    SymbolDescriptor? Symbol,
    ChangeImpactDescriptor? Impact,
    string? FailureReason,
    string? Guidance);
