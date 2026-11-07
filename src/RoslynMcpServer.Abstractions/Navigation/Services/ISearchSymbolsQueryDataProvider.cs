using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the backend data provider contract for symbol-search query operations.
/// </summary>
public interface ISearchSymbolsQueryDataProvider
{
    /// <summary>
    /// Searches source declarations from the underlying workspace backend.
    /// </summary>
    ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken = default);
}
