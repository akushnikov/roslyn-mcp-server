using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;

namespace RoslynMcpServer.Application.QueryPipeline;

internal static class QueryResultMapper
{
    public static TContract Map<TResult, TContract>(
        OneOf<Success<TResult>, Error<QueryError>, Canceled> result,
        Func<TResult, TContract> fromSuccess,
        Func<QueryError, TContract> fromFailure,
        Func<TContract> fromCanceled)
        => result.Match(
            success => fromSuccess(success.Value),
            error => fromFailure(error.Value),
            _ => fromCanceled());
}
