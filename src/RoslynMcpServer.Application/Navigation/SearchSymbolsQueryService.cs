using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>
/// Maps symbol-search pipeline outcomes to the public result contract.
/// </summary>
public sealed class SearchSymbolsQueryService(
    SearchSymbolsQueryOperation operation) : ISearchSymbolsQueryService
{
    /// <inheritdoc />
    public async ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new SearchSymbolsResult(
                Symbols: Array.Empty<SymbolDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new SearchSymbolsResult(
                Symbols: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
