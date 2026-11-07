using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;
using RoslynMcpServer.Infrastructure.Roslyn.Workspace;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>
/// Searches source declarations in Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynSearchSymbolsQueryDataProvider(
    IWorkspaceSessionProvider workspaceSessionProvider,
    ILogger<RoslynSearchSymbolsQueryDataProvider> logger) : ISearchSymbolsQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<SearchSymbolsResult> SearchSymbolsAsync(
        SearchSymbolsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var session = await workspaceSessionProvider.GetReadySessionAsync(request.SolutionPath, cancellationToken);
            if (session is null)
            {
                return new SearchSymbolsResult(
                    Symbols: Array.Empty<SymbolDescriptor>(),
                    FailureReason: "The requested solution is not currently loaded.",
                    Guidance: "Call load_solution first for the requested solutionPath.");
            }

            var symbols = await SymbolFinder.FindSourceDeclarationsAsync(
                session.Solution,
                name => name.Contains(request.Query, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.TypeAndMember,
                cancellationToken);

            var maxResults = Math.Min(request.MaxResults, 200);

            var results = symbols
                .Select(SymbolResolutionHelper.MapSymbol)
                .Where(static descriptor => descriptor is not null)
                .Cast<SymbolDescriptor>()
                .Where(descriptor => MatchesKindFilter(descriptor, request.KindFilter))
                .OrderBy(static descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static descriptor => descriptor.ContainerName, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToArray();

            return new SearchSymbolsResult(results, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to search symbols in {SolutionPath} for {Query}",
                request.SolutionPath,
                request.Query);

            return new SearchSymbolsResult(
                Symbols: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The server could not complete the symbol search.",
                Guidance: "Retry the request and ensure the solution is loaded.");
        }
    }

    private static bool MatchesKindFilter(SymbolDescriptor descriptor, string? kindFilter) =>
        string.IsNullOrWhiteSpace(kindFilter) ||
        descriptor.Kind.Equals(kindFilter, StringComparison.OrdinalIgnoreCase);
}
