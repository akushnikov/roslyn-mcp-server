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
/// Validates source coordinates before retrieving attributes and preserves resolved-symbol context on failure.
/// </summary>
public sealed class SymbolAttributesQueryOperation(
    ISymbolAttributesQueryDataProvider dataProvider,
    ILogger<SymbolAttributesQueryOperation> logger) : QueryOperationBase<GetSymbolAttributesRequest, GetSymbolAttributesResult, NavigationQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetSymbolAttributesResult>, Error<NavigationQueryError>, Canceled>> ExecuteCoreAsync(
        GetSymbolAttributesRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetSymbolAttributesAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<NavigationQueryError>(
                new NavigationQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Symbol));
        }

        return new Success<GetSymbolAttributesResult>(providerResult);
    }

    protected override Error<NavigationQueryError>? Validate(GetSymbolAttributesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<NavigationQueryError>(
                new NavigationQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new Error<NavigationQueryError>(
                new NavigationQueryError(
                    "A non-empty filePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.Line <= 0 || request.Column <= 0)
        {
            return new Error<NavigationQueryError>(
                new NavigationQueryError(
                    "Line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        return null;
    }

    protected override Error<NavigationQueryError> MapUnhandledError(GetSymbolAttributesRequest request, Exception exception) =>
        new(new NavigationQueryError(
            "The server could not retrieve symbol attributes.",
            "Retry the request and ensure the solution is loaded."));
}
