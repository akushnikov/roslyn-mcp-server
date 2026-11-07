using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation.Operations;

/// <summary>
/// Executes the go-to-definition query through the shared query pipeline.
/// </summary>
public sealed class GoToDefinitionQueryOperation(
    IGoToDefinitionQueryDataProvider dataProvider,
    ILogger<GoToDefinitionQueryOperation> logger) : QueryOperationBase<GoToDefinitionRequest, GoToDefinitionResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GoToDefinitionResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GoToDefinitionAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<QueryError>(
                new QueryError(providerResult.FailureReason, providerResult.Guidance));
        }

        return new Success<GoToDefinitionResult>(providerResult);
    }

    protected override Error<QueryError>? Validate(GoToDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<QueryError>(
                new QueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new Error<QueryError>(
                new QueryError(
                    "A non-empty filePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.Line <= 0 || request.Column <= 0)
        {
            return new Error<QueryError>(
                new QueryError(
                    "Line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        return null;
    }

    protected override Error<QueryError> MapUnhandledError(
        GoToDefinitionRequest request,
        Exception exception)
    {
        return new Error<QueryError>(
            new QueryError(
                "The server could not resolve symbol definitions.",
                "Retry the request and ensure the solution is loaded."));
    }
}
