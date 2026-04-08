using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the application boundary for the symbol-info query used by MCP adapters.
/// </summary>
public interface ISymbolInfoQueryService
{
    /// <summary>
    /// Returns semantic information for the symbol resolved at a source position.
    /// </summary>
    ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default);
}
