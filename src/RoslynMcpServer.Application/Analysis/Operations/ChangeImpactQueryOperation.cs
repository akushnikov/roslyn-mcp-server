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

/// <summary>Executes ChangeImpact queries through the shared validation, cancellation and telemetry pipeline.</summary>
public sealed class ChangeImpactQueryOperation(
    IChangeImpactQueryDataProvider dataProvider,
    ILogger<ChangeImpactQueryOperation> logger) : QueryOperationBase<AnalyzeChangeImpactRequest, AnalyzeChangeImpactResult, AnalysisQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<AnalyzeChangeImpactResult>, Error<AnalysisQueryError>, Canceled>> ExecuteCoreAsync(
        AnalyzeChangeImpactRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.AnalyzeChangeImpactAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Symbol));
        }

        return new Success<AnalyzeChangeImpactResult>(providerResult);
    }

    protected override Error<AnalysisQueryError>? Validate(AnalyzeChangeImpactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(
                    "A non-empty filePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.Line <= 0 || request.Column <= 0)
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(
                    "Line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        if (request.MaxResults <= 0)
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(
                    "MaxResults must be a positive value.",
                    "Pass a positive integer (e.g., the default of 100)."));
        }

        return null;
    }

    protected override Error<AnalysisQueryError> MapUnhandledError(
        AnalyzeChangeImpactRequest request,
        Exception exception)
    {
        return new Error<AnalysisQueryError>(
            new AnalysisQueryError(
                "The server could not compute a semantic impact summary for the resolved symbol.",
                "Retry the request on a source symbol inside the loaded solution."));
    }
}
