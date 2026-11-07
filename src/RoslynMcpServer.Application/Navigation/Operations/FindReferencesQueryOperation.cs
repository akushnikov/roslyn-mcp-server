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
/// Executes the find-references query through the shared query pipeline.
/// </summary>
public sealed class FindReferencesQueryOperation(
    IFindReferencesQueryDataProvider dataProvider,
    ILogger<FindReferencesQueryOperation> logger) : QueryOperationBase<FindReferencesRequest, FindReferencesResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<FindReferencesResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.FindReferencesAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<QueryError>(
                new QueryError(providerResult.FailureReason, providerResult.Guidance));
        }

        return new Success<FindReferencesResult>(providerResult);
    }

    protected override Error<QueryError>? Validate(FindReferencesRequest request)
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

        if (request.MaxResults <= 0)
        {
            return new Error<QueryError>(
                new QueryError(
                    "MaxResults must be a positive value.",
                    "Pass a positive integer (e.g., the default of 100)."));
        }

        return null;
    }

    protected override Error<QueryError> MapUnhandledError(
        FindReferencesRequest request,
        Exception exception)
    {
        return new Error<QueryError>(
            new QueryError(
                "The server could not compute references for the resolved symbol.",
                "Retry the request and confirm the file position resolves to a source symbol."));
    }
}
