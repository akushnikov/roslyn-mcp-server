using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents definition locations resolved for a source symbol.
/// </summary>
public sealed record GoToDefinitionResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<SymbolLocation> Definitions,
    string? FailureReason,
    string? Guidance);
