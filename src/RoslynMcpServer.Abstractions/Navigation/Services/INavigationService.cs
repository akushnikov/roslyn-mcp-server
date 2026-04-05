using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Provides read-only semantic navigation over a loaded Roslyn workspace.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Returns a declaration outline for the requested source document.
    /// </summary>
    ValueTask<GetDocumentOutlineResult> GetDocumentOutlineAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches source declarations across a loaded solution.
    /// </summary>
    ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns semantic information for the symbol resolved at a source position.
    /// </summary>
    ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns source definition locations for the symbol resolved at a source position.
    /// </summary>
    ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns source references for the symbol resolved at a source position.
    /// </summary>
    ValueTask<FindReferencesResult> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default);
}
