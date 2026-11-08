using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies MethodAnalysis query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynMethodAnalysisQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynMethodAnalysisQueryDataProvider> logger) : IMethodAnalysisQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<AnalyzeMethodResult> AnalyzeMethodAsync(
        AnalyzeMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = helper;
        _ = logger;
        throw new NotImplementedException();
    }
}
