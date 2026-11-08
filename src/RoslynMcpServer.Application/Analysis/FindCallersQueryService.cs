using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>
/// Projects callers query outcomes to the public result without storing request context.
/// </summary>
public sealed class FindCallersQueryService(
    FindCallersQueryOperation operation) : IFindCallersQueryService
{
    /// <inheritdoc />
    public async ValueTask<FindCallersResult> FindCallersAsync(
        FindCallersRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new FindCallersResult(
                Symbol: error.Symbol,
                Callers: Array.Empty<CallerDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new FindCallersResult(
                Symbol: null,
                Callers: Array.Empty<CallerDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
