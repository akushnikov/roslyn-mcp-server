using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents caller locations for a resolved symbol.
/// </summary>
public sealed record FindCallersResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<CallerDescriptor> Callers,
    string? FailureReason,
    string? Guidance);
