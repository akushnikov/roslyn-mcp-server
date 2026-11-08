using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>
/// Projects implementations query outcomes to the public result without storing request context.
/// </summary>
public sealed class FindImplementationsQueryService(
    FindImplementationsQueryOperation operation) : IFindImplementationsQueryService
{
    /// <inheritdoc />
    public async ValueTask<FindImplementationsResult> FindImplementationsAsync(
        FindImplementationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new FindImplementationsResult(
                Symbol: error.Symbol,
                Implementations: Array.Empty<SymbolDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new FindImplementationsResult(
                Symbol: null,
                Implementations: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
