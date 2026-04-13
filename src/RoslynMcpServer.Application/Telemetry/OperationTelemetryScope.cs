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
            { OperationTelemetryConventions.MetricTagOperation, state.OperationName },
            { OperationTelemetryConventions.MetricTagOutcome, outcome }
        };

        OperationTelemetryRuntime.DurationMs.Record(durationMs, tags);

        state.Activity?.SetTag(OperationTelemetryConventions.ActivityTagOperationName, state.OperationName);
        state.Activity?.SetTag(OperationTelemetryConventions.ActivityTagDurationMs, durationMs);
        state.Activity?.SetTag(OperationTelemetryConventions.ActivityTagOutcome, outcome);
        state.Activity?.SetTag(
            OperationTelemetryConventions.ActivityTagCanceled,
            string.Equals(outcome, OperationTelemetryConventions.OutcomeCanceled, StringComparison.Ordinal));
        state.Activity?.SetTag(OperationTelemetryConventions.ActivityTagException, exception is not null);
        if (exception is not null)
        {
            state.Activity?.SetTag(OperationTelemetryConventions.ActivityTagExceptionType, exception.GetType().FullName);
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
