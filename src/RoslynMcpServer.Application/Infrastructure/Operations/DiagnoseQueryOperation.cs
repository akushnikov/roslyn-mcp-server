using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.Infrastructure.Requests;
using RoslynMcpServer.Abstractions.Infrastructure.Results;
using RoslynMcpServer.Abstractions.Infrastructure.Services;
using RoslynMcpServer.Abstractions.Infrastructure.Models;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.Server.Services;
using RoslynMcpServer.Abstractions.Workspace.Results;
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
    protected override async ValueTask<OneOf<Success<DiagnoseResult>, Error<ExceptionDispatchInfo>, Canceled>> ExecuteCoreAsync(
        DiagnoseRequest request,
        CancellationToken cancellationToken)
    {
        var server = await dataProvider.GetServerInfoAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var environment = await runtimeEnvironmentService.GetEnvironmentAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        WorkspaceHealthDescriptor? workspace = null;
        IReadOnlyList<WorkspaceOperationDiagnostic> diagnostics = Array.Empty<WorkspaceOperationDiagnostic>();
        if (!string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            workspace = await runtimeEnvironmentService.ProbeWorkspaceAsync(request.SolutionPath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics = request.Verbose
                ? workspace.Diagnostics
                : workspace.Diagnostics
                    .Where(static diagnostic => diagnostic.Severity == WorkspaceDiagnosticSeverity.Error)
                    .ToArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new Success<DiagnoseResult>(new DiagnoseResult(server, environment, workspace, diagnostics));
    }

    /// <inheritdoc />
    protected override Error<ExceptionDispatchInfo> MapUnhandledError(
        DiagnoseRequest request,
        Exception exception) => new(ExceptionDispatchInfo.Capture(exception));
}
