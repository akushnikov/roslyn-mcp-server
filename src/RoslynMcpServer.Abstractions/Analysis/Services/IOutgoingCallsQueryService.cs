using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;

namespace RoslynMcpServer.Abstractions.Analysis.Services;

/// <summary>
/// Defines the application boundary consumed by MCP adapters for OutgoingCalls queries.
/// </summary>
public interface IOutgoingCallsQueryService
{
    /// <summary>Returns the query payload from the loaded workspace.</summary>
    /// <param name="request">Workspace and query selection.</param>
    /// <param name="cancellationToken">Token used to cancel query execution.</param>
    /// <returns>The query payload or a structured failure.</returns>
    ValueTask<GetOutgoingCallsResult> GetOutgoingCallsAsync(
        GetOutgoingCallsRequest request,
        CancellationToken cancellationToken = default);
}
