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

/// <summary>Executes MethodAnalysis queries through the shared validation, cancellation and telemetry pipeline.</summary>
public sealed class MethodAnalysisQueryOperation(
    IMethodAnalysisQueryDataProvider dataProvider,
    ILogger<MethodAnalysisQueryOperation> logger) : QueryOperationBase<AnalyzeMethodRequest, AnalyzeMethodResult, MethodAnalysisQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<AnalyzeMethodResult>, Error<MethodAnalysisQueryError>, Canceled>> ExecuteCoreAsync(
        AnalyzeMethodRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.AnalyzeMethodAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<MethodAnalysisQueryError>(
                new MethodAnalysisQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Signature));
        }

        return new Success<AnalyzeMethodResult>(providerResult);
    }

    protected override Error<MethodAnalysisQueryError>? Validate(AnalyzeMethodRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<MethodAnalysisQueryError>(
                new MethodAnalysisQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new Error<MethodAnalysisQueryError>(
                new MethodAnalysisQueryError(
                    "A non-empty filePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.Line <= 0 || request.Column <= 0)
        {
            return new Error<MethodAnalysisQueryError>(
                new MethodAnalysisQueryError(
                    "Line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        if (request.MaxOutgoingCalls <= 0)
        {
            return new Error<MethodAnalysisQueryError>(
                new MethodAnalysisQueryError(
                    "MaxOutgoingCalls must be a positive value.",
                    "Pass a positive integer (e.g., the default of 100)."));
        }

        return null;
    }

    protected override Error<MethodAnalysisQueryError> MapUnhandledError(
        AnalyzeMethodRequest request,
        Exception exception)
    {
        return new Error<MethodAnalysisQueryError>(
            new MethodAnalysisQueryError(
                "The server could not analyze the resolved callable member.",
                "Retry the request on a source callable member inside the loaded solution."));
    }
}
