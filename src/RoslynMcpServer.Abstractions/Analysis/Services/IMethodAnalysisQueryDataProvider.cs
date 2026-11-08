using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;

namespace RoslynMcpServer.Abstractions.Analysis.Services;

/// <summary>
/// Defines the backend boundary consumed by query operations for MethodAnalysis queries.
/// </summary>
public interface IMethodAnalysisQueryDataProvider
{
    /// <summary>Returns the query payload from the loaded workspace.</summary>
    /// <param name="request">Workspace and query selection.</param>
    /// <param name="cancellationToken">Token used to cancel query execution.</param>
    /// <returns>The query payload or a structured failure.</returns>
    ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(
        AnalyzeMethodRequest request,
        CancellationToken cancellationToken = default);
}
