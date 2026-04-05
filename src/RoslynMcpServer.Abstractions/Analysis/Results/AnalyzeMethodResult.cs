using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents a compound semantic analysis for a resolved callable member.
/// </summary>
public sealed record AnalyzeMethodResult(
    MethodSignatureDescriptor? Signature,
    DataFlowDescriptor? DataFlow,
    ControlFlowDescriptor? ControlFlow,
    IReadOnlyList<CallEdgeDescriptor> OutgoingCalls,
    string? FailureReason,
    string? Guidance);
