using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>
/// Resolves go-to-definition query data from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynGoToDefinitionQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynGoToDefinitionQueryDataProvider> logger) : IGoToDefinitionQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<GoToDefinitionResult> GoToDefinitionAsync(
        GoToDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath,
                request.FilePath,
                request.Line,
                request.Column,
                cancellationToken);

            if (resolved.FailureReason is not null)
            {
                return new GoToDefinitionResult(
                    Symbol: null,
                    Definitions: Array.Empty<SymbolLocation>(),
                    FailureReason: resolved.FailureReason,
                    Guidance: resolved.Guidance);
            }

            var definitions = SymbolResolutionHelper.GetSourceLocations(resolved.Symbol!).ToArray();
            if (definitions.Length == 0)
            {
                return new GoToDefinitionResult(
                    Symbol: SymbolResolutionHelper.MapSymbol(resolved.Symbol),
                    Definitions: Array.Empty<SymbolLocation>(),
                    FailureReason: "No source definition was found for the resolved symbol.",
                    Guidance: "The symbol may come from metadata or generated code outside the loaded solution.");
            }

            return new GoToDefinitionResult(
                SymbolResolutionHelper.MapSymbol(resolved.Symbol),
                definitions,
                null,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to resolve definitions for {FilePath}:{Line}:{Column}",
                request.FilePath,
                request.Line,
                request.Column);

            return new GoToDefinitionResult(
                Symbol: null,
                Definitions: Array.Empty<SymbolLocation>(),
                FailureReason: "The server could not resolve symbol definitions.",
                Guidance: "Retry the request and ensure the solution is loaded.");
        }
    }
}
