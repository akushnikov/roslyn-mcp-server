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

/// <summary>Executes TypeCompatibility queries through the shared validation, cancellation and telemetry pipeline.</summary>
public sealed class TypeCompatibilityQueryOperation(
    ITypeCompatibilityQueryDataProvider dataProvider,
    ILogger<TypeCompatibilityQueryOperation> logger) : QueryOperationBase<CheckTypeCompatibilityRequest, CheckTypeCompatibilityResult, TypeCompatibilityQueryError>(logger)
{
    protected override async ValueTask<OneOf<Success<CheckTypeCompatibilityResult>, Error<TypeCompatibilityQueryError>, Canceled>> ExecuteCoreAsync(
        CheckTypeCompatibilityRequest request,
        CancellationToken cancellationToken)
    {
        var providerResult = await dataProvider.CheckTypeCompatibilityAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(providerResult.FailureReason))
        {
            return new Error<TypeCompatibilityQueryError>(
                new TypeCompatibilityQueryError(providerResult.FailureReason, providerResult.Guidance, providerResult.Source, providerResult.Target));
        }

        return new Success<CheckTypeCompatibilityResult>(providerResult);
    }

    protected override Error<TypeCompatibilityQueryError>? Validate(CheckTypeCompatibilityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            return new Error<TypeCompatibilityQueryError>(
                new TypeCompatibilityQueryError(
                    "A non-empty solutionPath is required.",
                    "Pass an absolute path to a previously loaded .sln, .slnx, or .csproj file."));
        }

        if (string.IsNullOrWhiteSpace(request.SourceFilePath))
        {
            return new Error<TypeCompatibilityQueryError>(
                new TypeCompatibilityQueryError(
                    "A non-empty sourceFilePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.SourceLine <= 0 || request.SourceColumn <= 0)
        {
            return new Error<TypeCompatibilityQueryError>(
                new TypeCompatibilityQueryError(
                    "Source line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        if (string.IsNullOrWhiteSpace(request.TargetFilePath))
        {
            return new Error<TypeCompatibilityQueryError>(
                new TypeCompatibilityQueryError(
                    "A non-empty targetFilePath is required.",
                    "Pass an absolute path to a source document within the loaded solution."));
        }

        if (request.TargetLine <= 0 || request.TargetColumn <= 0)
        {
            return new Error<TypeCompatibilityQueryError>(
                new TypeCompatibilityQueryError(
                    "Target line and column must be positive 1-based values.",
                    "Use 1-based line and column values within the target source file."));
        }

        return null;
    }

    protected override Error<TypeCompatibilityQueryError> MapUnhandledError(
        CheckTypeCompatibilityRequest request,
        Exception exception)
    {
        return new Error<TypeCompatibilityQueryError>(
            new TypeCompatibilityQueryError(
                "The server could not check type compatibility for the requested symbols.",
                "Retry the request on typed symbols inside the loaded solution."));
    }
}
