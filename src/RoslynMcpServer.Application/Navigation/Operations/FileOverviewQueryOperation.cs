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

/// <summary>Validates document paths and promotes provider failures while retaining their document context.</summary>
public sealed class FileOverviewQueryOperation(
    IFileOverviewQueryDataProvider dataProvider,
    ILogger<FileOverviewQueryOperation> logger) : QueryOperationBase<GetFileOverviewRequest, GetFileOverviewResult, DocumentQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetFileOverviewResult>, Error<DocumentQueryError>, Canceled>> ExecuteCoreAsync(
        GetFileOverviewRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetFileOverviewAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<DocumentQueryError>(
                new DocumentQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.FilePath));
        }

        return new Success<GetFileOverviewResult>(providerResult);
    }

    protected override Error<DocumentQueryError>? Validate(GetFileOverviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<DocumentQueryError>(
                new DocumentQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new Error<DocumentQueryError>(
                new DocumentQueryError(
                    "A non-empty filePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        return null;
    }

    protected override Error<DocumentQueryError> MapUnhandledError(GetFileOverviewRequest request, Exception exception) =>
        new(new DocumentQueryError(
            "The server could not retrieve the file overview.",
            "Retry the request and ensure the solution is loaded."));
}
