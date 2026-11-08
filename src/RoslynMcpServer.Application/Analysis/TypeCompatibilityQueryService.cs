using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>Projects TypeCompatibility query outcomes without storing request context.</summary>
public sealed class TypeCompatibilityQueryService(
    TypeCompatibilityQueryOperation operation) : ITypeCompatibilityQueryService
{
    /// <inheritdoc />
    public async ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibilityAsync(
        CheckTypeCompatibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new CheckTypeCompatibilityResult(
                Source: error.Source,
                Target: error.Target,
                Compatibility: null,
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new CheckTypeCompatibilityResult(
                Source: null,
                Target: null,
                Compatibility: null,
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
