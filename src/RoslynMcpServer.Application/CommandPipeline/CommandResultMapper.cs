using OneOf;
using OneOf.Types;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;

namespace RoslynMcpServer.Application.CommandPipeline;

/// <summary>
/// Maps the three-way command pipeline result union into a transport-safe result contract.
/// </summary>
/// <remarks>
/// Centralizes the repeated <c>result.Match(...)</c> mechanics for command service adapters
/// while allowing each service to provide feature-specific failure and cancellation payloads.
/// </remarks>
internal static class CommandResultMapper
{
    /// <summary>
    /// Maps a command pipeline result to a transport-safe contract by applying the
    /// provided factory delegates for success, failure, and cancellation outcomes.
    /// </summary>
    /// <typeparam name="TResult">Successful command payload type.</typeparam>
    /// <typeparam name="TContract">Transport-safe result contract type.</typeparam>
    /// <param name="result">The raw command pipeline result union.</param>
    /// <param name="fromSuccess">Factory for the success case; receives the successful payload.</param>
    /// <param name="fromFailure">Factory for the failure case; receives the command error payload.</param>
    /// <param name="fromCanceled">Factory for the cancellation case.</param>
    /// <returns>A transport-safe result contract populated by the matching factory.</returns>
    public static TContract Map<TResult, TContract>(
        OneOf<Success<TResult>, Error<CommandError>, Canceled> result,
        Func<TResult, TContract> fromSuccess,
        Func<CommandError, TContract> fromFailure,
        Func<TContract> fromCanceled)
        => result.Match(
            success => fromSuccess(success.Value),
            error => fromFailure(error.Value),
            _ => fromCanceled());
}
