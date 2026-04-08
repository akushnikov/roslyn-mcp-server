using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;
using RoslynMcpServer.Abstractions.CommandPipeline.Services;

namespace RoslynMcpServer.Application.CommandPipeline;

/// <summary>
/// Provides a template execution pipeline for stateful command operations.
/// </summary>
/// <typeparam name="TRequest">Input contract for the command operation.</typeparam>
/// <typeparam name="TResult">Successful output contract for the command operation.</typeparam>
/// <typeparam name="TError">Typed error payload for the command operation.</typeparam>
public abstract class CommandOperationBase<TRequest, TResult, TError> : ICommandOperation<TRequest, TResult, TError>
    where TRequest : notnull
{
    /// <summary>
    /// Executes the command pipeline with cancellation checks, validation,
    /// core execution, and centralized unhandled error mapping.
    /// </summary>
    public async ValueTask<OneOf<Success<TResult>, Error<TError>, Canceled>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return new Canceled();
        }

        var validationFailure = Validate(request);
        if (validationFailure is not null)
        {
            return (Error<TError>)validationFailure;
        }

        try
        {
            return await ExecuteCoreAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new Canceled();
        }
        catch (Exception exception)
        {
            return MapUnhandledError(request, exception);
        }
    }

    /// <summary>
    /// Validates input request before core execution.
    /// </summary>
    protected virtual Error<TError>? Validate(TRequest request) => null;

    /// <summary>
    /// Executes the operation's domain-specific command logic.
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
}
