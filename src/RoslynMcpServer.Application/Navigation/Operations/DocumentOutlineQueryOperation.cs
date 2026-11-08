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

/// <summary>
/// Validates document paths before provider access and preserves available file context in typed failures.
/// </summary>
public sealed class DocumentOutlineQueryOperation(
    IDocumentOutlineQueryDataProvider dataProvider,
    ILogger<DocumentOutlineQueryOperation> logger) : QueryOperationBase<GetDocumentOutlineRequest, GetDocumentOutlineResult, DocumentQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetDocumentOutlineResult>, Error<DocumentQueryError>, Canceled>> ExecuteCoreAsync(
        GetDocumentOutlineRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetDocumentOutlineAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<DocumentQueryError>(
                new DocumentQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.FilePath));
        }

        return new Success<GetDocumentOutlineResult>(providerResult);
    }

    protected override Error<DocumentQueryError>? Validate(GetDocumentOutlineRequest request)
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

    protected override Error<DocumentQueryError> MapUnhandledError(GetDocumentOutlineRequest request, Exception exception) =>
        new(new DocumentQueryError(
            "The server could not retrieve the document outline.",
            "Retry the request and ensure the solution is loaded."));
}
