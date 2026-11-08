using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies type hierarchy from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynTypeHierarchyQueryDataProvider(
    SymbolResolutionHelper helper) : ITypeHierarchyQueryDataProvider
{
    /// <inheritdoc />
    public ValueTask<GetTypeHierarchyResult> GetTypeHierarchyAsync(
        GetTypeHierarchyRequest request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
