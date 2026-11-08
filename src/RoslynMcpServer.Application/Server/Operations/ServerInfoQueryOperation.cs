using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.Server.Requests;
using RoslynMcpServer.Abstractions.Server.Results;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Server.Operations;

/// <summary>
/// Runs server metadata collection through the shared cancellation and telemetry pipeline.
/// </summary>
public sealed class ServerInfoQueryOperation(
    IServerInfoQueryDataProvider dataProvider,
    ILogger<ServerInfoQueryOperation> logger)
    : QueryOperationBase<GetServerInfoRequest, GetServerInfoResult, ExceptionDispatchInfo>(logger)
{
    /// <inheritdoc />
    protected override ValueTask<OneOf<Success<GetServerInfoResult>, Error<ExceptionDispatchInfo>, Canceled>> ExecuteCoreAsync(
        GetServerInfoRequest request,
        CancellationToken cancellationToken)
    {
        _ = dataProvider;
        throw new NotImplementedException("ServerInfo core execution is completed by plan 16-02.");
    }

    /// <inheritdoc />
    protected override Error<ExceptionDispatchInfo> MapUnhandledError(
        GetServerInfoRequest request,
        Exception exception) => new(ExceptionDispatchInfo.Capture(exception));
}
