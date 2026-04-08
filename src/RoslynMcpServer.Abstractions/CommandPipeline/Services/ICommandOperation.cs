using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;

namespace RoslynMcpServer.Abstractions.CommandPipeline.Services;

/// <summary>
/// Defines a reusable execution contract for state-changing command operations.
/// </summary>
/// <typeparam name="TRequest">Input contract for the command operation.</typeparam>
/// <typeparam name="TResult">Successful output contract produced by the command operation.</typeparam>
/// <typeparam name="TError">Typed error payload produced by the command operation.</typeparam>
public interface ICommandOperation<in TRequest, TResult, TError>
{
    /// <summary>
    /// Executes the command operation for the specified request.
    /// </summary>
    ValueTask<OneOf<Success<TResult>, Error<TError>, Canceled>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}
