using OneOf;
using OneOf.Types;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Abstractions.QueryPipeline.Services;
using RoslynMcpServer.Application.Telemetry;

namespace RoslynMcpServer.Application.QueryPipeline;

/// <summary>
/// Provides a template execution pipeline for query operations.
/// </summary>
/// <typeparam name="TRequest">Input contract for the query operation.</typeparam>
/// <typeparam name="TResult">Successful output contract for the query operation.</typeparam>
/// <typeparam name="TError">Typed error payload for the query operation.</typeparam>
public abstract class QueryOperationBase<TRequest, TResult, TError> : IQueryOperation<TRequest, TResult, TError>
    where TRequest : notnull
{
    private readonly ILogger _logger;
    private readonly string _operationName;

    protected QueryOperationBase(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationName = $"{GetType().FullName}.ExecuteAsync";
    }

    /// <summary>
    /// Executes the query pipeline with cancellation checks, validation,
    /// core execution, and centralized unhandled error mapping.
    /// </summary>
    public async ValueTask<OneOf<Success<TResult>, Error<TError>, Canceled>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var telemetryState = OperationTelemetryScope.Start(_operationName);

        if (cancellationToken.IsCancellationRequested)
        {
            return Complete(new Canceled(), telemetryState);
        }

        var validationFailure = Validate(request);
        if (validationFailure is not null)
        {
            return Complete(validationFailure.Value, telemetryState);
        }

        try
        {
            var result = await ExecuteCoreAsync(request, cancellationToken);
            return Complete(result, telemetryState);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(new Canceled(), telemetryState);
            }

            throw;
        }
        catch (Exception exception)
        {
            return Complete(MapUnhandledError(request, exception), telemetryState, exception);
        }
    }

    /// <summary>
    /// Validates input request before core execution.
    /// </summary>
    protected virtual Error<TError>? Validate(TRequest request) => null;

    /// <summary>
    /// Executes the operation's domain-specific query logic.
    /// </summary>
    protected abstract ValueTask<OneOf<Success<TResult>, Error<TError>, Canceled>> ExecuteCoreAsync(
        TRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Converts unhandled exceptions into transport-safe result contracts.
    /// </summary>
    protected abstract Error<TError> MapUnhandledError(
        TRequest request,
        Exception exception);

    private OneOf<Success<TResult>, Error<TError>, Canceled> Complete(
        OneOf<Success<TResult>, Error<TError>, Canceled> result,
        OperationTelemetryScope.State telemetryState,
        Exception? exception = null)
    {
        var outcome = result.Match(
            _ => OperationTelemetryConventions.OutcomeSuccess,
            _ => OperationTelemetryConventions.OutcomeError,
            _ => OperationTelemetryConventions.OutcomeCanceled);

        OperationTelemetryScope.Complete(_logger, telemetryState, outcome, exception);
        return result;
    }
}
