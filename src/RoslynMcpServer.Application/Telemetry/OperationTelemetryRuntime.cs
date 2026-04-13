using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RoslynMcpServer.Application.Telemetry;

/// <summary>
/// Hosts telemetry primitives and mutable sink registration for operation aspects.
/// </summary>
public static class OperationTelemetryRuntime
{
    /// <summary>
    /// Activity source used for operation-level tracing.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(
        OperationTelemetryConventions.ActivitySourceName,
        OperationTelemetryConventions.ActivitySourceVersion);

    /// <summary>
    /// Meter used for operation-level metrics.
    /// </summary>
    public static Meter Meter { get; } = new(
        OperationTelemetryConventions.MeterName,
        OperationTelemetryConventions.MeterVersion);

    /// <summary>
    /// Duration metric for operation executions in milliseconds.
    /// </summary>
    public static Histogram<double> DurationMs { get; } = Meter.CreateHistogram<double>(
        OperationTelemetryConventions.DurationMetricName,
        unit: OperationTelemetryConventions.DurationMetricUnit,
        description: OperationTelemetryConventions.DurationMetricDescription);
}
