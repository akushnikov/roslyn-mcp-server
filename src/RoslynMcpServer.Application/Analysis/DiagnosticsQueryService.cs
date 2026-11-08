using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Application.Analysis.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis;

/// <summary>
/// Projects compiler diagnostics query outcomes to the public result without storing request context.
/// </summary>
public sealed class DiagnosticsQueryService(
    DiagnosticsQueryOperation operation) : IDiagnosticsQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetDiagnosticsResult> GetDiagnosticsAsync(
        GetDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => new GetDiagnosticsResult(
                Diagnostics: Array.Empty<AnalysisDiagnosticDescriptor>(),
                FailureReason: error.FailureReason,
                Guidance: error.Guidance),
            static () => new GetDiagnosticsResult(
                Diagnostics: Array.Empty<AnalysisDiagnosticDescriptor>(),
                FailureReason: "The operation was canceled.",
                Guidance: "Retry the request when the operation can run to completion."));
    }
}
