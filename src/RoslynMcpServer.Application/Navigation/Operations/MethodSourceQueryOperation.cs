using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Application.QueryPipeline;

namespace RoslynMcpServer.Application.Navigation.Operations;

/// <summary>Executes the MethodSource query through the shared query pipeline.</summary>
public sealed class MethodSourceQueryOperation(
    IMethodSourceQueryDataProvider dataProvider,
    ILogger<MethodSourceQueryOperation> logger) : QueryOperationBase<GetMethodSourceRequest, GetMethodSourceResult, MethodSourceQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetMethodSourceResult>, Error<MethodSourceQueryError>, Canceled>> ExecuteCoreAsync(
        GetMethodSourceRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetMethodSourceAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<MethodSourceQueryError>(
                new MethodSourceQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Signature));
        }

        return new Success<GetMethodSourceResult>(providerResult);
    }

    protected override Error<MethodSourceQueryError>? Validate(GetMethodSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<MethodSourceQueryError>(
                new MethodSourceQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new Error<MethodSourceQueryError>(
                new MethodSourceQueryError(
                    "A non-empty filePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.Line <= 0 || request.Column <= 0)
        {
            return new Error<MethodSourceQueryError>(
                new MethodSourceQueryError(
                    "Line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        return null;
    }

    protected override Error<MethodSourceQueryError> MapUnhandledError(GetMethodSourceRequest request, Exception exception) =>
        new(new MethodSourceQueryError(
            "The server could not retrieve method source.",
            "Retry the request and ensure the solution is loaded."));
}
