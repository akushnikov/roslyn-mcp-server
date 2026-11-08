using System.Diagnostics;
using System.Runtime.ExceptionServices;
using RoslynMcpServer.Abstractions.Server.Requests;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.QueryPipeline;
using RoslynMcpServer.Application.Server.Operations;

namespace RoslynMcpServer.Application.Server;

/// <summary>
/// Projects server snapshot outcomes while preserving the original exception and cancellation surface.
/// </summary>
public sealed class ServerInfoQueryService(ServerInfoQueryOperation operation) : IServerInfoQueryService
{
    /// <inheritdoc />
    public async ValueTask<GetServerInfoResult> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        var result = await operation.ExecuteAsync(new GetServerInfoRequest(), cancellationToken);

        return QueryResultMapper.Map(
            result,
            static success => success,
            static error => Rethrow(error),
            () => throw new OperationCanceledException(cancellationToken));
    }

    private static GetServerInfoResult Rethrow(ExceptionDispatchInfo error)
    {
        error.Throw();
        throw new UnreachableException();
    }
}
