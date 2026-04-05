using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes an outgoing call edge from a source member.
/// </summary>
public sealed record CallEdgeDescriptor(
    SymbolDescriptor? Target,
    SymbolLocation Location,
    string? LineText);
