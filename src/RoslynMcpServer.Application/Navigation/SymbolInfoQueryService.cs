using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation;

public sealed class SymbolInfoQueryService(
    SymbolInfoQueryOperation operation) : ISymbolInfoQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetSymbolInfoResult(
                Symbol: null,
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetSymbolInfoResult(
                Symbol: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
