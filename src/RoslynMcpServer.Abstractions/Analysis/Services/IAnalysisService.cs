using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;

namespace RoslynMcpServer.Abstractions.Analysis.Services;

/// <summary>
/// Provides read-only Roslyn analysis over a loaded workspace.
/// </summary>
public interface IAnalysisService
{
    /// <summary>
    /// Returns compiler diagnostics for a loaded solution or source file.
    /// </summary>
    ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(
        GetDiagnosticsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns implementations for the symbol resolved at a source position.
    /// </summary>
    ValueTask<FindImplementationsResult> FindImplementationsAsync(
        FindImplementationsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns type hierarchy information for the symbol resolved at a source position.
    /// </summary>
    ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(
        GetTypeHierarchyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns callers for the symbol resolved at a source position.
    /// </summary>
    ValueTask<FindCallersResult> FindCallersAsync(
        FindCallersRequest request,
        CancellationToken cancellationToken = default);
}
