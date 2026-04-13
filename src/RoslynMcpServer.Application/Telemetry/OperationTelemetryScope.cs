using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace RoslynMcpServer.Application.Telemetry;

internal static class OperationTelemetryScope
{
    internal sealed record State(string OperationName, Stopwatch Stopwatch, Activity? Activity);

    public static State Start(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return new State(
            operationName,
            Stopwatch.StartNew(),
            OperationTelemetryRuntime.ActivitySource.StartActivity(operationName, ActivityKind.Internal));
    }

    public static void Complete(ILogger logger, State state, string outcome, Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        state.Stopwatch.Stop();

        var durationMs = state.Stopwatch.Elapsed.TotalMilliseconds;
        var tags = new TagList
        {
            { "operation", state.OperationName },
            { "outcome", outcome }
        };

        OperationTelemetryRuntime.DurationMs.Record(durationMs, tags);

        state.Activity?.SetTag("operation.name", state.OperationName);
        state.Activity?.SetTag("operation.duration.ms", durationMs);
        state.Activity?.SetTag("operation.outcome", outcome);
        state.Activity?.SetTag("operation.canceled", string.Equals(outcome, "canceled", StringComparison.Ordinal));
        state.Activity?.SetTag("operation.exception", exception is not null);
        if (exception is not null)
        {
            state.Activity?.SetTag("operation.exception.type", exception.GetType().FullName);
        }

        state.Activity?.Stop();

        if (exception is not null)
        {
            logger.LogError(
                exception,
                "Operation {OperationName} finished with {Outcome} after {DurationMs} ms. ExceptionType={ExceptionType}",
                state.OperationName,
                outcome,
                durationMs,
                exception.GetType().FullName);
            return;
        }

        logger.LogDebug(
            "Operation {OperationName} finished with {Outcome} after {DurationMs} ms.",
            state.OperationName,
            outcome,
            durationMs);
    }
}
