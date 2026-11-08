using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies implementations from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynFindImplementationsQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynFindImplementationsQueryDataProvider> logger) : IFindImplementationsQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<FindImplementationsResult> FindImplementationsAsync(
        FindImplementationsRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
