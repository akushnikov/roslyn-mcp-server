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

/// <summary>Executes the TypeOverview query through the shared query pipeline.</summary>
public sealed class TypeOverviewQueryOperation(
    ITypeOverviewQueryDataProvider dataProvider,
    ILogger<TypeOverviewQueryOperation> logger) : QueryOperationBase<GetTypeOverviewRequest, GetTypeOverviewResult, NavigationQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetTypeOverviewResult>, Error<NavigationQueryError>, Canceled>> ExecuteCoreAsync(
        GetTypeOverviewRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetTypeOverviewAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<NavigationQueryError>(
                new NavigationQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Type));
        }

        return new Success<GetTypeOverviewResult>(providerResult);
    }

    protected override Error<NavigationQueryError>? Validate(GetTypeOverviewRequest request)
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

    protected override Error<NavigationQueryError> MapUnhandledError(GetTypeOverviewRequest request, Exception exception) =>
        new(new NavigationQueryError(
            "The server could not retrieve the type overview.",
            "Retry the request and ensure the solution is loaded."));
}
