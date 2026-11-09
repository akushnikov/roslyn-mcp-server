using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>
/// Resolves symbol-info query data through the shared Roslyn symbol resolver.
/// </summary>
internal sealed class RoslynSymbolInfoQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynSymbolInfoQueryDataProvider> logger) : ISymbolInfoQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GetSymbolInfoResult> GetSymbolInfoAsync(
        GetSymbolInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);

            return new GetSymbolInfoResult(
                SymbolResolutionHelper.MapSymbol(resolved.Symbol),
                resolved.FailureReason,
                resolved.Guidance);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to resolve symbol info for {FilePath}:{Line}:{Column}",
                request.FilePath,
                request.Line,
                request.Column);

            return new GetSymbolInfoResult(
                Symbol: null,
                FailureReason: "The server could not resolve symbol information.",
                Guidance: "Retry the request and ensure the file belongs to a loaded solution.");
        }
    }
}
