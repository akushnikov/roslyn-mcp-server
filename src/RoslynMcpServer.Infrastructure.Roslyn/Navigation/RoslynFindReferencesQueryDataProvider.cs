using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RoslynMcpServer.Abstractions.Navigation.Models;
using RoslynMcpServer.Abstractions.Navigation.Requests;
using RoslynMcpServer.Abstractions.Navigation.Results;
using RoslynMcpServer.Abstractions.Navigation.Services;

namespace RoslynMcpServer.Infrastructure.Roslyn.Navigation;

/// <summary>
/// Resolves find-references query data from Roslyn workspace sessions.
/// </summary>
internal sealed class RoslynFindReferencesQueryDataProvider(
    SymbolResolutionHelper helper,
    ILogger<RoslynFindReferencesQueryDataProvider> logger) : IFindReferencesQueryDataProvider
{
    /// <inheritdoc />
    public async ValueTask<FindReferencesResult> FindReferencesAsync(
        FindReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SymbolDescriptor? resolvedSymbol = null;

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
                return new FindReferencesResult(
                    Symbol: null,
                    References: Array.Empty<SymbolReferenceDescriptor>(),
                    FailureReason: resolved.FailureReason,
                    Guidance: resolved.Guidance);
            }

            resolvedSymbol = SymbolResolutionHelper.MapSymbol(resolved.Symbol);
            var referencedSymbols = await SymbolFinder.FindReferencesAsync(
                resolved.Symbol!,
                resolved.Session!.Solution,
                cancellationToken);

            var maxResults = Math.Min(request.MaxResults, 200);
            var references = new List<SymbolReferenceDescriptor>(Math.Min(maxResults, 64));

            foreach (var referencedSymbol in referencedSymbols)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var descriptor = await MapReferenceAsync(location, cancellationToken);
                    if (descriptor is null)
                    {
                        continue;
                    }

                    references.Add(descriptor);
                    if (references.Count >= maxResults)
                    {
                        return new FindReferencesResult(
                            resolvedSymbol, references, null, null);
                    }
                }
            }

            return new FindReferencesResult(
                resolvedSymbol, references, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to find references for {FilePath}:{Line}:{Column}",
                request.FilePath,
                request.Line,
                request.Column);

            return new FindReferencesResult(
                Symbol: resolvedSymbol,
                References: Array.Empty<SymbolReferenceDescriptor>(),
                FailureReason: "The server could not compute references for the resolved symbol.",
                Guidance: "Retry the request and confirm the file position resolves to a source symbol.");
        }
    }

    private static async ValueTask<SymbolReferenceDescriptor?> MapReferenceAsync(
        ReferenceLocation location,
        CancellationToken cancellationToken)
    {
        var lineSpan = location.Location.GetLineSpan();
        var sourceTree = location.Location.SourceTree;
        var sourceText = sourceTree is null
            ? null
            : await sourceTree.GetTextAsync(cancellationToken);

        string? lineText = null;
        if (sourceText is not null && lineSpan.StartLinePosition.Line < sourceText.Lines.Count)
        {
            lineText = sourceText.Lines[lineSpan.StartLinePosition.Line].ToString().Trim();
        }

        return new SymbolReferenceDescriptor(
            Location: new SymbolLocation(
                location.Document.FilePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            ContainingSymbol: null,
            LineText: lineText);
    }
}
