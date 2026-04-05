using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes Roslyn control flow analysis for a selected source region.
/// </summary>
public sealed record ControlFlowDescriptor(
    bool StartPointIsReachable,
    bool EndPointIsReachable,
    IReadOnlyList<SymbolLocation> EntryPoints,
    IReadOnlyList<SymbolLocation> ExitPoints,
    IReadOnlyList<SymbolLocation> ReturnStatements,
    bool Succeeded);
