using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>Projects MethodAnalysis query outcomes without storing request context.</summary>
public sealed class MethodAnalysisQueryService(
    MethodAnalysisQueryOperation operation) : IMethodAnalysisQueryService
{
    /// <inheritdoc />
    public async ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(
        AnalyzeMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new AnalyzeMethodResult(
                Signature: error.Signature,
                DataFlow: null,
                ControlFlow: null,
                OutgoingCalls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new AnalyzeMethodResult(
                Signature: null,
                DataFlow: null,
                ControlFlow: null,
                OutgoingCalls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
