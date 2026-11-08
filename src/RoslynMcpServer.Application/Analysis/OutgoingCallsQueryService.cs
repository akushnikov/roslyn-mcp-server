using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>Projects OutgoingCalls query outcomes without storing request context.</summary>
public sealed class OutgoingCallsQueryService(
    OutgoingCallsQueryOperation operation) : IOutgoingCallsQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetOutgoingCallsResult> GetOutgoingCallsAsync(
        GetOutgoingCallsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetOutgoingCallsResult(
                Symbol: error.Symbol,
                Calls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetOutgoingCallsResult(
                Symbol: null,
                Calls: Array.Empty<CallEdgeDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
