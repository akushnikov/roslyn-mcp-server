using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Analysis.Models;
using RoslynMcpServer.Abstractions.Analysis.Requests;
using RoslynMcpServer.Abstractions.Analysis.Results;
using RoslynMcpServer.Abstractions.Analysis.Services;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Infrastructure.Roslyn.Navigation;

namespace RoslynMcpServer.Infrastructure.Roslyn.Analysis;

/// <summary>
/// Supplies callers from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynFindCallersQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynFindCallersQueryDataProvider> logger) : IFindCallersQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<FindCallersResult> FindCallersAsync(
        FindCallersRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SymbolDescriptor? resolvedSymbol = null;

        try
        {
            var resolved = await helper.ResolveSymbolAsync(
                request.SolutionPath, request.FilePath, request.Line, request.Column, cancellationToken);
            if (resolved.FailureReason is not null)
            {
                return new FindCallersResult(null, Array.Empty<CallerDescriptor>(), resolved.FailureReason, resolved.Guidance);
            }

            if (resolved.Symbol is null || resolved.Session is null)
            {
                return new FindCallersResult(
                    Symbol: null,
                    Callers: Array.Empty<CallerDescriptor>(),
                    FailureReason: "No symbol was resolved at the requested source position.",
                    Guidance: "Move the cursor onto a method, property, or other callable member and try again.");
            }

            resolvedSymbol = SymbolResolutionHelper.MapSymbol(resolved.Symbol);
            var callers = await SymbolFinder.FindCallersAsync(resolved.Symbol, resolved.Session.Solution, cancellationToken);
            var results = new List<CallerDescriptor>();
            var maxResults = Math.Min(request.MaxResults, 200);

            foreach (var caller in callers)
            {
                foreach (var location in caller.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var descriptor = await MapCallerAsync(caller.CallingSymbol?.ToDisplayString(), location, cancellationToken);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    results.Add(descriptor);
                    if (results.Count >= maxResults)
                    {
                        return new FindCallersResult(resolvedSymbol, results, null, null);
                    }
                }
            }

            return new FindCallersResult(resolvedSymbol, results, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to find callers for {FilePath}:{Line}:{Column}", request.FilePath, request.Line, request.Column);
            return new FindCallersResult(
                Symbol: resolvedSymbol,
                Callers: Array.Empty<CallerDescriptor>(),
                FailureReason: "The server could not compute callers for the resolved symbol.",
                Guidance: "Retry the request on a callable symbol inside the loaded solution.");
        }
    }

    private static async Task<CallerDescriptor?> MapCallerAsync(
        string? callingSymbol,
        Location location,
        CancellationToken cancellationToken)
    {
        if (!location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        var sourceTree = location.SourceTree;
        var sourceText = sourceTree is null ? null : await sourceTree.GetTextAsync(cancellationToken);

        string? lineText = null;
        if (sourceText is not null && lineSpan.StartLinePosition.Line < sourceText.Lines.Count)
        {
            lineText = sourceText.Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
        }

        return new CallerDescriptor(
            callingSymbol,
            new SymbolLocation(lineSpan.Path, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1),
            lineText);
    }
}
