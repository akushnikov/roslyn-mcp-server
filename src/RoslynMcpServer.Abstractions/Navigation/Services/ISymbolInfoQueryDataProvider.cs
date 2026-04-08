using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;

namespace RoslynMcpServer.Abstractions.Navigation.Services;

/// <summary>
/// Defines the backend data provider contract for symbol-info query operations.
/// </summary>
public interface ISymbolInfoQueryDataProvider
{
    /// <summary>
    /// Resolves semantic symbol information from the underlying workspace backend.
    /// </summary>
    ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default);
}
