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
    public static ActivitySource ActivitySource { get; } = new("RoslynMcpServer.Application.OperationPipeline", "1.0.0");

    /// <summary>
    /// Meter used for operation-level metrics.
    /// </summary>
    public static Meter Meter { get; } = new("RoslynMcpServer.Application.OperationPipeline", "1.0.0");

    /// <summary>
    /// Duration metric for operation executions in milliseconds.
    /// </summary>
    public static Histogram<double> DurationMs { get; } = Meter.CreateHistogram<double>(
        "roslyn_mcp.operation.duration",
        unit: "ms",
        description: "Operation pipeline execution duration.");
}
