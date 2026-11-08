using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Infrastructure.Operations;

/// <summary>
/// Runs server and optional workspace diagnostics through the shared query pipeline.
/// </summary>
public sealed class DiagnoseQueryOperation(
    IServerInfoQueryDataProvider dataProvider,
    IRuntimeEnvironmentService runtimeEnvironmentService,
    ILogger<DiagnoseQueryOperation> logger)
    : QueryOperationBase<DiagnoseRequest, DiagnoseResult, ExceptionDispatchInfo>(logger)
{
    /// <inheritdoc />
    protected override ValueTask<OneOf<Success<DiagnoseResult>, Error<ExceptionDispatchInfo>, Canceled>> ExecuteCoreAsync(
        DiagnoseRequest request,
        CancellationToken cancellationToken)
    {
        _ = dataProvider;
        _ = runtimeEnvironmentService;
        throw new NotImplementedException("Diagnose core execution is completed by plan 16-03.");
    }

    /// <inheritdoc />
    protected override Error<ExceptionDispatchInfo> MapUnhandledError(
        DiagnoseRequest request,
        Exception exception) => new(ExceptionDispatchInfo.Capture(exception));
}
