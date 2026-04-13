namespace RoslynMcpServer.Application.Telemetry;

internal static class OperationTelemetryConventions
{
    public const string ActivitySourceName = "RoslynMcpServer.Application.OperationPipeline";
    public const string ActivitySourceVersion = "1.0.0";
    public const string MeterName = "RoslynMcpServer.Application.OperationPipeline";
    public const string MeterVersion = "1.0.0";
    public const string DurationMetricName = "roslyn_mcp.operation.duration";
    public const string DurationMetricUnit = "ms";
    public const string DurationMetricDescription = "Operation pipeline execution duration.";

    public const string OutcomeSuccess = "success";
    public const string OutcomeError = "error";
    public const string OutcomeCanceled = "canceled";

    public const string MetricTagOperation = "operation";
    public const string MetricTagOutcome = "outcome";

    public const string ActivityTagOperationName = "operation.name";
    public const string ActivityTagDurationMs = "operation.duration.ms";
    public const string ActivityTagOutcome = "operation.outcome";
    public const string ActivityTagCanceled = "operation.canceled";
    public const string ActivityTagException = "operation.exception";
    public const string ActivityTagExceptionType = "operation.exception.type";
}
