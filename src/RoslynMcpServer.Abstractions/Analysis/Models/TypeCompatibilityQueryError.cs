using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>Preserves resolved context when the TypeCompatibility query cannot complete.</summary>
/// <param name="FailureReason">Transport-safe failure description.</param>
/// <param name="Guidance">Optional recovery guidance for the caller.</param>
/// <param name="Source">Context resolved before the failure, if available.</param>
/// <param name="Target">Context resolved before the failure, if available.</param>
public sealed record TypeCompatibilityQueryError(
    string FailureReason,
    string? Guidance,
    SymbolDescriptor? Source = null,
    SymbolDescriptor? Target = null);
