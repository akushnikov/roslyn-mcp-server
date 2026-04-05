using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents semantic information for a source symbol.
/// </summary>
public sealed record GetSymbolInfoResult(
    SymbolDescriptor? Symbol,
    string? FailureReason,
    string? Guidance);
