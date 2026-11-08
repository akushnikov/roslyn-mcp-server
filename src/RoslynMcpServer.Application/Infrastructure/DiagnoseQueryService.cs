using System.Diagnostics;
using System.Runtime.ExceptionServices;
using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Application.Infrastructure.Operations;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Infrastructure;

/// <summary>
/// Projects diagnostic snapshots while preserving original exceptions and caller cancellation.
/// </summary>
public sealed class DiagnoseQueryService(DiagnoseQueryOperation operation) : IDiagnoseQueryService
{
    /// <inheritdoc />
    public async ValueTask<DiagnoseResult> DiagnoseAsync(
        DiagnoseRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(request, cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => Rethrow(error),
            () => throw new OperationCanceledException(cancellationToken));
    }

    private static DiagnoseResult Rethrow(ExceptionDispatchInfo error)
    {
        error.Throw();
        throw new UnreachableException();
    }
}
