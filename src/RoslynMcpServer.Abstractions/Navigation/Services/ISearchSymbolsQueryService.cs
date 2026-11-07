using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the application boundary for searching source declarations in a loaded solution.
/// </summary>
public interface ISearchSymbolsQueryService
{
    /// <summary>
    /// Searches source declarations across a previously loaded solution.
    /// </summary>
    ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken = default);
}
