using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents outgoing calls for a resolved callable symbol.
/// </summary>
public sealed record GetOutgoingCallsResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<CallEdgeDescriptor> Calls,
    string? FailureReason,
    string? Guidance);
