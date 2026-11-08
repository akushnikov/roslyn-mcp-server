using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation.Operations;

/// <summary>Validates signature requests before provider access and promotes provider failures into pipeline errors.</summary>
public sealed class MethodSignatureQueryOperation(
    IMethodSignatureQueryDataProvider dataProvider,
    ILogger<MethodSignatureQueryOperation> logger) : QueryOperationBase<GetMethodSignatureRequest, GetMethodSignatureResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetMethodSignatureResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        GetMethodSignatureRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetMethodSignatureAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<QueryError>(
                new QueryError(providerResult.FailureReason, providerResult.Guidance));
        }

        return new Success<GetMethodSignatureResult>(providerResult);
    }

    protected override Error<QueryError>? Validate(GetMethodSignatureRequest request)
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

    protected override Error<QueryError> MapUnhandledError(GetMethodSignatureRequest request, Exception exception) =>
        new(new QueryError(
            "The server could not retrieve the method signature.",
            "Retry the request and ensure the solution is loaded."));
}
