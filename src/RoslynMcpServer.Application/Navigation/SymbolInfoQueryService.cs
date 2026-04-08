using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Application.Navigation.Operations;

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
            success => success.Value, 
            error => new GetSymbolInfoResult(
                Symbol: null, 
                FailureReason: error.Value.FailureReason, 
                Guidance: error.Value.Guidance),
            _ => new GetSymbolInfoResult(
                Symbol: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
