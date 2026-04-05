namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes Roslyn data flow analysis for a selected source region.
/// </summary>
public sealed record DataFlowDescriptor(
    IReadOnlyList<string> VariablesDeclared,
    IReadOnlyList<string> DataFlowsIn,
    IReadOnlyList<string> DataFlowsOut,
    IReadOnlyList<string> ReadInside,
    IReadOnlyList<string> WrittenInside,
    IReadOnlyList<string> ReadOutside,
    IReadOnlyList<string> WrittenOutside,
    IReadOnlyList<string> Captured,
    IReadOnlyList<string> AlwaysAssigned,
    bool Succeeded);
