using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies callers from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynFindCallersQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynFindCallersQueryDataProvider> logger) : IFindCallersQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<FindCallersResult> FindCallersAsync(
        FindCallersRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
