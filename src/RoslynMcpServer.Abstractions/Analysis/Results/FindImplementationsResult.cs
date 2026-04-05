using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents implementation symbols for a resolved symbol.
/// </summary>
public sealed record FindImplementationsResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<SymbolDescriptor> Implementations,
    string? FailureReason,
    string? Guidance);
