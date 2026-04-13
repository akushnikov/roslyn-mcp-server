using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using CommandCanceled = RoslynMcpServer.Abstractions.CommandPipeline.Models.Canceled;
using RoslynMcpServer.Abstractions.CommandPipeline.Models;
using QueryCanceled = RoslynMcpServer.Abstractions.QueryPipeline.Models.Canceled;
using RoslynMcpServer.Abstractions.QueryPipeline.Models;
using RoslynMcpServer.Application.CommandPipeline;
using RoslynMcpServer.Application.QueryPipeline;
using RoslynMcpServer.Application.Telemetry;

namespace RoslynMcpServer.UnitTests.Application.Telemetry;

public sealed class OperationTelemetryPipelineTests
{
    [Fact]
    public async Task QueryPipeline_EmitsSuccessLogActivityAndMetric()
    {
        using var activityCapture = new ActivityCapture();
        using var meterCapture = new MeterCapture();
        var logger = new ListLogger<SuccessQueryOperation>();
        var operation = new SuccessQueryOperation(logger);

        var result = await operation.ExecuteAsync(new QueryRequest("ok"));
        var (success, error, canceled) = result;

        Assert.NotNull(success);
        Assert.Null(error);
        Assert.Null(canceled);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.LogLevel);
        Assert.Contains("finished with success", entry.Message, StringComparison.Ordinal);

        var activity = Assert.Single(activityCapture.CompletedActivities);
        Assert.Equal("success", GetStringTag(activity, "operation.outcome"));
        Assert.Equal(false, GetBoolTag(activity, "operation.exception"));

        var measurement = Assert.Single(meterCapture.Measurements);
        Assert.True(measurement.Value >= 0);
        Assert.Contains(measurement.Tags, static tag => tag.Key == "outcome" && Equals(tag.Value, "success"));
    }

    [Fact]
    public async Task QueryPipeline_EmitsErrorLogWithOriginalException()
    {
        using var activityCapture = new ActivityCapture();
        var logger = new ListLogger<ErrorQueryOperation>();
        var operation = new ErrorQueryOperation(logger);

        var result = await operation.ExecuteAsync(new QueryRequest("boom"));
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.NotNull(error);
        Assert.Null(canceled);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.LogLevel);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains("finished with error", entry.Message, StringComparison.Ordinal);

        var activity = Assert.Single(activityCapture.CompletedActivities);
        Assert.Equal("error", GetStringTag(activity, "operation.outcome"));
        Assert.Equal(typeof(InvalidOperationException).FullName, GetStringTag(activity, "operation.exception.type"));
    }

    [Fact]
    public async Task CommandPipeline_EmitsCanceledLogWithoutException()
    {
        var logger = new ListLogger<SuccessCommandOperation>();
        var operation = new SuccessCommandOperation(logger);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await operation.ExecuteAsync(new CommandRequest("cancel"), cts.Token);
        var (success, error, canceled) = result;

        Assert.Null(success);
        Assert.Null(error);
        Assert.NotNull(canceled);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.LogLevel);
        Assert.Null(entry.Exception);
        Assert.Contains("finished with canceled", entry.Message, StringComparison.Ordinal);
    }

    private sealed record QueryRequest(string Value);
    private sealed record QueryResult(string Value);
    private sealed record CommandRequest(string Value);
    private sealed record CommandResult(string Value);

    private static string? GetStringTag(Activity activity, string key)
        => activity.Tags.FirstOrDefault(tag => tag.Key == key).Value;

    private static bool? GetBoolTag(Activity activity, string key)
        => activity.TagObjects.FirstOrDefault(tag => tag.Key == key).Value as bool?;

    private sealed class SuccessQueryOperation(ILogger<SuccessQueryOperation> logger)
        : QueryOperationBase<QueryRequest, QueryResult, QueryError>(logger)
    {
        protected override ValueTask<OneOf<Success<QueryResult>, Error<QueryError>, QueryCanceled>> ExecuteCoreAsync(
            QueryRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<OneOf<Success<QueryResult>, Error<QueryError>, QueryCanceled>>(
                new Success<QueryResult>(new QueryResult(request.Value)));

        protected override Error<QueryError> MapUnhandledError(QueryRequest request, Exception exception)
            => new(new QueryError("error", "guidance"));
    }

    private sealed class ErrorQueryOperation(ILogger<ErrorQueryOperation> logger)
        : QueryOperationBase<QueryRequest, QueryResult, QueryError>(logger)
    {
        protected override ValueTask<OneOf<Success<QueryResult>, Error<QueryError>, QueryCanceled>> ExecuteCoreAsync(
            QueryRequest request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("simulated");

        protected override Error<QueryError> MapUnhandledError(QueryRequest request, Exception exception)
            => new(new QueryError("mapped", "guidance"));
    }

    private sealed class SuccessCommandOperation(ILogger<SuccessCommandOperation> logger)
        : CommandOperationBase<CommandRequest, CommandResult, CommandError>(logger)
    {
        protected override ValueTask<OneOf<Success<CommandResult>, Error<CommandError>, CommandCanceled>> ExecuteCoreAsync(
            CommandRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<OneOf<Success<CommandResult>, Error<CommandError>, CommandCanceled>>(
                new Success<CommandResult>(new CommandResult(request.Value)));
        }

        protected override Error<CommandError> MapUnhandledError(CommandRequest request, Exception exception)
            => new(new CommandError("error", "guidance"));
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ActivityListener _listener;

        public ActivityCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == "RoslynMcpServer.Application.OperationPipeline",
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => CompletedActivities.Add(activity)
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> CompletedActivities { get; } = [];

        public void Dispose() => _listener.Dispose();
    }

    private sealed class MeterCapture : IDisposable
    {
        private readonly MeterListener _listener = new();

        public MeterCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "RoslynMcpServer.Application.OperationPipeline" &&
                    instrument.Name == "roslyn_mcp.operation.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                Measurements.Add(new MeasurementRecord(measurement, tags.ToArray()));
            });

            _listener.Start();
        }

        public List<MeasurementRecord> Measurements { get; } = [];

        public void Dispose() => _listener.Dispose();
    }

    private sealed record MeasurementRecord(double Value, KeyValuePair<string, object?>[] Tags);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
