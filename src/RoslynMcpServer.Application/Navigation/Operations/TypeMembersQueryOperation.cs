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
/// Executes the type-members query through the shared query pipeline.
/// </summary>
public sealed class TypeMembersQueryOperation(
    ITypeMembersQueryDataProvider dataProvider,
    ILogger<TypeMembersQueryOperation> logger) : QueryOperationBase<GetTypeMembersRequest, GetTypeMembersResult, NavigationQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<GetTypeMembersResult>, Error<NavigationQueryError>, Canceled>> ExecuteCoreAsync(
        GetTypeMembersRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.GetTypeMembersAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<NavigationQueryError>(
                new NavigationQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Type));
        }

        return new Success<GetTypeMembersResult>(providerResult);
    }

    protected override Error<NavigationQueryError>? Validate(GetTypeMembersRequest request)
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

    protected override Error<NavigationQueryError> MapUnhandledError(
        GetTypeMembersRequest request,
        Exception exception)
    {
        return new Error<NavigationQueryError>(
            new NavigationQueryError(
                "The server could not retrieve type members.",
                "Retry the request and ensure the solution is loaded."));
    }
}
