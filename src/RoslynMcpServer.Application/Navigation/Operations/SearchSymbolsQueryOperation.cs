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
/// Executes symbol searches through the shared query pipeline.
/// </summary>
public sealed class SearchSymbolsQueryOperation(
    ISearchSymbolsQueryDataProvider dataProvider,
    ILogger<SearchSymbolsQueryOperation> logger) : QueryOperationBase<SearchSymbolsRequest, SearchSymbolsResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<SearchSymbolsResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.SearchSymbolsAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<QueryError>(
                new QueryError(providerResult.FailureReason, providerResult.Guidance));
        }

        return new Success<SearchSymbolsResult>(providerResult);
    }

    protected override Error<QueryError>? Validate(SearchSymbolsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<QueryError>(
                new QueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new Error<QueryError>(
                new QueryError(
                    "A non-empty query is required.",
                    "Pass part of a symbol name and optionally a kindFilter."));
        }

        if (request.MaxResults <= 0)
        {
            return new Error<QueryError>(
                new QueryError(
                    "MaxResults must be a positive value.",
                    "Pass a positive integer (e.g., the default of 50)."));
        }

        return null;
    }

    protected override Error<QueryError> MapUnhandledError(
        SearchSymbolsRequest request,
        Exception exception)
    {
        return new Error<QueryError>(
            new QueryError(
                "The server could not complete the symbol search.",
                "Retry the request and ensure the solution is loaded."));
    }
}
