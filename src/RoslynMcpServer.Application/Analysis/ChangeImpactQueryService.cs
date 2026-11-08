using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>Projects ChangeImpact query outcomes without storing request context.</summary>
public sealed class ChangeImpactQueryService(
    ChangeImpactQueryOperation operation) : IChangeImpactQueryService
{
    /// <inheritdoc />
    public async ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpactAsync(
        AnalyzeChangeImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new AnalyzeChangeImpactResult(
                Symbol: error.Symbol,
                Impact: null,
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new AnalyzeChangeImpactResult(
                Symbol: null,
                Impact: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
