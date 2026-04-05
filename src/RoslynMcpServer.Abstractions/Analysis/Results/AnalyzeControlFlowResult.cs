using RoslynMcpServer.Abstractions.Analysis.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents control flow analysis for a selected source region.
/// </summary>
public sealed record AnalyzeControlFlowResult(
    ControlFlowDescriptor? Analysis,
    string? FailureReason,
    string? Guidance);
