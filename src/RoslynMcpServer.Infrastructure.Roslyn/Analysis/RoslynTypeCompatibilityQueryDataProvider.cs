using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>Supplies TypeCompatibility query data from the loaded Roslyn workspace.</summary>
internal sealed class RoslynTypeCompatibilityQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynTypeCompatibilityQueryDataProvider> logger) : ITypeCompatibilityQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<CheckTypeCompatibilityResult> CheckTypeCompatibilityAsync(
        CheckTypeCompatibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = helper;
        _ = logger;
        throw new NotImplementedException();
    }
}
