using RoslynMcpServer.Abstractions.Navigation.Models;

namespace RoslynMcpServer.Abstractions.Navigation.Results;

/// <summary>
/// Represents the structural outline for a source document.
/// </summary>
public sealed record GetDocumentOutlineResult(
    string? FilePath,
    IReadOnlyList<DocumentOutlineNode> Nodes,
    string? FailureReason,
    string? Guidance);
