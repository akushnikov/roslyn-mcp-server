using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents symbol search results for a loaded solution.
/// </summary>
public sealed record SearchSymbolsResult(
    IReadOnlyList<SymbolDescriptor> Symbols,
    string? FailureReason,
    string? Guidance);
