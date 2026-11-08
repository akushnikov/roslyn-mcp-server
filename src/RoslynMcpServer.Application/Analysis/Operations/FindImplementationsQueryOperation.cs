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
/// Executes the implementations query through the shared validation, cancellation and telemetry pipeline.
/// </summary>
public sealed class FindImplementationsQueryOperation(
    IFindImplementationsQueryDataProvider dataProvider,
    ILogger<FindImplementationsQueryOperation> logger) : QueryOperationBase<FindImplementationsRequest, FindImplementationsResult, AnalysisQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<FindImplementationsResult>, Error<AnalysisQueryError>, Canceled>> ExecuteCoreAsync(
        FindImplementationsRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.FindImplementationsAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<AnalysisQueryError>(
                new AnalysisQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Symbol));
        }

        return new Success<FindImplementationsResult>(providerResult);
    }

    protected override Error<AnalysisQueryError>? Validate(FindImplementationsRequest request)
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
        FindImplementationsRequest request,
        Exception exception)
    {
        return new Error<AnalysisQueryError>(
            new AnalysisQueryError(
                "The server could not compute implementations for the resolved symbol.",
                "Retry the request on an interface, abstract member, or base type."));
    }
}
