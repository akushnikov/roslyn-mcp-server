using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies ChangeImpact query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynChangeImpactQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynChangeImpactQueryDataProvider> logger) : IChangeImpactQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<AnalyzeChangeImpactResult> AnalyzeChangeImpactAsync(
        AnalyzeChangeImpactRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = helper;
        _ = logger;
        throw new NotImplementedException();
    }
}
