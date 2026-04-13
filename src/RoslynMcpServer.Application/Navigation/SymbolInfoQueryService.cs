using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

using RoslynMcpServer.Abstractions.QueryPipeline.Models;

namespace RoslynMcpServer.Application.Navigation;

/// <summary>
/// Application boundary service for symbol-info query requests.
/// </summary>
internal sealed class SymbolInfoQueryService(
    SymbolInfoQueryOperation operation) : ISymbolInfoQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return result.Match(
            static success => success.Value,
            static error => FromFailure(error.Value),
            static _ => FromCanceled());
    }

    private static GetSymbolInfoResult FromFailure(QueryError error)
    {
        return new GetSymbolInfoResult(
            Symbol: null,
            FailureReason: error.FailureReason,
            Guidance: error.Guidance);
    }

    private static GetSymbolInfoResult FromCanceled()
    {
        return new GetSymbolInfoResult(
            Symbol: null,
            FailureReason: "The operation was canceled.",
            Guidance: "Retry the request when the operation can run to completion.");
    }
}
