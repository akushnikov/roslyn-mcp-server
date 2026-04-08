using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;

namespace RoslynMcpServer.Abstractions.QueryPipeline.Services;

/// <summary>
/// Defines a reusable execution contract for read-only query operations.
/// </summary>
/// <typeparam name="TRequest">Input contract for the query operation.</typeparam>
/// <typeparam name="TResult">Successful output contract produced by the query operation.</typeparam>
/// <typeparam name="TError">Typed error payload produced by the query operation.</typeparam>
public interface IQueryOperation<in TRequest, TResult, TError>
{
    /// <summary>
    /// Executes the query operation for the specified request.
    /// </summary>
    ValueTask<OneOf<Success<TResult>, Error<TError>, Canceled>> ExecuteAsync(
        TRequest request,
        CancellationToken cancellationToken = default);
}
