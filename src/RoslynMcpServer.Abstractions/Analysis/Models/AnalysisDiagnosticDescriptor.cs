using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Analysis.Models;

/// <summary>
/// Describes a compiler or analyzer diagnostic in a transport-safe shape.
/// </summary>
public sealed record AnalysisDiagnosticDescriptor(
    string Id,
    string Severity,
    string Message,
    SymbolLocation? Location);
