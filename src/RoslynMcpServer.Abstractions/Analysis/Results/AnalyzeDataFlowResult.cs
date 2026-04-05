using RoslynMcpServer.Abstractions.Analysis.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents data flow analysis for a selected source region.
/// </summary>
public sealed record AnalyzeDataFlowResult(
    DataFlowDescriptor? Analysis,
    string? FailureReason,
    string? Guidance);
