using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents type hierarchy information for a resolved type symbol.
/// </summary>
public sealed record GetTypeHierarchyResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<SymbolDescriptor> BaseTypes,
    IReadOnlyList<SymbolDescriptor> DerivedTypes,
    IReadOnlyList<SymbolDescriptor> Interfaces,
    string? FailureReason,
    string? Guidance);
