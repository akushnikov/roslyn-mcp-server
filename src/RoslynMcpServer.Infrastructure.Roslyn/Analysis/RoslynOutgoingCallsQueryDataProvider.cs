using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies OutgoingCalls query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynOutgoingCallsQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynOutgoingCallsQueryDataProvider> logger) : IOutgoingCallsQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetOutgoingCallsResult> GetOutgoingCallsAsync(
        GetOutgoingCallsRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = helper;
        _ = logger;
        throw new NotImplementedException();
    }
}
