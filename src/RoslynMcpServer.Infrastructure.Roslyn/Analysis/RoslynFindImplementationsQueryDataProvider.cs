using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
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
    public async ValueTask<FindImplementationsResult> FindImplementationsAsync(
        FindImplementationsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolved = await helper.ResolveSymbolAsync(
            request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
        if (resolved.FailureReason is not null)
        {
            return new FindImplementationsResult(
                null, Array.Empty<SymbolDescriptor>(), resolved.FailureReason, resolved.Guidance);
        }

        if (resolved.Symbol is null || resolved.Session is null)
        {
            return new FindImplementationsResult(
                Symbol: null,
                Implementations: Array.Empty<SymbolDescriptor>(),
                FailureReason: "No symbol was resolved at the requested source position.",
                Guidance: "Move the cursor onto an interface, abstract member, or type declaration and try again.");
        }

        try
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                resolved.Symbol,
                resolved.Session.Solution,
                cancellationToken: cancellationToken);

            var results = implementations
                .Select(SymbolResolutionHelper.MapSymbol)
                .Where(static descriptor => descriptor is not null)
                .Cast<SymbolDescriptor>()
                .DistinctBy(static descriptor => $"{descriptor.Definition?.FilePath}:{descriptor.Definition?.Line}:{descriptor.Definition?.Column}:{descriptor.DisplayName}")
                .Take(Math.Min(request.MaxResults, 200))
                .ToArray();

            return new FindImplementationsResult(SymbolResolutionHelper.MapSymbol(resolved.Symbol), results, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to find implementations for {FilePath}:{Line}:{Column}",
                request.FilePath, request.Line, request.Column);
            return new FindImplementationsResult(
                Symbol: SymbolResolutionHelper.MapSymbol(resolved.Symbol),
                Implementations: Array.Empty<SymbolDescriptor>(),
                FailureReason: "The server could not compute implementations for the resolved symbol.",
                Guidance: "Retry the request on an interface, abstract member, or base type.");
        }
    }
}
