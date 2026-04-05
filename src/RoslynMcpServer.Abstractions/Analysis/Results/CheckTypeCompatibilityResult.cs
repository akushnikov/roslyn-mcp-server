using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Results;

/// <summary>
/// Represents a compatibility check between two resolved types.
/// </summary>
public sealed record CheckTypeCompatibilityResult(
    SymbolDescriptor? Source,
    SymbolDescriptor? Target,
    TypeCompatibilityDescriptor? Compatibility,
    string? FailureReason,
    string? Guidance);
