using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Analysis.Operations;

/// <summary>
/// Executes the compiler diagnostics query through the shared validation, cancellation and telemetry pipeline.
/// </summary>
public sealed class DiagnosticsQueryOperation(
    IDiagnosticsQueryDataProvider dataProvider,
    ILogger<DiagnosticsQueryOperation> logger) : QueryOperationBase<GetDiagnosticsRequest, GetDiagnosticsResult, AnalysisQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetDiagnosticsResult>, Error<AnalysisQueryError>, Canceled>> ExecuteCoreAsync(
        GetDiagnosticsRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetDiagnosticsAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(providerResult.FailureReason, providerResult.Guidance));
        }

        return new Success<GetDiagnosticsResult>(providerResult);
    }

    protected override Error<AnalysisQueryError>? Validate(GetDiagnosticsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        return null;
    }

    protected override Error<AnalysisQueryError> MapUnhandledError(
        GetDiagnosticsRequest request,
        Exception exception)
    {
        return new Error<AnalysisQueryError>(
            new AnalysisQueryError(
                "The server could not compute diagnostics for the requested solution.",
                "Retry the request and confirm the solution is loaded."));
    }
}
