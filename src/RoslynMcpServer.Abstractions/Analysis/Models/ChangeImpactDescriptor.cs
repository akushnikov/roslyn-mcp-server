using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes a best-effort semantic impact summary for a resolved symbol.
/// </summary>
public sealed record ChangeImpactDescriptor(
    int ReferenceCount,
    int CallerCount,
    int ImplementationCount,
    IReadOnlyList<string> ImpactedFiles,
    IReadOnlyList<SymbolReferenceDescriptor> References,
    IReadOnlyList<CallerDescriptor> Callers,
    IReadOnlyList<SymbolDescriptor> Implementations);
