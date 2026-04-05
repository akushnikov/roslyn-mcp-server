using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents source references resolved for a symbol.
/// </summary>
public sealed record FindReferencesResult(
    SymbolDescriptor? Symbol,
    IReadOnlyList<SymbolReferenceDescriptor> References,
    string? FailureReason,
    string? Guidance);
