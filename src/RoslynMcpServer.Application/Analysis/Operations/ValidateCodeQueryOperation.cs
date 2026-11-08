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

/// <summary>Executes ValidateCode queries through the shared validation, cancellation and telemetry pipeline.</summary>
public sealed class ValidateCodeQueryOperation(
    IValidateCodeQueryDataProvider dataProvider,
    ILogger<ValidateCodeQueryOperation> logger) : QueryOperationBase<ValidateCodeRequest, ValidateCodeResult, QueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<ValidateCodeResult>, Error<QueryError>, Canceled>> ExecuteCoreAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.ValidateCodeAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<QueryError>(
                new QueryError(providerResult.FailureReason, providerResult.Guidance));
        }

        return new Success<ValidateCodeResult>(providerResult);
    }

    protected override Error<QueryError>? Validate(ValidateCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<QueryError>(
                new QueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        return null;
    }

    protected override Error<QueryError> MapUnhandledError(
        ValidateCodeRequest request,
        Exception exception)
    {
        return new Error<QueryError>(
            new QueryError(
                "The server could not compute diagnostics for the requested solution.",
                "Retry the request and confirm the solution is loaded."));
    }
}
